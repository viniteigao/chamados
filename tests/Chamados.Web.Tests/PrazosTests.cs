using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chamados.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Chamados.Web.Tests;

/// <summary>
/// Prazos: a aplicação inteira, com a política de prazos vinda da configuração. Para ver chamados
/// vencidos sem esperar dias, uma cópia da aplicação usa prazos de milissegundos.
/// </summary>
public sealed class PrazosTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    [Fact]
    public async Task Indicadores_de_prazo_sao_so_da_coordenacao_e_trazem_a_tabela_configurada()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var coordenadora = await fabrica.ClienteComTokenAsync(Acesso.Coordenadora);

        var negado = await atendente.GetAsync(new Uri("/api/chamados/prazos", UriKind.Relative));
        var indicadores = await coordenadora.GetFromJsonAsync<IndicadoresDePrazo>(
            new Uri("/api/chamados/prazos", UriKind.Relative), Json.Opcoes);

        Assert.Equal(HttpStatusCode.Forbidden, negado.StatusCode);
        Assert.Equal(TimeSpan.FromHours(4), indicadores!.Prazos[Prioridade.Critica]);
        Assert.Equal(TimeSpan.FromDays(7), indicadores.Prazos[Prioridade.Baixa]);
    }

    [Fact]
    public async Task Detalhe_na_api_traz_o_vencimento_pela_prioridade()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var resposta = await atendente.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Quadro de energia faiscando", prioridade = "Critica" });
        var criado = await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes);

        using var detalhe = JsonDocument.Parse(
            await atendente.GetStringAsync(new Uri($"/api/chamados/{criado!.Id}", UriKind.Relative)));
        var raiz = detalhe.RootElement;
        var prazo = raiz.GetProperty("prazo");

        Assert.Equal("NoPrazo", prazo.GetProperty("situacao").GetString());
        Assert.Equal(raiz.GetProperty("criadoEm").GetDateTimeOffset().AddHours(4), prazo.GetProperty("vencimento").GetDateTimeOffset());
    }

    [Fact]
    public async Task Com_prazos_curtissimos_todos_os_em_andamento_aparecem_como_vencidos()
    {
        await using var apressada = fabrica.WithWebHostBuilder(builder =>
            ComPrazos(builder, critica: "00:00:00.001", alta: "00:00:00.002", media: "00:00:00.003", baixa: "00:00:00.004"));
        var coordenadora = await apressada.ClienteComTokenAsync(Acesso.Coordenadora);
        var navegador = await apressada.ClienteConectadoAsync(Acesso.Coordenadora);

        var todos = await coordenadora.GetFromJsonAsync<List<ResumoChamado>>(new Uri("/api/chamados", UriKind.Relative), Json.Opcoes);
        var vencidos = await coordenadora.GetFromJsonAsync<List<ResumoChamado>>(
            new Uri("/api/chamados?somenteVencidos=true", UriKind.Relative), Json.Opcoes);
        var indicadores = await coordenadora.GetFromJsonAsync<IndicadoresDePrazo>(
            new Uri("/api/chamados/prazos", UriKind.Relative), Json.Opcoes);
        var painel = WebUtility.HtmlDecode(await navegador.GetStringAsync(new Uri("/", UriKind.Relative)));
        var lista = await navegador.GetStringAsync(new Uri("/Chamados?somenteVencidos=true", UriKind.Relative));

        var emAndamento = todos!.Count(c => c.Status is StatusChamado.Aberto or StatusChamado.EmAtendimento);
        Assert.True(emAndamento > 1, "A semente cria chamados em andamento.");
        Assert.Equal(emAndamento, vencidos!.Count);
        Assert.All(vencidos, c => Assert.True(c.Status is StatusChamado.Aberto or StatusChamado.EmAtendimento));
        Assert.Equal(emAndamento, indicadores!.Vencidos);
        Assert.Contains($"{emAndamento} chamados estão com o prazo vencido", painel, StringComparison.Ordinal);
        Assert.Contains("selo-vencido", lista, StringComparison.Ordinal);
        Assert.Contains("name=\"somenteVencidos\" value=\"true\" checked", lista, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prazos_invalidos_impedem_a_aplicacao_de_iniciar_com_a_mensagem_do_dominio()
    {
        // Crítica com 2 dias e Alta com 1 dia: o urgente esperaria mais que o menos urgente.
        await using var invalida = fabrica.WithWebHostBuilder(builder =>
            ComPrazos(builder, critica: "2.00:00:00", alta: "1.00:00:00", media: "3.00:00:00", baixa: "7.00:00:00"));

        var erro = Assert.ThrowsAny<Exception>(() => invalida.CreateClient());

        Assert.Contains("O prazo da prioridade Critica não pode ser maior", erro.ToString(), StringComparison.Ordinal);
    }

    private static void ComPrazos(IWebHostBuilder builder, string critica, string alta, string media, string baixa)
    {
        builder.UseSetting("Prazos:Critica", critica);
        builder.UseSetting("Prazos:Alta", alta);
        builder.UseSetting("Prazos:Media", media);
        builder.UseSetting("Prazos:Baixa", baixa);
    }
}
