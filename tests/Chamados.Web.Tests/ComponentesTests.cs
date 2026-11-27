using Chamados.Domain;
using Chamados.Web.Components.Fila;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chamados.Web.Tests;

/// <summary>
/// Testes de componente com o HtmlRenderer, que já vem no ASP.NET Core: renderiza o componente
/// fora do navegador e devolve o HTML. Serve para conferir o que é exibido em cada estado.
/// Para simular cliques e digitação, a ferramenta usual é a biblioteca bUnit (da comunidade).
/// </summary>
public sealed class ComponentesTests
{
    private static async Task<string> RenderizarAsync<TComponente>(Dictionary<string, object?> parametros)
        where TComponente : IComponent
    {
        await using var servicos = new ServiceCollection().BuildServiceProvider();
        await using var renderizador = new HtmlRenderer(servicos, NullLoggerFactory.Instance);
        return await renderizador.Dispatcher.InvokeAsync(async () =>
            (await renderizador.RenderComponentAsync<TComponente>(ParameterView.FromDictionary(parametros))).ToHtmlString());
    }

    private static ResumoChamado Resumo(string titulo, StatusChamado status)
        => new(Guid.NewGuid(), titulo, status, Prioridade.Alta, null, DateTimeOffset.UtcNow, 1);

    [Fact]
    public async Task Selo_mostra_o_rotulo_em_portugues()
    {
        var html = await RenderizarAsync<Selo>(new() { [nameof(Selo.Valor)] = StatusChamado.EmAtendimento });

        Assert.Contains("Em atendimento", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StatusChamado.Aberto, "Iniciar")]
    [InlineData(StatusChamado.EmAtendimento, "Resolver")]
    public async Task Cartao_oferece_a_acao_que_cabe_ao_status(StatusChamado status, string acao)
    {
        var html = await RenderizarAsync<CartaoChamado>(new() { [nameof(CartaoChamado.Chamado)] = Resumo("Projetor", status) });

        Assert.Contains($">{acao}</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cartao_codifica_html_vindo_do_titulo()
    {
        var html = await RenderizarAsync<CartaoChamado>(new()
        {
            [nameof(CartaoChamado.Chamado)] = Resumo("<img src=x onerror=alert(1)>", StatusChamado.Aberto),
        });

        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
    }

    // --- Prazos ---

    [Fact]
    public async Task Cartao_vencido_ganha_destaque_e_diz_ha_quanto_tempo_venceu()
    {
        var prazo = new PrazoDoChamado(DateTimeOffset.UtcNow.AddHours(-2), SituacaoPrazo.Vencido, TimeSpan.FromHours(-2));

        var html = await RenderizarAsync<CartaoChamado>(new()
        {
            [nameof(CartaoChamado.Chamado)] = Resumo("Porta não tranca", StatusChamado.Aberto),
            [nameof(CartaoChamado.Prazo)] = prazo,
        });
        // O renderizador codifica acentos (á vira &#xE1;): decodifica para conferir o texto lido.
        var texto = System.Net.WebUtility.HtmlDecode(html);

        Assert.Contains("cartao-fila cartao-vencido", html, StringComparison.Ordinal);
        Assert.Contains("selo-vencido", html, StringComparison.Ordinal);
        Assert.Contains("venceu há 2 h", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cartao_sem_prazo_nao_mostra_a_linha_de_prazo()
    {
        var html = await RenderizarAsync<CartaoChamado>(new() { [nameof(CartaoChamado.Chamado)] = Resumo("Projetor", StatusChamado.Aberto) });

        Assert.DoesNotContain("class=\"prazo\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"cartao-fila\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cartao_com_conversa_mostra_quantas_mensagens_com_link_para_ela()
    {
        var chamado = Resumo("Wi-Fi instável", StatusChamado.EmAtendimento) with { TotalComentarios = 3 };

        var html = await RenderizarAsync<CartaoChamado>(new() { [nameof(CartaoChamado.Chamado)] = chamado });
        var semConversa = await RenderizarAsync<CartaoChamado>(new()
        {
            [nameof(CartaoChamado.Chamado)] = Resumo("Projetor", StatusChamado.Aberto),
        });

        Assert.Contains($"href=\"/Chamados/Detalhes/{chamado.Id}#conversa\">3 mensagens</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("contador-mensagens", semConversa, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cartao_com_mensagens_novas_mostra_o_selo_com_link_para_a_primeira_delas()
    {
        var chamado = Resumo("Wi-Fi instável", StatusChamado.EmAtendimento) with { TotalComentarios = 3, ComentariosNaoLidos = 2 };

        var html = await RenderizarAsync<CartaoChamado>(new() { [nameof(CartaoChamado.Chamado)] = chamado });
        var lidas = await RenderizarAsync<CartaoChamado>(new()
        {
            [nameof(CartaoChamado.Chamado)] = chamado with { ComentariosNaoLidos = 0 },
        });

        Assert.Contains($"href=\"/Chamados/Detalhes/{chamado.Id}#novas\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"2 mensagens novas para você\">2 novas</a>", System.Net.WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
        Assert.DoesNotContain("selo-novas", lidas, StringComparison.Ordinal);
    }
}
