using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chamados.Domain;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Chamados.Web.Tests;

/// <summary>
/// Testes de integração: requisições HTTP de verdade contra a aplicação em memória.
/// Conferem as regras de acesso e as defesas do Módulo 8 do jeito que um atacante as veria.
/// </summary>
public sealed class SegurancaTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    private const string Injecao =
        "x' UNION SELECT \"Id\", \"Email\" || ' ' || \"PasswordHash\", NULL, 1, 1, 'Aberto', NULL, NULL, 0, NULL FROM \"AspNetUsers\" --";

    [Fact]
    public async Task Pagina_sem_login_redireciona_para_o_login()
    {
        var resposta = await fabrica.ClienteAnonimo().GetAsync(new Uri("/atendimento", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Conta/Entrar", resposta.Headers.Location!.AbsolutePath);
        Assert.Contains("ReturnUrl=%2Fatendimento", resposta.Headers.Location.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_sem_login_responde_401_e_nao_redireciona()
    {
        var resposta = await fabrica.ClienteAnonimo().GetAsync(new Uri("/api/chamados", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Solicitante_nao_inicia_atendimento_e_atendente_inicia()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var abertos = await atendente.GetFromJsonAsync<List<ResumoChamado>>(
            new Uri("/api/chamados?status=Aberto", UriKind.Relative), Json.Opcoes);
        var rota = new Uri($"/api/chamados/{abertos![0].Id}/atendimento", UriKind.Relative);

        var negado = await solicitante.PostAsJsonAsync(rota, new { responsavel = "Bruno" });
        var aceito = await atendente.PostAsJsonAsync(rota, new { responsavel = "Ana" });

        Assert.Equal(HttpStatusCode.Forbidden, negado.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aceito.StatusCode);
    }

    [Fact]
    public async Task Busca_por_responsavel_nao_executa_sql_digitado()
    {
        // Qualquer usuário conectado pode buscar; por isso o ataque parte do solicitante.
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);

        var corpo = await solicitante.GetStringAsync(
            new Uri($"/api/chamados/por-responsavel?nome={Uri.EscapeDataString(Injecao)}", UriKind.Relative));

        Assert.Equal("[]", corpo);
        Assert.DoesNotContain("AQAAAA", corpo, StringComparison.Ordinal);   // início de todo hash do Identity
    }

    [Fact]
    public async Task Coordenadora_ve_indicadores_lidos_pela_api_com_o_proprio_login()
    {
        var coordenadora = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);

        var html = await coordenadora.GetStringAsync(new Uri("/indicadores", UriKind.Relative));

        // "Por status" só é renderizado quando a API respondeu; numa falha, a página mostra o aviso.
        Assert.Contains("Por status", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Atendente_sem_cargo_de_coordenacao_e_levada_ao_acesso_negado()
    {
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var resposta = await atendente.GetAsync(new Uri("/indicadores", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Conta/AcessoNegado", resposta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Login_com_endereco_de_retorno_externo_volta_para_o_inicio()
    {
        var cliente = fabrica.ClienteAnonimo();
        var formulario = await Acesso.FormularioDeLoginAsync(cliente, Acesso.Solicitante, FabricaChamados.Senha,
            "https://site-falso.example/login");

        var resposta = await cliente.PostAsync(new Uri("/Conta/Entrar", UriKind.Relative), formulario);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/", resposta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Descricao_com_html_aparece_como_texto()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var criado = await atendente.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Teste de XSS na descrição", prioridade = "Baixa", descricao = "<script>alert('xss')</script>" });
        var chamado = await criado.Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes);
        var navegador = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var html = await navegador.GetStringAsync(new Uri($"/Chamados/Detalhes/{chamado!.Id}", UriKind.Relative));

        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Respostas_trazem_os_cabecalhos_de_seguranca()
    {
        var resposta = await fabrica.ClienteAnonimo().GetAsync(new Uri("/Conta/Entrar", UriKind.Relative));

        var csp = string.Join(';', resposta.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(resposta.Headers.GetValues("X-Content-Type-Options")));
    }

    // --- Melhorias ---

    [Fact]
    public async Task Muitas_tentativas_de_token_respondem_429_em_json_e_ficam_no_log()
    {
        await using var restrita = ComLimiteDeLogin(3);
        var cliente = restrita.ClienteAnonimo();

        var respostas = new List<HttpResponseMessage>();
        for (var i = 0; i < 4; i++)
        {
            // E-mail que não existe: nenhuma conta real fica bloqueada pelo teste.
            respostas.Add(await cliente.PostAsJsonAsync(new Uri("/api/conta/token", UriKind.Relative),
                new { email = "ninguem@chamados.local", senha = "qualquer-coisa" }));
        }

        Assert.All(respostas.Take(3), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        var recusada = respostas[3];
        Assert.Equal(HttpStatusCode.TooManyRequests, recusada.StatusCode);
        Assert.Equal("application/problem+json", recusada.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(recusada.Headers.RetryAfter);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1008);
    }

    [Fact]
    public async Task Muitas_tentativas_no_formulario_mostram_a_pagina_429()
    {
        await using var restrita = ComLimiteDeLogin(3);
        var cliente = restrita.ClienteAnonimo();

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 4; i++)
        {
            // Abrir a página (GET) não conta; só o envio da senha (POST).
            var formulario = await Acesso.FormularioDeLoginAsync(cliente, "ninguem@chamados.local", "qualquer-coisa", "/");
            ultima = await cliente.PostAsync(new Uri("/Conta/Entrar", UriKind.Relative), formulario);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);
        Assert.Contains("Muitas tentativas", await ultima.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prioridade_inexistente_responde_400_so_com_a_mensagem_do_dominio()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);

        var resposta = await atendente.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Prioridade fora da lista", prioridade = 99 });
        using var problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        // Antes: "Prioridade inexistente. (Parameter 'prioridade')\r\nActual value was 99."
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("Prioridade inexistente.", problema.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Responsavel_longo_demais_responde_400_com_o_erro_no_campo_sem_mudar_o_chamado()
    {
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var abertos = await atendente.GetFromJsonAsync<List<ResumoChamado>>(
            new Uri("/api/chamados?status=Aberto", UriKind.Relative), Json.Opcoes);
        var chamado = abertos![^1];

        var resposta = await atendente.PostAsJsonAsync(new Uri($"/api/chamados/{chamado.Id}/atendimento", UriKind.Relative),
            new { responsavel = new string('a', Chamado.TamanhoMaximoResponsavel + 1) });
        using var problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var depois = await atendente.GetFromJsonAsync<ResumoChamado>(
            new Uri($"/api/chamados/{chamado.Id}", UriKind.Relative), Json.Opcoes);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(problema.RootElement.GetProperty("errors").EnumerateObject(),
            campo => campo.Name.Equals("Responsavel", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(chamado.Versao, depois!.Versao);
        Assert.Equal(StatusChamado.Aberto, depois.Status);
    }

    // Uma cópia da aplicação (mesmo banco da classe) com um limite baixo de tentativas de login.
    private WebApplicationFactory<Program> ComLimiteDeLogin(int tentativasPorMinuto)
        => fabrica.WithWebHostBuilder(builder => builder.UseSetting(
            $"{LimiteDeLoginOptions.Secao}:{nameof(LimiteDeLoginOptions.TentativasPorMinuto)}",
            tentativasPorMinuto.ToString(CultureInfo.InvariantCulture)));
}
