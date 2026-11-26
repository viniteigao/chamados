using System.Net;
using System.Net.Http.Json;
using Chamados.Domain;

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
}
