using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chamados.Domain;
using Chamados.Web.Api;

namespace Chamados.Web.Tests;

/// <summary>
/// Comentários: a conversa do chamado, pela página e pela API. Na semente, o chamado do
/// ar-condicionado da sala 204 (da carla) já começa com três mensagens.
/// </summary>
public sealed class ComentariosTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public async Task Solicitante_escreve_pela_pagina_e_a_mensagem_aparece_com_data_e_hora()
    {
        var chamado = await NovoChamadoAsync("Tomada da sala 5 com mau contato");
        var detalhe = $"/Chamados/Detalhes/{chamado.Id}";
        var solicitante = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);

        var envio = await solicitante.PostAsync(new Uri($"{detalhe}?handler=Comentar", UriKind.Relative),
            await FormularioAsync(solicitante, detalhe, new() { ["texto"] = "Faísca quando liga o carregador." }));
        var html = WebUtility.HtmlDecode(await solicitante.GetStringAsync(new Uri(detalhe, UriKind.Relative)));
        var mensagem = Assert.Single(await ConversaAsync(Acesso.Atendente, chamado.Id));
        var local = mensagem.Em.ToLocalTime();

        // Post/Redirect/Get, de volta à conversa.
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Equal($"{detalhe}#conversa", envio.Headers.Location!.OriginalString);
        Assert.Equal(Acesso.NomeSolicitante, mensagem.Autor);
        Assert.Contains("Faísca quando liga o carregador.", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Você</strong>", html, StringComparison.Ordinal);

        // Data e hora de cada mensagem: legível ("26/11/2026 às 14:32") e em ISO 8601 no atributo datetime.
        Assert.Contains(local.ToString("dd/MM/yyyy 'às' HH:mm", PtBr), html, StringComparison.Ordinal);
        Assert.Contains($"datetime=\"{local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)}",
            html, StringComparison.Ordinal);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 2007);
    }

    [Fact]
    public async Task Atendente_ve_quem_escreveu_e_responde_na_mesma_conversa()
    {
        var chamado = await NovoChamadoAsync("Janela da sala 9 não fecha");
        await EnviarAsync(await fabrica.ClienteComTokenAsync(Acesso.Solicitante), chamado.Id, "Está chovendo dentro da sala.");
        var detalhe = $"/Chamados/Detalhes/{chamado.Id}";
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var resposta = await atendente.PostAsync(new Uri($"{detalhe}?handler=Comentar", UriKind.Relative),
            await FormularioAsync(atendente, detalhe, new() { ["texto"] = "Vou levar uma lona agora." }));
        var html = WebUtility.HtmlDecode(await atendente.GetStringAsync(new Uri(detalhe, UriKind.Relative)));
        var conversa = await ConversaAsync(Acesso.Solicitante, chamado.Id);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal([Acesso.NomeSolicitante, Acesso.NomeAtendente], conversa.Select(m => m.Autor));
        Assert.Contains($"<strong>{Acesso.NomeSolicitante}</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"papel\">solicitante</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"mensagem minha\"", html, StringComparison.Ordinal);   // a da própria atendente
    }

    [Fact]
    public async Task Mensagem_em_chamado_de_outra_pessoa_responde_404_e_nao_entra_na_conversa()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var daCarla = (await atendente.GetFromJsonAsync<List<ResumoChamado>>(
            new Uri("/api/chamados", UriKind.Relative), Json.Opcoes))!.First(c => c.AbertoPorNome == Acesso.NomeCoordenadora);
        var antes = (await ConversaAsync(Acesso.Atendente, daCarla.Id)).Count;
        var solicitanteApi = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var solicitanteNavegador = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);

        var escrita = await solicitanteApi.PostAsJsonAsync(
            new Uri($"/api/chamados/{daCarla.Id}/comentarios", UriKind.Relative), new { texto = "Oi" });
        var leitura = await solicitanteApi.GetAsync(new Uri($"/api/chamados/{daCarla.Id}/comentarios", UriKind.Relative));

        // O token antifalsificação vale para o usuário, não para uma página: um POST montado à mão.
        var pagina = await solicitanteNavegador.PostAsync(
            new Uri($"/Chamados/Detalhes/{daCarla.Id}?handler=Comentar", UriKind.Relative),
            await FormularioAsync(solicitanteNavegador, "/Chamados/Novo", new() { ["texto"] = "Oi" }));

        Assert.Equal(HttpStatusCode.NotFound, escrita.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, leitura.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pagina.StatusCode);
        Assert.Equal(antes, (await ConversaAsync(Acesso.Atendente, daCarla.Id)).Count);
    }

    [Fact]
    public async Task Pela_api_autor_e_hora_vem_do_servidor_e_mensagem_vazia_ou_longa_responde_400()
    {
        var chamado = await NovoChamadoAsync("Bebedouro do 3º andar vazando");
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var rota = new Uri($"/api/chamados/{chamado.Id}/comentarios", UriKind.Relative);
        var antes = DateTimeOffset.UtcNow;

        // "em" e "autor" no corpo são ignorados: quem escreve é o dono do token, e a hora é a do servidor.
        var criada = await atendente.PostAsJsonAsync(rota,
            new { texto = "  Chego em 10 minutos.  ", em = "2000-01-01T00:00:00Z", autor = "outra pessoa" });
        var mensagem = await criada.Content.ReadFromJsonAsync<ComentarioResposta>(Json.Opcoes);
        var vazia = await atendente.PostAsJsonAsync(rota, new { texto = "   " });
        var longa = await atendente.PostAsJsonAsync(rota, new { texto = new string('a', Chamado.TamanhoMaximoComentario + 1) });
        using var problema = JsonDocument.Parse(await longa.Content.ReadAsStringAsync());
        using var detalhe = JsonDocument.Parse(
            await atendente.GetStringAsync(new Uri($"/api/chamados/{chamado.Id}", UriKind.Relative)));

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        Assert.Equal(rota.OriginalString, criada.Headers.Location!.OriginalString);
        Assert.Equal("Chego em 10 minutos.", mensagem!.Texto);
        Assert.Equal(Acesso.NomeAtendente, mensagem.Autor);
        Assert.InRange(mensagem.Em, antes, DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.BadRequest, vazia.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longa.StatusCode);
        Assert.Contains(problema.RootElement.GetProperty("errors").EnumerateObject(),
            campo => campo.Name.Equals("Texto", StringComparison.OrdinalIgnoreCase));
        var unica = Assert.Single(detalhe.RootElement.GetProperty("comentarios").EnumerateArray());
        Assert.Equal(Acesso.NomeAtendente, unica.GetProperty("autor").GetString());
    }

    [Fact]
    public async Task Mensagem_com_html_aparece_como_texto_e_nao_executa()
    {
        var chamado = await NovoChamadoAsync("Monitor piscando na sala 3");
        await EnviarAsync(await fabrica.ClienteComTokenAsync(Acesso.Solicitante), chamado.Id,
            "<script>alert('x')</script>");
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var html = await atendente.GetStringAsync(new Uri($"/Chamados/Detalhes/{chamado.Id}", UriKind.Relative));

        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_e_api_mostram_quantas_mensagens_cada_chamado_tem()
    {
        var atendenteApi = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var resumo = Assert.Single((await atendenteApi.GetFromJsonAsync<List<ResumoChamado>>(
            new Uri("/api/chamados?texto=sala 204", UriKind.Relative), Json.Opcoes))!);
        var lista = await atendente.GetStringAsync(new Uri("/Chamados?Texto=sala%20204", UriKind.Relative));

        Assert.Equal(3, resumo.TotalComentarios);
        Assert.Contains($"href=\"/Chamados/Detalhes/{resumo.Id}#conversa\">3 mensagens</a>", lista, StringComparison.Ordinal);
    }

    // Um chamado novo do bruno (solicitante), aberto pela API.
    private async Task<ResumoChamado> NovoChamadoAsync(string titulo)
    {
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var resposta = await solicitante.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo, prioridade = "Baixa" });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes))!;
    }

    private static async Task EnviarAsync(HttpClient cliente, Guid id, string texto)
        => (await cliente.PostAsJsonAsync(new Uri($"/api/chamados/{id}/comentarios", UriKind.Relative), new { texto }))
            .EnsureSuccessStatusCode();

    private async Task<List<ComentarioResposta>> ConversaAsync(string email, Guid id)
        => (await (await fabrica.ClienteComTokenAsync(email)).GetFromJsonAsync<List<ComentarioResposta>>(
            new Uri($"/api/chamados/{id}/comentarios", UriKind.Relative), Json.Opcoes))!;

    // Formulário de uma página com o token antifalsificação tirado dela, como o navegador faz.
    private static async Task<FormUrlEncodedContent> FormularioAsync(HttpClient cliente, string pagina,
        Dictionary<string, string> campos)
    {
        var html = await cliente.GetStringAsync(new Uri(pagina, UriKind.Relative));
        var token = System.Text.RegularExpressions.Regex.Match(html,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        campos["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return new FormUrlEncodedContent(campos);
    }
}
