using System.ComponentModel.DataAnnotations;
using Chamados.Web.Apresentacao;
using Chamados.Web.Diagnostico;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages;

/// <summary>
/// Gestão de usuários: "Meu perfil". Qualquer pessoa conectada troca o próprio nome de exibição e a
/// própria senha. Fica fora da pasta /Conta, que é aberta a anônimos (login), e por isso vale aqui a
/// política de fallback: só quem entrou.
/// </summary>
public sealed class PerfilModel(GestaoDeUsuarios gestao, UserManager<IdentityUser> usuarios,
    SignInManager<IdentityUser> entrada, ILogger<PerfilModel> logger) : PageModel
{
    public NomeInput Nome { get; set; } = new();

    public TrocaDeSenhaInput Senha { get; set; } = new();

    public void OnGet() => Nome.Nome = User.NomeParaExibir() ?? string.Empty;

    public async Task<IActionResult> OnPostNomeAsync()
    {
        if (!await TryUpdateModelAsync(Nome, nameof(Nome)))
        {
            return Page();
        }

        if (await usuarios.GetUserAsync(User) is not { } usuario)
        {
            return Challenge();
        }

        var resultado = await gestao.AlterarNomeAsync(usuario.Id, Nome.Nome);
        if (!resultado.Succeeded)
        {
            ModelState.AdicionarErros(resultado);
            return Page();
        }

        // O cookie guarda as claims de quando a pessoa entrou: renova agora para o nome novo aparecer já.
        await entrada.RefreshSignInAsync(usuario);
        TempData["Mensagem"] = "Nome alterado. Ele vale para o que você fizer daqui em diante.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSenhaAsync()
    {
        Nome.Nome = User.NomeParaExibir() ?? string.Empty;
        if (!await TryUpdateModelAsync(Senha, nameof(Senha)))
        {
            return Page();
        }

        if (await usuarios.GetUserAsync(User) is not { } usuario)
        {
            return Challenge();
        }

        var resultado = await usuarios.ChangePasswordAsync(usuario, Senha.Atual, Senha.Nova);
        if (!resultado.Succeeded)
        {
            ModelState.AdicionarErros(resultado, campoSenha: "Senha.Nova", campoSenhaAtual: "Senha.Atual");
            return Page();
        }

        Registro.SenhaAlterada(logger, usuario.Id);

        // A troca de senha muda o carimbo de segurança: as outras sessões (outro navegador, tokens) caem.
        // Esta é renovada aqui, para a pessoa continuar conectada.
        await entrada.RefreshSignInAsync(usuario);
        TempData["Mensagem"] = "Senha alterada. As outras sessões abertas com a senha antiga foram encerradas.";
        return RedirectToPage();
    }

    public sealed class NomeInput
    {
        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(GestaoDeUsuarios.TamanhoMaximoNome, ErrorMessage = "Use no máximo {1} caracteres.")]
        [Display(Name = "Nome (aparece nas telas)")]
        public string Nome { get; set; } = string.Empty;
    }

    public sealed class TrocaDeSenhaInput
    {
        [Required(ErrorMessage = "Informe a senha atual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha atual")]
        public string Atual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a nova senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nova senha")]
        public string Nova { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repita a nova senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Nova), ErrorMessage = "As duas senhas não são iguais.")]
        [Display(Name = "Repita a nova senha")]
        public string Confirmacao { get; set; } = string.Empty;
    }
}
