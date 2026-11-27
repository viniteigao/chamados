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

    [Fact]
    public async Task Login_pela_api_registra_recusa_e_aceite_pelo_id_e_nunca_pelo_email()
    {
        // Melhorias: antes, o endpoint de token não registrava nada além de "respondeu 401".
        var cliente = fabrica.ClienteAnonimo();
        await cliente.PostAsJsonAsync(new Uri("/api/conta/token", UriKind.Relative),
            new { email = Acesso.Coordenadora, senha = "senha-errada" });
        await fabrica.ClienteComTokenAsync(Acesso.Coordenadora);

        var eventos = fabrica.Registros.Todos.Where(r => r.Categoria == "Chamados.Web.Api.EndpointsConta").ToList();

        Assert.Contains(eventos, r => r.EventoId == 1002);   // Login recusado
        Assert.Contains(eventos, r => r.EventoId == 1001);   // Login aceito
        Assert.DoesNotContain(eventos, r => r.Mensagem.Contains(Acesso.Coordenadora, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Resolver_dentro_do_prazo_conta_na_metrica_com_a_etiqueta_no_prazo()
    {
        // Prazos: chamados.resolvidos{no_prazo=true|false}. Resolvido logo após abrir: dentro do prazo.
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var fabricaDeMedidores = fabrica.Services.GetRequiredService<IMeterFactory>();
        var etiquetas = new System.Collections.Concurrent.ConcurrentQueue<object?>();

        using var ouvinte = new MeterListener();
        ouvinte.InstrumentPublished = (instrumento, l) =>
        {
            if (instrumento.Meter.Scope == fabricaDeMedidores && instrumento.Name == "chamados.resolvidos")
            {
                l.EnableMeasurementEvents(instrumento);
            }
        };
        ouvinte.SetMeasurementEventCallback<long>((_, _, tags, _) => etiquetas.Enqueue(tags[0].Value));
        ouvinte.Start();

        var resposta = await atendente.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Disjuntor desarmando", prioridade = "Critica" });
        var criado = await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes);
        (await atendente.PostAsJsonAsync(new Uri($"/api/chamados/{criado!.Id}/atendimento", UriKind.Relative),
            new { })).EnsureSuccessStatusCode();
        (await atendente.PostAsJsonAsync(new Uri($"/api/chamados/{criado.Id}/resolucao", UriKind.Relative),
            new { solucao = "Disjuntor trocado" })).EnsureSuccessStatusCode();

        Assert.Equal(true, Assert.Single(etiquetas));
    }
}
