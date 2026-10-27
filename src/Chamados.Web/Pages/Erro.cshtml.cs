using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages;

/// <summary>
/// Destino do UseExceptionHandler("/Erro") fora de Development.
/// Mostra só um código de rastreio: o detalhe da exceção fica no log, nunca na tela.
/// </summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErroModel : PageModel
{
    public string CodigoRequisicao { get; private set; } = string.Empty;

    // OnGet e OnPost: a requisição que falhou pode ter sido um POST de formulário.
    public void OnGet() => CodigoRequisicao = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

    public void OnPost() => OnGet();
}
