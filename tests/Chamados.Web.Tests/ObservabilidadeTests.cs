using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Chamados.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Web.Tests;

/// <summary>A métrica de negócio é contada quando um chamado é aberto pela API.</summary>
public sealed class ObservabilidadeTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    [Fact]
    public async Task Abrir_chamado_soma_um_na_metrica_chamados_abertos()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var fabricaDeMedidores = fabrica.Services.GetRequiredService<IMeterFactory>();
        long total = 0;
        string? prioridade = null;

        using var ouvinte = new MeterListener();
        ouvinte.InstrumentPublished = (instrumento, l) =>
        {
            // Só o medidor "Chamados" desta aplicação: outra fábrica de testes em paralelo não interfere.
            if (instrumento.Meter.Scope == fabricaDeMedidores && instrumento.Name == "chamados.abertos")
            {
                l.EnableMeasurementEvents(instrumento);
            }
        };
        ouvinte.SetMeasurementEventCallback<long>((_, valor, etiquetas, _) =>
        {
            Interlocked.Add(ref total, valor);
            prioridade = etiquetas[0].Value as string;
        });
        ouvinte.Start();

        var resposta = await atendente.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Medindo a métrica de abertura", prioridade = "Critica" });

        resposta.EnsureSuccessStatusCode();
        Assert.Equal(1, Interlocked.Read(ref total));
        Assert.Equal(nameof(Prioridade.Critica), prioridade);
    }
}
