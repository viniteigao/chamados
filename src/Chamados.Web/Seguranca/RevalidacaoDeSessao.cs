using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Gestão de usuários: uma conta desativada, ou com perfil ou senha trocados, ganha um carimbo de
/// segurança novo. Estas duas classes fazem as sessões já abertas perceberem isso:
/// o cookie é revalidado pelo próprio Identity (intervalo no Program.cs), e aqui ficam a API e o Blazor.
/// </summary>
public static class RevalidacaoDeSessao
{
    /// <summary>De quanto em quanto tempo cookie e circuito Blazor conferem o carimbo de segurança.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);
}

/// <summary>
/// API: confere o carimbo de segurança a cada requisição (uma consulta ao banco). Um token emitido
/// antes da desativação ou da troca de perfil deixa de valer na hora, em vez de valer até expirar.
/// </summary>
public sealed class FiltroSessaoValida : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var entrada = http.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
            if (await entrada.ValidateSecurityStampAsync(http.User) is null)
            {
                var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<FiltroSessaoValida>();
                Registro.SessaoRevogada(logger, http.User.IdDoUsuario() ?? "desconhecido");
                return TypedResults.Problem("Esta sessão não vale mais: a conta foi desativada ou o perfil mudou. Entre de novo.",
                    statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado");
            }
        }

        return await next(context);
    }
}

/// <summary>
/// Blazor: o circuito guarda quem entrou e não volta a ler o cookie. A cada minuto este provedor
/// confere o carimbo de segurança; se mudou, o circuito passa a ver um usuário anônimo e as páginas
/// protegidas deixam de mostrar o conteúdo (é o desenho do modelo de projeto Blazor com Identity).
/// </summary>
public sealed class RevalidacaoDeSessaoBlazor(ILoggerFactory registros, IServiceScopeFactory escopos)
    : RevalidatingServerAuthenticationStateProvider(registros)
{
    protected override TimeSpan RevalidationInterval => RevalidacaoDeSessao.Intervalo;

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);

        // O provedor vive o circuito inteiro: cada conferência usa um escopo (e um DbContext) novo.
        await using var escopo = escopos.CreateAsyncScope();
        var entrada = escopo.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        return await entrada.ValidateSecurityStampAsync(authenticationState.User) is not null;
    }
}
