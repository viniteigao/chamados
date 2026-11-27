using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chamados.Domain;
using Chamados.Web.Api;

namespace Chamados.Web.Tests;

/// <summary>
/// Mensagens não lidas, pela aplicação inteira: o selo na lista, o aviso no cabeçalho, as marcas na
/// conversa, o filtro, a API e a fila Blazor. Os testes desta classe dividem o banco: cada um cria os
/// próprios chamados e confere números relativos (antes e depois), não totais.
/// </summary>
public sealed partial class MensagensNaoLidasTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    private const string Davi = "davi@chamados.local";

    [GeneratedRegex(@"Mensage(?:m|ns) novas? em (\d+) chamados?")]
    private static partial Regex AvisoDoCabecalho();

    [Fact]
    public async Task Resposta_aparece_como_nova_para_quem_abriu_ate_ele_abrir_a_conversa()
    {
        var chamado = await NovoChamadoAsync("Tomada da sala 7 soltando faísca");
        var lista = $"/Chamados?Texto={Uri.EscapeDataString("sala 7")}";
        var detalhe = $"/Chamados/Detalhes/{chamado.Id}";
        var bruno = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);
        var avisoAntes = Aviso(await bruno.GetStringAsync(Rota(lista)));

        await EnviarAsync(await fabrica.ClienteComTokenAsync(Acesso.Atendente), chamado.Id, "Desligue o disjuntor da sala, por favor.");
        var listaComNova = await bruno.GetStringAsync(Rota(lista));
        var primeiraVisita = WebUtility.HtmlDecode(await bruno.GetStringAsync(Rota(detalhe)));
        var listaDepois = await bruno.GetStringAsync(Rota(lista));
        var segundaVisita = await bruno.GetStringAsync(Rota(detalhe));

        // Antes de abrir: o selo na linha (com link para a primeira nova) e um chamado a mais no aviso.
        Assert.Equal(avisoAntes + 1, Aviso(listaComNova));
        Assert.Contains($"href=\"{detalhe}#novas\"", listaComNova, StringComparison.Ordinal);
        Assert.Contains(">1 nova</a>", listaComNova, StringComparison.Ordinal);

        // Na conversa: a mensagem marcada como nova, com a âncora, e o resumo acima dela.
        Assert.Contains("1 mensagem nova desde a sua última visita", primeiraVisita, StringComparison.Ordinal);
        Assert.Contains("class=\"mensagem nova\" id=\"novas\"", primeiraVisita, StringComparison.Ordinal);
        Assert.Contains("<span class=\"marca-nova\">nova</span>", primeiraVisita, StringComparison.Ordinal);

        // Abrir a conversa é ler: o selo e a marca somem, e o aviso volta ao que era.
        Assert.Equal(avisoAntes, Aviso(listaDepois));
        Assert.DoesNotContain($"{detalhe}#novas", listaDepois, StringComparison.Ordinal);
        Assert.DoesNotContain("marca-nova", segundaVisita, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mensagens_proprias_nao_contam_e_cada_pessoa_tem_a_sua_leitura()
    {
        var chamado = await NovoChamadoAsync("Janela da sala 8 emperrada");
        var bruno = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var ana = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var davi = await fabrica.ClienteComTokenAsync(Davi);
        await EnviarAsync(bruno, chamado.Id, "Não abre de jeito nenhum.");
        (await ana.PostAsJsonAsync(Rota($"/api/chamados/{chamado.Id}/atendimento"),
            new { responsavelId = await fabrica.IdDoUsuarioAsync(Davi) })).EnsureSuccessStatusCode();

        // A do Bruno não conta para ele; o Davi atende e acompanha; a Ana vê o chamado, mas não acompanha.
        Assert.Equal(0, await NaoLidasAsync(bruno, chamado.Id));
        Assert.Equal(1, await NaoLidasAsync(davi, chamado.Id));
        Assert.Equal(0, await NaoLidasAsync(ana, chamado.Id));

        // Quem escreve passa a acompanhar e leu até a própria mensagem.
        await EnviarAsync(ana, chamado.Id, "O Davi passa aí à tarde.");
        Assert.Equal(0, await NaoLidasAsync(ana, chamado.Id));
        Assert.Equal(2, await NaoLidasAsync(davi, chamado.Id));
        Assert.Equal(1, await NaoLidasAsync(bruno, chamado.Id));

        // O Davi abre a conversa no navegador: a leitura é dele, a do Bruno não muda.
        var daviNavegador = await fabrica.ClienteConectadoAsync(Davi);
        await daviNavegador.GetStringAsync(Rota($"/Chamados/Detalhes/{chamado.Id}"));
        Assert.Equal(0, await NaoLidasAsync(davi, chamado.Id));
        Assert.Equal(1, await NaoLidasAsync(bruno, chamado.Id));
    }

    [Fact]
    public async Task Filtro_com_mensagens_novas_na_lista_e_na_api()
    {
        var chamado = await NovoChamadoAsync("Persiana da sala 6 caiu");
        await EnviarAsync(await fabrica.ClienteComTokenAsync(Acesso.Atendente), chamado.Id, "Vou levar a escada.");
        var bruno = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);
        var brunoApi = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var filtrada = "/Chamados?comNaoLidas=true&Texto=Persiana";

        var pagina = await bruno.GetStringAsync(Rota(filtrada));
        var api = await ListarAsync(brunoApi, "/api/chamados?comNaoLidas=true");
        var marcar = await brunoApi.PostAsync(Rota($"/api/chamados/{chamado.Id}/comentarios/lidos"), null);
        var paginaDepois = WebUtility.HtmlDecode(await bruno.GetStringAsync(Rota(filtrada)));
        var apiDepois = await ListarAsync(brunoApi, "/api/chamados?comNaoLidas=true");

        Assert.Contains("<h1>Mensagens novas</h1>", pagina, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Chamados/Detalhes/{chamado.Id}\"", pagina, StringComparison.Ordinal);
        Assert.Contains("name=\"comNaoLidas\" value=\"true\" checked=\"checked\"", pagina, StringComparison.Ordinal);
        Assert.Contains(api, c => c.Id == chamado.Id);
        Assert.All(api, c => Assert.True(c.ComentariosNaoLidos > 0));
        Assert.Equal(HttpStatusCode.NoContent, marcar.StatusCode);
        Assert.Contains("Nenhuma mensagem nova para você com esses filtros.", paginaDepois, StringComparison.Ordinal);
        Assert.DoesNotContain(apiDepois, c => c.Id == chamado.Id);
    }

    [Fact]
    public async Task Pela_api_a_conversa_diz_o_que_e_novo_e_so_o_post_marca_como_lida()
    {
        var chamado = await NovoChamadoAsync("Bebedouro da sala 2 vazando");
        var ana = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        await EnviarAsync(ana, chamado.Id, "Feche o registro embaixo dele.");
        var bruno = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var daCarla = (await ListarAsync(ana, "/api/chamados")).First(c => c.AbertoPorNome == Acesso.NomeCoordenadora);

        var primeira = await ConversaAsync(bruno, chamado.Id);
        var segunda = await ConversaAsync(bruno, chamado.Id);   // ler pela API não marca nada
        using var detalhe = JsonDocument.Parse(await bruno.GetStringAsync(Rota($"/api/chamados/{chamado.Id}")));
        var marcar = await bruno.PostAsync(Rota($"/api/chamados/{chamado.Id}/comentarios/lidos"), null);
        var depois = await ConversaAsync(bruno, chamado.Id);
        var alheio = await bruno.PostAsync(Rota($"/api/chamados/{daCarla.Id}/comentarios/lidos"), null);

        Assert.True(Assert.Single(primeira).Nova);
        Assert.True(Assert.Single(segunda).Nova);
        Assert.True(Assert.Single(detalhe.RootElement.GetProperty("comentarios").EnumerateArray()).GetProperty("nova").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, marcar.StatusCode);
        Assert.False(Assert.Single(depois).Nova);
        Assert.Equal(0, await NaoLidasAsync(bruno, chamado.Id));
        Assert.Equal(HttpStatusCode.NotFound, alheio.StatusCode);   // chamado de outra pessoa, como no resto da conversa
    }

    [Fact]
    public async Task Cabecalho_e_fila_das_paginas_blazor_tambem_avisam()
    {
        var chamado = await NovoChamadoAsync("Lâmpada da sala 3 piscando");
        await EnviarAsync(await fabrica.ClienteComTokenAsync(Acesso.Atendente), chamado.Id, "Passo aí depois do almoço.");
        await EnviarAsync(await fabrica.ClienteComTokenAsync(Acesso.Solicitante), chamado.Id, "Obrigado!");
        var ana = await fabrica.ClienteConectadoAsync(Acesso.Atendente);
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);

        var fila = await ana.GetStringAsync(Rota("/atendimento"));
        var paginaDaCarla = await carla.GetStringAsync(Rota("/"));

        // A fila é pré-renderizada no servidor: o cartão já sai com o selo, e o cabeçalho com o aviso.
        Assert.Contains("class=\"aviso-mensagens\" href=\"/Chamados?comNaoLidas=true\"", fila, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Chamados/Detalhes/{chamado.Id}#novas\"", fila, StringComparison.Ordinal);

        // A Carla não tem mensagem nova (na semente, a última da conversa dela é dela): sem aviso.
        Assert.DoesNotContain("aviso-mensagens", paginaDaCarla, StringComparison.Ordinal);
    }

    private static Uri Rota(string caminho) => new(caminho, UriKind.Relative);

    // O número do aviso do cabeçalho; zero quando ele não aparece.
    private static int Aviso(string html)
        => AvisoDoCabecalho().Match(html) is { Success: true } aviso ? int.Parse(aviso.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

    // Um chamado novo do bruno (solicitante), aberto pela API.
    private async Task<ResumoChamado> NovoChamadoAsync(string titulo)
    {
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var resposta = await solicitante.PostAsJsonAsync(Rota("/api/chamados"), new { titulo, prioridade = "Baixa" });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes))!;
    }

    private static async Task EnviarAsync(HttpClient cliente, Guid id, string texto)
        => (await cliente.PostAsJsonAsync(Rota($"/api/chamados/{id}/comentarios"), new { texto })).EnsureSuccessStatusCode();

    private static async Task<List<ResumoChamado>> ListarAsync(HttpClient cliente, string caminho)
        => (await cliente.GetFromJsonAsync<List<ResumoChamado>>(Rota(caminho), Json.Opcoes))!;

    private static async Task<int?> NaoLidasAsync(HttpClient cliente, Guid id)
        => (await ListarAsync(cliente, "/api/chamados")).Single(c => c.Id == id).ComentariosNaoLidos;

    private static async Task<List<ComentarioResposta>> ConversaAsync(HttpClient cliente, Guid id)
        => (await cliente.GetFromJsonAsync<List<ComentarioResposta>>(Rota($"/api/chamados/{id}/comentarios"), Json.Opcoes))!;
}
