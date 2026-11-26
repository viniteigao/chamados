using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Regras de acesso do sistema, num lugar só. Páginas, componentes e endpoints citam a política
/// pelo nome; quem decide o que ela exige é este arquivo.
/// </summary>
public static class Politicas
{
    // Nomes de políticas.
    public const string Atender = "Atender";
    public const string VerIndicadores = "VerIndicadores";
    public const string Api = "Api";

    // Papel (role) e claim usados pelas políticas.
    public const string PapelAtendente = "Atendente";
    public const string ClaimCargo = "cargo";
    public const string CargoCoordenacao = "Coordenacao";

    /// <summary>Esquemas aceitos pela API: o cookie do navegador e o token do aplicativo (Módulo 10).</summary>
    public static readonly string[] EsquemasDaApi = [IdentityConstants.ApplicationScheme, IdentityConstants.BearerScheme];

    public static AuthorizationBuilder AddPoliticasChamados(this IServiceCollection services)
        => services.AddAuthorizationBuilder()
            // Política de fallback: vale para tudo que não declarou regra própria. Uma página nova
            // nasce protegida; para abrir ao público, é preciso escrever [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            // Baseada em papel: só quem está no papel Atendente.
            .AddPolicy(Atender, politica => politica.RequireRole(PapelAtendente))
            // Baseada em claim: o usuário carrega a informação "cargo = Coordenacao".
            .AddPolicy(VerIndicadores, politica => politica.RequireClaim(ClaimCargo, CargoCoordenacao))
            // A API aceita cookie ou token e, sem nenhum dos dois, responde 401 (nunca redireciona).
            .AddPolicy(Api, politica => politica
                .AddAuthenticationSchemes(EsquemasDaApi)
                .RequireAuthenticatedUser());
}
