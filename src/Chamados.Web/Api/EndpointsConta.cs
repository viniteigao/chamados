using System.ComponentModel.DataAnnotations;
using Chamados.Web.Diagnostico;
using Chamados.Web.Seguranca;
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
        app.MapPost("/api/conta/token", async (PedidoToken pedido, SignInManager<IdentityUser> entrada,
            UserManager<IdentityUser> usuarios, ILoggerFactory registros) =>
        {
            // Com o esquema de token, o SignIn escreve a resposta: { tokenType, accessToken, expiresIn, refreshToken }.
            entrada.AuthenticationScheme = IdentityConstants.BearerScheme;
            var resultado = await entrada.PasswordSignInAsync(pedido.Email, pedido.Senha,
                isPersistent: false, lockoutOnFailure: true);

            // Melhorias: os mesmos eventos de log do formulário (1001 a 1003). Antes, uma tentativa
            // de senha pela API só aparecia como "respondeu 401". Para o log, o Id; nunca o e-mail.
            var logger = registros.CreateLogger(typeof(EndpointsConta).FullName!);
            var usuarioId = (await usuarios.FindByEmailAsync(pedido.Email))?.Id ?? "desconhecido";

            if (resultado.Succeeded)
            {
                Registro.LoginAceito(logger, usuarioId);
                return Results.Empty;
            }

            if (resultado.IsLockedOut)
            {
                Registro.ContaBloqueada(logger, usuarioId);
            }
            else
            {
                Registro.LoginRecusado(logger, usuarioId);
            }

            return TypedResults.Problem("E-mail ou senha incorretos, ou conta bloqueada.",
                statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado");
        })
        .AllowAnonymous()
        .RequireRateLimiting(LimiteDeLogin.Politica)   // Melhorias: tentativas por minuto, por origem
        .WithTags("Conta")
        .WithSummary("Troca e-mail e senha por um token de acesso (1 hora)");

        return app;
    }
}

public sealed record PedidoToken(
    [Required, EmailAddress] string Email,
    [Required] string Senha);
