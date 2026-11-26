using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Chamados.Web.Tests;

/// <summary>Atalhos para entrar no sistema pelos dois caminhos reais: formulário e token.</summary>
public static partial class Acesso
{
    public const string Atendente = "ana@chamados.local";
    public const string Solicitante = "bruno@chamados.local";
    public const string Coordenadora = "carla@chamados.local";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntifalsificacao();

    /// <summary>Cliente sem redirecionamento automático, para os testes verem o 302.</summary>
    public static HttpClient ClienteAnonimo(this FabricaChamados fabrica)
        => fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Faz login pelo formulário, como no navegador: GET, token antifalsificação, POST.</summary>
    public static async Task<HttpClient> ClienteConectadoAsync(this FabricaChamados fabrica, string email)
    {
        var cliente = fabrica.ClienteAnonimo();
        var resposta = await cliente.PostAsync(new Uri("/Conta/Entrar", UriKind.Relative),
            await FormularioDeLoginAsync(cliente, email, FabricaChamados.Senha, "/"));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        return cliente;
    }

    public static async Task<FormUrlEncodedContent> FormularioDeLoginAsync(
        HttpClient cliente, string email, string senha, string returnUrl)
    {
        var pagina = await cliente.GetStringAsync(new Uri("/Conta/Entrar", UriKind.Relative));
        var token = TokenAntifalsificacao().Match(pagina).Groups[1].Value;
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Entrada.Email"] = email,
            ["Entrada.Senha"] = senha,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
        });
    }

    /// <summary>Pede um token à API e devolve um cliente que o envia em toda requisição.</summary>
    public static async Task<HttpClient> ClienteComTokenAsync(this FabricaChamados fabrica, string email)
    {
        var cliente = fabrica.ClienteAnonimo();
        var resposta = await cliente.PostAsJsonAsync(new Uri("/api/conta/token", UriKind.Relative),
            new { email, senha = FabricaChamados.Senha });
        resposta.EnsureSuccessStatusCode();
        var token = await resposta.Content.ReadFromJsonAsync<RespostaToken>();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token!.AccessToken);
        return cliente;
    }

    private sealed record RespostaToken(string AccessToken);
}
