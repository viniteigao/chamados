using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Chamados.Web.Seguranca;

/// <summary>Quantas tentativas de login cada origem (endereço IP) pode fazer por minuto.</summary>
public sealed class LimiteDeLoginOptions
{
    public const string Secao = "LimiteDeLogin";

    [Range(1, 1000)]
    public int TentativasPorMinuto { get; set; } = 10;
}

/// <summary>
/// Melhorias: limite de tentativas de login por origem, no formulário e no endpoint de token.
/// O bloqueio do Identity (5 erros, 5 minutos) protege cada conta; sem limite por origem, um
/// anônimo podia testar senhas em volume e bloquear contas em série, em milissegundos.
/// Usa o limitador que vem no ASP.NET Core (System.Threading.RateLimiting): nenhum pacote novo.
/// </summary>
public static class LimiteDeLogin
{
    public const string Politica = "Login";

    public static IServiceCollection AddLimiteDeLogin(this IServiceCollection services)
    {
        services.AddOptions<LimiteDeLoginOptions>()
            .BindConfiguration(LimiteDeLoginOptions.Secao)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opcoes.AddPolicy(Politica, contexto =>
            {
                // Abrir a página de login não conta: só o envio da senha (POST).
                if (!HttpMethods.IsPost(contexto.Request.Method))
                {
                    return RateLimitPartition.GetNoLimiter("leitura");
                }

                // Uma janela de um minuto por endereço IP. Atrás de um proxy reverso, todos chegariam
                // com o IP do proxy: nesse caso, configure UseForwardedHeaders antes deste limite.
                var limite = contexto.RequestServices.GetRequiredService<IOptions<LimiteDeLoginOptions>>().Value;
                var origem = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecida";
                return RateLimitPartition.GetFixedWindowLimiter(origem, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.TentativasPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });

            opcoes.OnRejected = async (rejeicao, cancellationToken) =>
            {
                var http = rejeicao.HttpContext;
                if (rejeicao.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    http.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                // Para o log, só o caminho: nem o e-mail tentado, nem o endereço IP.
                var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LimiteDeLogin).FullName!);
                Registro.LimiteDeLoginAtingido(logger, http.Request.Path.Value ?? "/");

                // API: Problem Details em JSON. Páginas: corpo vazio, e o UseStatusCodePagesWithReExecute
                // mostra /Erro/429 com o layout do site.
                if (http.Request.Path.StartsWithSegments("/api"))
                {
                    await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = http,
                        ProblemDetails =
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Muitas tentativas",
                            Detail = "Tentativas de login demais a partir desta origem. Aguarde um minuto e tente de novo.",
                        },
                    });
                }
            };
        });
    }
}
