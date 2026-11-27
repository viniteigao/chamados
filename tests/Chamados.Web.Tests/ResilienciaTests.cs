using System.Net;
using System.Text.Json;

namespace Chamados.Web.Tests;

/// <summary>
/// Melhorias: como o sistema responde quando o banco falha, em ambiente Production.
/// Os dois primeiros testes reproduzem os bugs encontrados na análise do projeto.
/// </summary>
public sealed class ResilienciaTests(FabricaProducao fabrica) : IClassFixture<FabricaProducao>
{
    [Fact]
    public async Task Erro_interno_na_api_responde_problem_details_em_json_e_nao_html()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        var resposta = await atendente.GetAsync(new Uri("/api/chamados", UriKind.Relative));
        var corpo = await resposta.Content.ReadAsStringAsync();

        // Antes da correção: 500 com a página HTML "Algo deu errado".
        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        using var problema = JsonDocument.Parse(corpo);
        Assert.Equal(500, problema.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Erro interno", problema.RootElement.GetProperty("title").GetString());
        Assert.True(problema.RootElement.TryGetProperty("traceId", out _), "O cliente precisa do traceId para achar o log.");
        Assert.DoesNotContain("Falha simulada", corpo, StringComparison.Ordinal);   // nenhum detalhe interno
    }

    [Fact]
    public async Task Erro_numa_pagina_mostra_a_pagina_de_erro_mesmo_com_o_banco_falhando()
    {
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var resposta = await atendente.GetAsync(new Uri("/Chamados", UriKind.Relative));
        var html = await resposta.Content.ReadAsStringAsync();

        // Antes da correção: 500 em branco, porque a própria página de erro consultava o banco.
        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("text/html", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Algo deu errado", html, StringComparison.Ordinal);
        Assert.Contains($">{Acesso.NomeAtendente}</a>", html, StringComparison.Ordinal);   // o cabeçalho continua, sem o contador
    }

    [Fact]
    public async Task Pagina_abre_mesmo_quando_o_contador_do_cabecalho_falha()
    {
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        // O formulário de abertura não consulta o banco; só o contador do cabeçalho consultaria.
        var resposta = await atendente.GetAsync(new Uri("/Chamados/Novo", UriKind.Relative));
        var html = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Abrir chamado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("resumo-status", html, StringComparison.Ordinal);
    }
}
