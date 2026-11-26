using System.ComponentModel.DataAnnotations;
using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Conta;

/// <summary>
/// Login com o ASP.NET Core Identity. A senha é conferida contra o hash guardado no banco;
/// depois de 5 erros seguidos a conta fica bloqueada por 5 minutos (opções no Program.cs).
/// </summary>
[AllowAnonymous]
public sealed class EntrarModel(SignInManager<IdentityUser> entrada, UserManager<IdentityUser> usuarios,
    ILogger<EntrarModel> logger) : PageModel
{
    [BindProperty]
    public EntradaInput Entrada { get; set; } = new();

    /// <summary>Página que o usuário tentou abrir antes de ser mandado ao login.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var resultado = await entrada.PasswordSignInAsync(
            Entrada.Email, Entrada.Senha, isPersistent: false, lockoutOnFailure: true);

        // Para o log, o Id do usuário, nunca o e-mail digitado.
        var usuarioId = (await usuarios.FindByEmailAsync(Entrada.Email))?.Id ?? "desconhecido";

        if (resultado.Succeeded)
        {
            Registro.LoginAceito(logger, usuarioId);

            // Só endereços deste site. Sem esta verificação, ?ReturnUrl=https://site-falso
            // levaria o usuário, recém-autenticado, para fora (redirecionamento aberto).
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
        }

        if (resultado.IsLockedOut)
        {
            Registro.ContaBloqueada(logger, usuarioId);
            ModelState.AddModelError(string.Empty,
                "Conta bloqueada temporariamente por tentativas seguidas. Tente de novo em alguns minutos.");
            return Page();
        }

        Registro.LoginRecusado(logger, usuarioId);

        // Mensagem única para e-mail inexistente e senha errada: não confirma quem tem conta.
        ModelState.AddModelError(string.Empty, "E-mail ou senha incorretos.");
        return Page();
    }

    public sealed class EntradaInput
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = string.Empty;
    }
}
