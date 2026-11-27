using System.ComponentModel.DataAnnotations;
using Chamados.Web.Apresentacao;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Usuarios;

/// <summary>
/// Gestão de usuários: cadastro. A coordenação define nome, e-mail (o login), o que a pessoa pode fazer
/// e uma senha inicial, que passa a ela por um canal seguro. O sistema não envia e-mail.
/// </summary>
public sealed class NovoModel(GestaoDeUsuarios gestao) : PageModel
{
    [BindProperty]
    public NovoUsuarioInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var resultado = await gestao.CriarAsync(
            new NovoUsuario(Input.Nome, Input.Email, Input.Senha, Input.Atende, Input.Coordena), User.IdDoUsuario()!);
        if (!resultado.Succeeded)
        {
            ModelState.AdicionarErros(resultado, campoEmail: "Input.Email", campoSenha: "Input.Senha");
            return Page();
        }

        TempData["Mensagem"] = $"Cadastro de {Input.Nome.Trim()} concluído. Passe a senha inicial por um canal seguro; " +
            "a pessoa pode trocá-la em Meu perfil.";
        return RedirectToPage("./Index");
    }

    public sealed class NovoUsuarioInput
    {
        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(GestaoDeUsuarios.TamanhoMaximoNome, ErrorMessage = "Use no máximo {1} caracteres.")]
        [Display(Name = "Nome (aparece nas telas)")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(256, ErrorMessage = "Use no máximo {1} caracteres.")]
        [Display(Name = "E-mail (é o login)")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha inicial.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha inicial")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repita a senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Senha), ErrorMessage = "As duas senhas não são iguais.")]
        [Display(Name = "Repita a senha")]
        public string ConfirmacaoSenha { get; set; } = string.Empty;

        [Display(Name = "Atende chamados (fila de atendimento, editar e mudar status)")]
        public bool Atende { get; set; }

        [Display(Name = "Coordenação (vê todos os chamados, indicadores e gerencia usuários)")]
        public bool Coordena { get; set; }
    }
}
