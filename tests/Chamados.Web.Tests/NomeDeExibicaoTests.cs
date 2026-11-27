using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Chamados.Domain;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Web.Tests;

/// <summary>
/// Nome de exibição: o login continua pelo e-mail, e o que aparece é o nome (claim "nome").
/// </summary>
public sealed class NomeDeExibicaoTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    [Fact]
    public async Task Login_continua_pelo_email_e_o_cabecalho_mostra_o_nome()
    {
        // ClienteConectadoAsync entra pelo formulário com o e-mail (e confere o redirecionamento).
        var solicitante = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);
        var atendente = await fabrica.ClienteConectadoAsync(Acesso.Atendente);

        var pagina = await solicitante.GetStringAsync(new Uri("/", UriKind.Relative));
        var fila = await atendente.GetStringAsync(new Uri("/atendimento", UriKind.Relative));   // layout do Blazor

        // O nome no texto, com link para "Meu perfil"; o e-mail de login só no title, para quem passar o mouse.
        Assert.Contains($"href=\"/Perfil\" title=\"{Acesso.Solicitante}\">{Acesso.NomeSolicitante}</a>", pagina, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Perfil\" title=\"{Acesso.Atendente}\">{Acesso.NomeAtendente}</a>", fila, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pelo_token_da_api_quem_abre_e_quem_atende_ficam_gravados_com_o_nome()
    {
        var solicitante = await fabrica.ClienteComTokenAsync(Acesso.Solicitante);
        var atendente = await fabrica.ClienteComTokenAsync(Acesso.Atendente);
        var criado = await (await solicitante.PostAsJsonAsync(new Uri("/api/chamados", UriKind.Relative),
            new { titulo = "Cortina da sala 7 rasgada", prioridade = "Baixa" })).Content.ReadFromJsonAsync<ResumoChamado>(Json.Opcoes);
        (await atendente.PostAsJsonAsync(new Uri($"/api/chamados/{criado!.Id}/atendimento", UriKind.Relative),
            new { responsavel = "Manutenção" })).EnsureSuccessStatusCode();

        using var detalhe = JsonDocument.Parse(
            await atendente.GetStringAsync(new Uri($"/api/chamados/{criado.Id}", UriKind.Relative)));

        // A API mostra nomes, e nenhum e-mail: o login de uma pessoa não fica exposto para as outras.
        Assert.Equal(Acesso.NomeSolicitante, criado.AbertoPorNome);
        Assert.Equal(Acesso.NomeSolicitante, detalhe.RootElement.GetProperty("abertoPor").GetString());
        Assert.Equal(Acesso.NomeAtendente,
            Assert.Single(detalhe.RootElement.GetProperty("historico").EnumerateArray()).GetProperty("por").GetString());
        Assert.DoesNotContain("@chamados.local", detalhe.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Semente_acrescenta_o_nome_a_usuario_criado_antes_dele_sem_duplicar_claims()
    {
        // Um banco anterior a este recurso: a carla existe, com a claim "cargo", mas sem "nome".
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var carla = (await usuarios.FindByEmailAsync(Acesso.Coordenadora))!;
            var nome = (await usuarios.GetClaimsAsync(carla)).Single(c => c.Type == NomeDeExibicao.Claim);
            Assert.True((await usuarios.RemoveClaimAsync(carla, nome)).Succeeded);
        }

        // Duas execuções, como dois inícios da aplicação: a primeira repõe o nome, a segunda não repete.
        var configuracao = fabrica.Services.GetRequiredService<IConfiguration>();
        await SementeUsuarios.PopularAsync(fabrica.Services, configuracao);
        await SementeUsuarios.PopularAsync(fabrica.Services, configuracao);

        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var claims = await usuarios.GetClaimsAsync((await usuarios.FindByEmailAsync(Acesso.Coordenadora))!);

            Assert.Equal(Acesso.NomeCoordenadora, Assert.Single(claims, c => c.Type == NomeDeExibicao.Claim).Value);
            Assert.Single(claims, c => c.Type == Politicas.ClaimCargo);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Sem_nome_vale_o_login(string? nome)
    {
        // Ex.: uma sessão aberta antes deste recurso, cujo cookie ainda não tem a claim.
        Claim[] claims = nome is null ? [] : [new Claim(NomeDeExibicao.Claim, nome)];
        var usuario = Usuario(claims);

        Assert.Equal(Acesso.Solicitante, usuario.NomeParaExibir());
        Assert.Equal(new Autor("id-bruno", Acesso.Solicitante), usuario.ComoAutor());
    }

    [Fact]
    public void Com_nome_o_autor_das_acoes_e_o_nome()
    {
        var usuario = Usuario([new Claim(NomeDeExibicao.Claim, " Bruno ")]);

        Assert.Equal(Acesso.NomeSolicitante, usuario.NomeParaExibir());
        Assert.Equal(new Autor("id-bruno", Acesso.NomeSolicitante), usuario.ComoAutor());
    }

    // Um usuário como o Identity monta no login: Id, nome de login (o e-mail) e as claims dele.
    private static ClaimsPrincipal Usuario(Claim[] claims)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "id-bruno"), new Claim(ClaimTypes.Name, Acesso.Solicitante), .. claims],
            authenticationType: "Teste"));
}
