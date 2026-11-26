using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Api;

/// <summary>
/// Emissão de token para clientes que não são navegador: o arquivo .http hoje e o aplicativo
/// MAUI no Módulo 10. Mesmo login, mesma senha, mesmo bloqueio por tentativas do formulário.
/// Não usamos MapIdentityApi: ele publicaria também cadastro aberto (/register), que este
/// sistema não tem.
/// </summary>
public static class EndpointsConta
{
    public static IEndpointRouteBuilder MapApiConta(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/conta/token", async (PedidoToken pedido, SignInManager<IdentityUser> entrada) =>
        {
            // Com o esquema de token, o SignIn escreve a resposta: { tokenType, accessToken, expiresIn, refreshToken }.
            entrada.AuthenticationScheme = IdentityConstants.BearerScheme;
            var resultado = await entrada.PasswordSignInAsync(pedido.Email, pedido.Senha,
                isPersistent: false, lockoutOnFailure: true);

            return resultado.Succeeded
                ? Results.Empty
                : TypedResults.Problem("E-mail ou senha incorretos, ou conta bloqueada.",
                    statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado");
        })
        .AllowAnonymous()
        .WithTags("Conta")
        .WithSummary("Troca e-mail e senha por um token de acesso (1 hora)");

        return app;
    }
}

public sealed record PedidoToken(
    [Required, EmailAddress] string Email,
    [Required] string Senha);
