using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chamados.Domain;
using Chamados.Web.Api;
using Chamados.Web.Seguranca;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Web.Tests;

/// <summary>
/// Responsável atendente: quem atende é uma pessoa da lista de atendentes ativos. Na semente, a ana e
/// o davi atendem; o ar-condicionado da sala 204 e o Wi-Fi do bloco B estão com o davi.
/// </summary>
public sealed partial class ResponsavelAtendenteTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    private const string Davi = "davi@chamados.local";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntifalsificacao();

    [Fact]
    public async Task Pela_api_sem_indicar_quem_atende_e_quem_chama_e_indicando_e_o_atendente_escolhido()
    {
        var ana = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var davi = await fabrica.ClienteComTokenAsync(Davi);
        var semIndicar = await NovoChamadoAsync("Tomada da sala 2 sem energia");
        var indicando = await NovoChamadoAsync("Tomada da sala 3 sem energia");

        var comAna = await IniciarAsync(ana, semIndicar.Id, new { });
        var comDavi = await IniciarAsync(ana, indicando.Id, new { responsavelId = await fabrica.IdDoUsuarioAsync(Davi) });
        var atribuidosAoDavi = await ListarAsync(davi, "/api/chamados?atribuidosAMim=true");

        Assert.Equal(Acesso.NomeAtendente, comAna.Responsavel);
        Assert.Equal("Davi", comDavi.Responsavel);
        Assert.Contains(atribuidosAoDavi, c => c.Id == indicando.Id);
        Assert.DoesNotContain(atribuidosAoDavi, c => c.Id == semIndicar.Id);
        Assert.All(atribuidosAoDavi, c => Assert.Equal("Davi", c.Responsavel));
    }

    [Fact]
    public async Task Nao_atribui_a_solicitante_a_conta_desativada_nem_a_id_inventado()
    {
        var ana = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var chamado = await NovoChamadoAsync("Janela emperrada na sala 4");
        var desativado = await AtendenteDesativadoAsync();

        foreach (var id in new[] { await fabrica.IdDoUsuarioAsync(Acesso.Solicitante), desativado, "id-inventado" })
        {
            var resposta = await ana.PostAsJsonAsync(Rota($"/api/chamados/{chamado.Id}/atendimento"), new { responsavelId = id });
            using var problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.True(problema.RootElement.GetProperty("errors").TryGetProperty("responsavelId", out _));
        }

        var depois = await ana.GetFromJsonAsync<ResumoChamado>(Rota($"/api/chamados/{chamado.Id}"), Json.Opcoes);
        Assert.Equal(StatusChamado.Aberto, depois!.Status);
    }

    [Fact]
    public async Task Reatribuir_pela_pagina_registra_no_historico_e_muda_os_meus_atendimentos()
    {
        var chamado = await NovoChamadoAsync("Ar-condicionado da sala 9 pingando");
        await IniciarAsync(await fabrica.ClienteComTokenAsync(Acesso.Atendente), chamado.Id, new { });
        var ana = await fabrica.ClienteConectadoAsync(Acesso.Atendente);
        var detalhe = $"/Chamados/Detalhes/{chamado.Id}";

        var resposta = await ana.PostAsync(Rota($"{detalhe}?handler=Reatribuir"),
            await FormularioAsync(ana, detalhe, new() { ["responsavelId"] = await fabrica.IdDoUsuarioAsync(Davi) }));
        var html = WebUtility.HtmlDecode(await ana.GetStringAsync(Rota(detalhe)));
        using var json = JsonDocument.Parse(await (await fabrica.ClienteComTokenAsync(Acesso.Atendente))
            .GetStringAsync(Rota($"/api/chamados/{chamado.Id}")));
        var daAna = await ListarAsync(await fabrica.ClienteComTokenAsync(Acesso.Atendente), "/api/chamados?atribuidosAMim=true");
        var doDavi = await ListarAsync(await fabrica.ClienteComTokenAsync(Davi), "/api/chamados?atribuidosAMim=true");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("Responsável alterado", html, StringComparison.Ordinal);
        Assert.Contains($"· {Acesso.NomeAtendente} → Davi", html, StringComparison.Ordinal);
        Assert.Contains($"· por {Acesso.NomeAtendente}", html, StringComparison.Ordinal);
        var edicao = Assert.Single(json.RootElement.GetProperty("edicoes").EnumerateArray());
        Assert.Equal("Davi", edicao.GetProperty("responsavelNovo").GetString());
        Assert.DoesNotContain(daAna, c => c.Id == chamado.Id);
        Assert.Contains(doDavi, c => c.Id == chamado.Id);
    }

    [Fact]
    public async Task Pela_pagina_quem_esta_conectado_vem_marcado_e_so_atendentes_sao_aceitos()
    {
        var chamado = await NovoChamadoAsync("Mesa solta na sala 10");
        var ana = await fabrica.ClienteConectadoAsync(Acesso.Atendente);
        var detalhe = $"/Chamados/Detalhes/{chamado.Id}";
        var anaId = await fabrica.IdDoUsuarioAsync(Acesso.Atendente);

        var tela = WebUtility.HtmlDecode(await ana.GetStringAsync(Rota(detalhe)));
        var recusado = WebUtility.HtmlDecode(await (await ana.PostAsync(Rota($"{detalhe}?handler=Iniciar"),
            await FormularioAsync(ana, detalhe, new() { ["responsavelId"] = await fabrica.IdDoUsuarioAsync(Acesso.Solicitante) })))
            .Content.ReadAsStringAsync());
        var aceito = await ana.PostAsync(Rota($"{detalhe}?handler=Iniciar"),
            await FormularioAsync(ana, detalhe, new() { ["responsavelId"] = await fabrica.IdDoUsuarioAsync(Davi) }));
        var depois = WebUtility.HtmlDecode(await ana.GetStringAsync(Rota(detalhe)));

        Assert.Contains($"<option value=\"{anaId}\" selected=\"selected\">{Acesso.NomeAtendente} (você)</option>", tela, StringComparison.Ordinal);
        Assert.DoesNotContain($">{Acesso.NomeSolicitante}</option>", tela, StringComparison.Ordinal);   // só atendentes na lista
        Assert.Contains("Escolha um atendente da lista.", recusado, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, aceito.StatusCode);
        Assert.Contains("· Atribuído a Davi</span>", depois, StringComparison.Ordinal);
        Assert.Contains($"· por {Acesso.NomeAtendente}</span>", depois, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reatribuir_chamado_aberto_responde_409()
    {
        var ana = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var chamado = await NovoChamadoAsync("Cortina caída na sala 11");

        var resposta = await ana.PostAsJsonAsync(Rota($"/api/chamados/{chamado.Id}/responsavel"),
            new { responsavelId = await fabrica.IdDoUsuarioAsync(Davi) });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Lista_de_atendentes_e_so_para_quem_atende_e_so_com_atendentes_ativos()
    {
        var ana = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var bruno = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var desativado = await AtendenteDesativadoAsync();

        var lista = await ana.GetFromJsonAsync<List<AtendenteResposta>>(Rota("/api/atendentes"), Json.Opcoes);
        var paraBruno = await bruno.GetAsync(Rota("/api/atendentes"));

        Assert.Contains(lista!, a => a.Nome == Acesso.NomeAtendente);
        Assert.Contains(lista!, a => a.Nome == "Davi");
        Assert.DoesNotContain(lista!, a => a.Nome is Acesso.NomeSolicitante or Acesso.NomeCoordenadora);
        Assert.DoesNotContain(lista!, a => a.Id == desativado);
        Assert.Equal(HttpStatusCode.Forbidden, paraBruno.StatusCode);
    }

    [Fact]
    public async Task Coordenacao_ve_a_carga_de_cada_atendente()
    {
        var carla = await fabrica.ClienteComTokenAsync(Acesso.Coordenadora);
        var bruno = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var navegador = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);

        var carga = await carla.GetFromJsonAsync<List<CargaResposta>>(Rota("/api/chamados/carga"), Json.Opcoes);
        var paraBruno = await bruno.GetAsync(Rota("/api/chamados/carga"));
        var indicadores = WebUtility.HtmlDecode(await navegador.GetStringAsync(Rota("/indicadores")));

        // Na semente, o davi está com dois chamados em atendimento (ar-condicionado e Wi-Fi).
        Assert.True(carga!.Single(c => c.Atendente == "Davi").EmAtendimento >= 2);
        Assert.Equal(HttpStatusCode.Forbidden, paraBruno.StatusCode);
        Assert.Contains("Carga por atendente", indicadores, StringComparison.Ordinal);
        Assert.Contains("<td>Davi</td>", indicadores, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lista_atribuidos_a_mim_mostra_meus_atendimentos()
    {
        var davi = await fabrica.ClienteConectadoAsync(Davi);
        var bruno = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);

        var lista = WebUtility.HtmlDecode(await davi.GetStringAsync(Rota("/Chamados?atribuidosAMim=true")));
        var listaDoBruno = await bruno.GetStringAsync(Rota("/Chamados"));

        Assert.Contains("<h1>Meus atendimentos</h1>", lista, StringComparison.Ordinal);
        Assert.Contains("Wi-Fi instável no bloco B", lista, StringComparison.Ordinal);
        Assert.DoesNotContain("Atribuídos a mim", listaDoBruno, StringComparison.Ordinal);   // só para quem atende
    }

    private static Uri Rota(string caminho) => new(caminho, UriKind.Relative);

    // Um chamado novo do bruno, aberto pela API.
    private async Task<ResumoChamado> NovoChamadoAsync(string titulo)
    {
        var bruno = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var resposta = await bruno.PostAsJsonAsync(Rota("/api/chamados"), new { titulo, prioridade = "Baixa" });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes))!;
    }

    private static async Task<ResumoChamado> IniciarAsync(HttpClient cliente, Guid id, object corpo)
    {
        var resposta = await cliente.PostAsJsonAsync(Rota($"/api/chamados/{id}/atendimento"), corpo);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes))!;
    }

    private static async Task<List<ResumoChamado>> ListarAsync(HttpClient cliente, string rota)
        => (await cliente.GetFromJsonAsync<List<ResumoChamado>>(Rota(rota), Json.Opcoes))!;

    // Um atendente cadastrado e desativado pela gestão de usuários; devolve o Id.
    private async Task<string> AtendenteDesativadoAsync()
    {
        var email = $"ex.{Guid.NewGuid():N}@chamados.local";
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var gestao = escopo.ServiceProvider.GetRequiredService<GestaoDeUsuarios>();
        Assert.True((await gestao.CriarAsync(new NovoUsuario("Ex-atendente", email, FabricaChamados.Senha, Atende: true, Coordena: false),
            "teste")).Succeeded);
        var id = await fabrica.IdDoUsuarioAsync(email);
        Assert.True((await gestao.DesativarAsync(id, "teste")).Succeeded);
        return id;
    }

    // Formulário de uma página com o token antifalsificação tirado dela, como o navegador faz.
    private static async Task<FormUrlEncodedContent> FormularioAsync(HttpClient cliente, string pagina,
        Dictionary<string, string> campos)
    {
        var html = await cliente.GetStringAsync(Rota(pagina));
        campos["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenAntifalsificacao().Match(html).Groups[1].Value);
        return new FormUrlEncodedContent(campos);
    }
}
