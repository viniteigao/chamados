namespace Chamados.Web.Seguranca;

/// <summary>
/// Cabeçalhos de segurança em todas as respostas. Cada um fecha uma porta que o navegador
/// deixaria aberta por padrão.
/// </summary>
public static class CabecalhosSeguranca
{
    public static IApplicationBuilder UseCabecalhosSeguranca(this IApplicationBuilder app, bool desenvolvimento)
    {
        // Content-Security-Policy: de onde a página pode carregar scripts, estilos, imagens e conexões.
        // Só do próprio site ('self'): um <script> injetado por XSS, inline ou de outro domínio, não roda.
        // Em Development, o Visual Studio abre um WebSocket em outra porta para o recarregamento automático.
        var conexoes = desenvolvimento ? "'self' wss://localhost:* ws://localhost:*" : "'self'";
        var csp = string.Join("; ",
            "default-src 'self'",
            "img-src 'self' data:",
            $"connect-src {conexoes}",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");

        return app.Use(async (contexto, proximo) =>
        {
            var cabecalhos = contexto.Response.Headers;
            cabecalhos.ContentSecurityPolicy = csp;
            cabecalhos.XContentTypeOptions = "nosniff";                    // não "adivinhar" tipo de arquivo
            cabecalhos["Referrer-Policy"] = "strict-origin-when-cross-origin"; // não vazar URLs para outros sites
            cabecalhos.XFrameOptions = "DENY";                              // não abrir dentro de <iframe> alheio
            await proximo(contexto);
        });
    }
}
