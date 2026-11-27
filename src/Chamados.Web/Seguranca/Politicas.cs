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

    /// <summary>Meus chamados: atendentes e coordenação veem todos os chamados (Seguranca/Visibilidade.cs).</summary>
    public const string VerTodosOsChamados = "VerTodosOsChamados";

    /// <summary>Gestão de usuários: cadastrar, mudar perfil, desativar e redefinir senha. Só a coordenação.</summary>
    public const string GerenciarUsuarios = "GerenciarUsuarios";

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
            // Gestão de usuários: a mesma claim, com nome próprio. Se um dia a gestão for de outro cargo,
            // muda só esta linha.
            .AddPolicy(GerenciarUsuarios, politica => politica.RequireClaim(ClaimCargo, CargoCoordenacao))
            // Meus chamados: papel OU claim. A mesma regra de Visibilidade.VeTodosOsChamados, para
            // menus (AuthorizeView) e páginas usarem a política pelo nome.
            .AddPolicy(VerTodosOsChamados, politica => politica.RequireAssertion(
                contexto => contexto.User.VeTodosOsChamados()))
            // A API aceita cookie ou token e, sem nenhum dos dois, responde 401 (nunca redireciona).
            .AddPolicy(Api, politica => politica
                .AddAuthenticationSchemes(EsquemasDaApi)
                .RequireAuthenticatedUser());
}
