using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chamados.Domain;

namespace Chamados.Web.Tests;

/// <summary>
/// Meus chamados: quem abriu cada chamado, quem fez cada mudança, e quem vê o quê.
/// Na semente, o bruno (sem papel) abriu 5 chamados, a carla 3 e a ana 4; quem muda status é a ana.
/// </summary>
public sealed class MeusChamadosTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    [Fact]
    public async Task Solicitante_ve_na_api_so_os_chamados_que_abriu()
    {
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        var doBruno = await ListarAsync(solicitante, "/api/chamados");
        var todos = await ListarAsync(atendente, "/api/chamados");

        Assert.NotEmpty(doBruno);
        Assert.All(doBruno, c => Assert.Equal(Acesso.NomeSolicitante, c.AbertoPorNome));
        Assert.Contains(todos, c => c.AbertoPorNome == Acesso.NomeCoordenadora);
        Assert.True(todos.Count > doBruno.Count);
    }

    [Fact]
    public async Task Chamado_de_outra_pessoa_responde_404_na_api_e_na_pagina_e_fica_no_log()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var daCarla = (await ListarAsync(atendente, "/api/chamados")).First(c => c.AbertoPorNome == Acesso.NomeCoordenadora);
        var solicitanteApi = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var solicitanteNavegador = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);

        var api = await solicitanteApi.GetAsync(new Uri($"/api/chamados/{daCarla.Id}", UriKind.Relative));
        var pagina = await solicitanteNavegador.GetAsync(new Uri($"/Chamados/Detalhes/{daCarla.Id}", UriKind.Relative));
        var paraAtendente = await atendente.GetAsync(new Uri($"/api/chamados/{daCarla.Id}", UriKind.Relative));

        // 404, e não 403: para quem não pode ver, o chamado "não existe".
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pagina.StatusCode);
        Assert.Equal(HttpStatusCode.OK, paraAtendente.StatusCode);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1009);
    }

    [Fact]
    public async Task Quem_abre_e_quem_muda_o_status_ficam_registrados_e_aparecem_no_detalhe()
    {
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var resposta = await solicitante.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Ventilador da sala 12 fazendo barulho", prioridade = "Baixa" });
        var criado = await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes);
        (await atendente.PostAsJsonAsync(new Uri($"/api/chamados/{criado!.Id}/atendimento", UriKind.Relative),
            new { })).EnsureSuccessStatusCode();

        using var detalhe = JsonDocument.Parse(
            await solicitante.GetStringAsync(new Uri($"/api/chamados/{criado.Id}", UriKind.Relative)));
        var navegador = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);
        var html = WebUtility.HtmlDecode(
            await navegador.GetStringAsync(new Uri($"/Chamados/Detalhes/{criado.Id}", UriKind.Relative)));

        Assert.Equal(Acesso.NomeSolicitante, detalhe.RootElement.GetProperty("abertoPor").GetString());
        var registro = Assert.Single(detalhe.RootElement.GetProperty("historico").EnumerateArray());
        Assert.Equal(Acesso.NomeAtendente, registro.GetProperty("por").GetString());
        Assert.Contains("Aberto por", html, StringComparison.Ordinal);
        Assert.Contains($"por {Acesso.NomeAtendente}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Solicitante_ve_painel_lista_e_menu_so_dele_e_sem_o_contador_da_fila()
    {
        var solicitante = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var painel = WebUtility.HtmlDecode(await solicitante.GetStringAsync(new Uri("/", UriKind.Relative)));
        var lista = WebUtility.HtmlDecode(await solicitante.GetStringAsync(new Uri("/Chamados", UriKind.Relative)));
        var painelDaAtendente = await atendente.GetStringAsync(new Uri("/", UriKind.Relative));

        Assert.Contains("abertos por você", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("resumo-status", painel, StringComparison.Ordinal);   // contador da fila inteira
        Assert.DoesNotContain(">Lista<", painel, StringComparison.Ordinal);
        Assert.Contains("<h1>Meus chamados</h1>", lista, StringComparison.Ordinal);
        Assert.DoesNotContain("Só os meus", lista, StringComparison.Ordinal);       // já são só os dele
        Assert.Contains("resumo-status", painelDaAtendente, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quem_ve_todos_pode_pedir_so_os_seus()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        var meus = await ListarAsync(atendente, "/api/chamados?meus=true");
        var todos = await ListarAsync(atendente, "/api/chamados");

        Assert.NotEmpty(meus);
        Assert.All(meus, c => Assert.Equal(Acesso.NomeAtendente, c.AbertoPorNome));
        Assert.True(todos.Count > meus.Count);
    }

    [Fact]
    public async Task Busca_por_responsavel_do_solicitante_so_traz_os_chamados_dele()
    {
        // Na semente, o Wi-Fi (da carla) está com o atendente Davi.
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        var doBruno = await ListarAsync(solicitante, "/api/chamados/por-responsavel?nome=Davi");
        var todos = await ListarAsync(atendente, "/api/chamados/por-responsavel?nome=Davi");

        Assert.Empty(doBruno);
        Assert.Contains(todos, c => c.Titulo == "Wi-Fi instável no bloco B");
    }

    [Fact]
    public async Task Pelas_paginas_o_formulario_e_o_detalhe_tambem_registram_quem_fez()
    {
        // O autor é opcional no serviço: este teste confere que as Razor Pages sempre o informam.
        var solicitante = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);
        var abertura = await solicitante.PostAsync(new Uri("/Chamados/Novo", UriKind.Relative),
            await FormularioAsync(solicitante, "/Chamados/Novo", new()
            {
                ["Input.Titulo"] = "Persiana da sala 8 emperrada",
                ["Input.Prioridade"] = "Baixa",
            }));
        var detalhe = abertura.Headers.Location!.OriginalString;   // Post/Redirect/Get: vai para o detalhe

        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);
        var inicio = await atendente.PostAsync(new Uri($"{detalhe}?handler=Iniciar", UriKind.Relative),
            await FormularioAsync(atendente, detalhe, new() { ["responsavelId"] = await fabrica.IdDoUsuarioAsync(Acesso.Atendente) }));

        using var json = JsonDocument.Parse(await (await fabrica.ClienteComTokenAsync(Acesso.Atendente))
            .GetStringAsync(new Uri($"/api/chamados/{detalhe.Split('/')[^1]}", UriKind.Relative)));

        Assert.Equal(HttpStatusCode.Redirect, abertura.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, inicio.StatusCode);
        Assert.Equal(Acesso.NomeSolicitante, json.RootElement.GetProperty("abertoPor").GetString());
        Assert.Equal(Acesso.NomeAtendente,
            Assert.Single(json.RootElement.GetProperty("historico").EnumerateArray()).GetProperty("por").GetString());
    }

    private static async Task<List<ResumoChamado>> ListarAsync(HttpClient cliente, string rota)
        => (await cliente.GetFromJsonAsync<List<ResumoChamado>>(new Uri(rota, UriKind.Relative), Json.Opcoes))!;

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
