using System.Security.Claims;
using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Conta;

/// <summary>
/// Logout só por POST, com token antifalsificação: um link ou imagem em outro site
/// não consegue deslogar ninguém (CSRF).
/// </summary>
public sealed class SairModel(SignInManager<IdentityUser> entrada, ILogger<SairModel> logger) : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "desconhecido";
        await entrada.SignOutAsync();
        Registro.Logout(logger, usuarioId);
        return RedirectToPage("/Conta/Entrar");
    }
}
