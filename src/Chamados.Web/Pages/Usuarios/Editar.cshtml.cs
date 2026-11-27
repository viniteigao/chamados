using System.ComponentModel.DataAnnotations;
using Chamados.Web.Apresentacao;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Usuarios;

/// <summary>
/// Gestão de usuários: editar uma pessoa. Uma página, três formulários (perfil, senha, situação).
/// Sem [BindProperty]: cada handler liga e valida só o formulário dele (TryUpdateModelAsync), para
/// os campos vazios de um formulário não reprovarem o envio de outro.
/// </summary>
public sealed class EditarModel(GestaoDeUsuarios gestao) : PageModel
{
    public UsuarioResumo Usuario { get; private set; } = null!;

    public PerfilInput Perfil { get; set; } = new();

    public NovaSenhaInput NovaSenha { get; set; } = new();

    /// <summary>A coordenadora está editando a si mesma: não pode tirar a coordenação nem se desativar.</summary>
    public bool EhVoceMesmo => Usuario.Id == User.IdDoUsuario();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (!await CarregarAsync(id))
        {
            return NotFound();
        }

        PreencherPerfil();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (!await CarregarAsync(id))
        {
            return NotFound();
        }

        if (!await TryUpdateModelAsync(Perfil, nameof(Perfil)))
        {
            return Page();
        }

        var resultado = await gestao.AlterarAsync(id, Perfil.Nome, Perfil.Atende, Perfil.Coordena, User.IdDoUsuario()!);
        return Concluir(resultado, "Perfil salvo. Se o acesso mudou, a pessoa precisa entrar de novo (em até 1 minuto a sessão dela é encerrada).");
    }

    public async Task<IActionResult> OnPostRedefinirSenhaAsync(string id)
    {
        if (!await CarregarAsync(id))
        {
            return NotFound();
        }

        PreencherPerfil();
        if (!await TryUpdateModelAsync(NovaSenha, nameof(NovaSenha)))
        {
            return Page();
        }

        var resultado = await gestao.RedefinirSenhaAsync(id, NovaSenha.Senha, User.IdDoUsuario()!);
        return Concluir(resultado, "Senha redefinida. Passe a nova senha por um canal seguro; as sessões abertas com a senha antiga foram encerradas.",
            campoSenha: "NovaSenha.Senha");
    }

    public async Task<IActionResult> OnPostDesativarAsync(string id)
    {
        if (!await CarregarAsync(id))
        {
            return NotFound();
        }

        PreencherPerfil();
        return Concluir(await gestao.DesativarAsync(id, User.IdDoUsuario()!),
            "Conta desativada. A pessoa não consegue mais entrar, e as sessões abertas são encerradas.");
    }

    public async Task<IActionResult> OnPostReativarAsync(string id)
    {
        if (!await CarregarAsync(id))
        {
            return NotFound();
        }

        PreencherPerfil();
        return Concluir(await gestao.ReativarAsync(id, User.IdDoUsuario()!), "Conta reativada.");
    }

    private async Task<bool> CarregarAsync(string id)
    {
        if (await gestao.ObterAsync(id) is not { } usuario)
        {
            return false;
        }

        Usuario = usuario;
        return true;
    }

    private void PreencherPerfil()
        => Perfil = new PerfilInput { Nome = Usuario.Nome, Atende = Usuario.Atende, Coordena = Usuario.Coordena };

    // Post/Redirect/Get no sucesso; no erro, a mesma página com as mensagens.
    private IActionResult Concluir(IdentityResult resultado, string sucesso, string? campoSenha = null)
    {
        if (resultado.Succeeded)
        {
            TempData["Mensagem"] = sucesso;
            return RedirectToPage(new { id = Usuario.Id });
        }

        ModelState.AdicionarErros(resultado, campoSenha: campoSenha);
        return Page();
    }

    public sealed class PerfilInput
    {
        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(GestaoDeUsuarios.TamanhoMaximoNome, ErrorMessage = "Use no máximo {1} caracteres.")]
        [Display(Name = "Nome (aparece nas telas)")]
        public string Nome { get; set; } = string.Empty;

        [Display(Name = "Atende chamados (fila de atendimento, editar e mudar status)")]
        public bool Atende { get; set; }

        [Display(Name = "Coordenação (vê todos os chamados, indicadores e gerencia usuários)")]
        public bool Coordena { get; set; }
    }

    public sealed class NovaSenhaInput
    {
        [Required(ErrorMessage = "Informe a nova senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nova senha")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repita a nova senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Senha), ErrorMessage = "As duas senhas não são iguais.")]
        [Display(Name = "Repita a nova senha")]
        public string Confirmacao { get; set; } = string.Empty;
    }
}
