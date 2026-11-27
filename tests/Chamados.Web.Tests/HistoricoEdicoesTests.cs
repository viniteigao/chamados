using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chamados.Domain;

namespace Chamados.Web.Tests;

/// <summary>
/// Histórico das edições: quem editou título, prioridade ou descrição, e quando, pela página e pela API.
/// </summary>
public sealed partial class HistoricoEdicoesTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntifalsificacao();

    [GeneratedRegex("name=\"Versao\" value=\"(\\d+)\"")]
    private static partial Regex VersaoLida();

    [Fact]
    public async Task Edicao_pela_pagina_aparece_no_historico_com_o_que_mudou_e_quem_editou()
    {
        var chamado = await NovoChamadoAsync("Lâmpada piscando na sala 3", "Baixa");
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);
        var edicao = $"/Chamados/Editar/{chamado.Id}";
        var formulario = await atendente.GetStringAsync(new Uri(edicao, UriKind.Relative));

        var resposta = await atendente.PostAsync(new Uri(edicao, UriKind.Relative), new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Titulo"] = "Lâmpada piscando na sala 3 (corredor)",
                ["Input.Prioridade"] = "Alta",
                ["Input.Descricao"] = "",
                ["Versao"] = VersaoLida().Match(formulario).Groups[1].Value,
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenAntifalsificacao().Match(formulario).Groups[1].Value),
            }));
        var html = WebUtility.HtmlDecode(await atendente.GetStringAsync(
            new Uri($"/Chamados/Detalhes/{chamado.Id}", UriKind.Relative)));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("Dados editados", html, StringComparison.Ordinal);
        Assert.Contains("· prioridade Baixa → Alta", html, StringComparison.Ordinal);
        Assert.Contains("· título “Lâmpada piscando na sala 3” → “Lâmpada piscando na sala 3 (corredor)”", html, StringComparison.Ordinal);
        Assert.DoesNotContain("descrição alterada", html, StringComparison.Ordinal);
        Assert.Contains($"· por {Acesso.NomeAtendente}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edicao_pela_api_registra_quem_editou_pelo_token_e_nao_pelo_corpo()
    {
        var chamado = await NovoChamadoAsync("Torneira pingando no 2º andar", "Media");
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        // "por" no corpo não faz parte do comando de edição: é ignorado.
        var resposta = await atendente.PutAsJsonAsync(new Uri($"/api/chamados/{chamado.Id}", UriKind.Relative), new
        {
            titulo = chamado.Titulo,
            prioridade = "Critica",
            descricao = "Já tem água no chão",
            versaoEsperada = chamado.Versao,
            por = "Outra pessoa",
        });
        using var detalhe = JsonDocument.Parse(
            await atendente.GetStringAsync(new Uri($"/api/chamados/{chamado.Id}", UriKind.Relative)));

        resposta.EnsureSuccessStatusCode();
        var edicao = Assert.Single(detalhe.RootElement.GetProperty("edicoes").EnumerateArray());
        Assert.Equal(Acesso.NomeAtendente, edicao.GetProperty("por").GetString());
        Assert.Equal("Media", edicao.GetProperty("prioridadeAnterior").GetString());
        Assert.Equal("Critica", edicao.GetProperty("prioridadeNova").GetString());
        Assert.Equal(JsonValueKind.Null, edicao.GetProperty("tituloNovo").ValueKind);   // o título não mudou
        Assert.True(edicao.GetProperty("descricaoAlterada").GetBoolean());
        Assert.True(edicao.GetProperty("em").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Chamado_sem_edicoes_tem_a_lista_vazia_na_api()
    {
        var chamado = await NovoChamadoAsync("Mesa bamba na sala 6", "Baixa");
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        using var detalhe = JsonDocument.Parse(
            await atendente.GetStringAsync(new Uri($"/api/chamados/{chamado.Id}", UriKind.Relative)));

        Assert.Empty(detalhe.RootElement.GetProperty("edicoes").EnumerateArray());
    }

    // Um chamado novo do bruno (solicitante), aberto pela API.
    private async Task<ResumoChamado> NovoChamadoAsync(string titulo, string prioridade)
    {
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var resposta = await solicitante.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative), new { titulo, prioridade });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes))!;
    }
}
