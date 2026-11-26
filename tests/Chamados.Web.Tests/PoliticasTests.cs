using System.Security.Claims;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Web.Tests;

/// <summary>
/// Testes de unidade das políticas: sem servidor, sem banco. Um usuário montado à mão
/// (ClaimsPrincipal) e o serviço de autorização com as políticas do sistema.
/// </summary>
public sealed class PoliticasTests
{
    private static readonly IAuthorizationService Autorizacao = new ServiceCollection()
        .AddLogging()
        .AddPoliticasChamados().Services
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    private static ClaimsPrincipal Usuario(params Claim[] claims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "teste"), .. claims], authenticationType: "Teste"));

    [Theory]
    [InlineData(Politicas.PapelAtendente, true)]
    [InlineData("Visitante", false)]
    public async Task Atender_exige_o_papel_atendente(string papel, bool permitido)
    {
        var resultado = await Autorizacao.AuthorizeAsync(Usuario(new Claim(ClaimTypes.Role, papel)), Politicas.Atender);

        Assert.Equal(permitido, resultado.Succeeded);
    }

    [Fact]
    public async Task Ver_indicadores_exige_a_claim_de_coordenacao_e_nao_basta_ser_atendente()
    {
        var coordenadora = Usuario(new Claim(Politicas.ClaimCargo, Politicas.CargoCoordenacao));
        var atendente = Usuario(new Claim(ClaimTypes.Role, Politicas.PapelAtendente));

        Assert.True((await Autorizacao.AuthorizeAsync(coordenadora, Politicas.VerIndicadores)).Succeeded);
        Assert.False((await Autorizacao.AuthorizeAsync(atendente, Politicas.VerIndicadores)).Succeeded);
    }

    [Fact]
    public async Task Politica_de_fallback_recusa_usuario_anonimo()
    {
        var anonimo = new ClaimsPrincipal(new ClaimsIdentity());   // sem authenticationType: não autenticado
        var opcoes = new ServiceCollection().AddLogging().AddPoliticasChamados().Services
            .BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;

        var resultado = await Autorizacao.AuthorizeAsync(anonimo, resource: null, opcoes.FallbackPolicy!);

        Assert.False(resultado.Succeeded);
    }
}
