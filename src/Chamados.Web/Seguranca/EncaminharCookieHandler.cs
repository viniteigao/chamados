using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Seguranca;

/// <summary>
/// A página /indicadores chama a própria API pelo servidor. Essa chamada sai do servidor, não do
/// navegador, e por isso não leva o cookie de login: a API responderia 401. Este handler copia
/// só o cookie de autenticação da requisição atual para a chamada à API.
/// Vale para páginas estáticas (com HttpContext). Em componentes interativos não há requisição
/// HTTP em andamento; lá o certo é chamar o serviço diretamente.
/// </summary>
public sealed class EncaminharCookieHandler(IHttpContextAccessor acessor) : DelegatingHandler
{
    public const string NomeDoCookie = "Chamados.Auth";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (acessor.HttpContext?.Request.Cookies[NomeDoCookie] is { } valor)
        {
            // Só este cookie, e só para o endereço da própria API configurado em Api:UrlBase.
            request.Headers.Add("Cookie", $"{NomeDoCookie}={valor}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
