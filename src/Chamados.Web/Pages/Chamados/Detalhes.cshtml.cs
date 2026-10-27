using System.ComponentModel.DataAnnotations;
using Chamados.Domain;
using Chamados.Web.Apresentacao;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Detalhe e mudança de status. Uma página, três formulários, três handlers nomeados:
/// asp-page-handler="Iniciar" chama OnPostIniciarAsync, e assim por diante.
/// </summary>
public sealed class DetalhesModel(ChamadoServico servico) : PageModel
{
    private const string Obrigatorio = "Preencha este campo antes de confirmar.";

    public Chamado Chamado { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await servico.ObterAsync(id, cancellationToken) is not { } chamado)
        {
            return NotFound();
        }

        Chamado = chamado;
        return Page();
    }

    // Aqui o model binding preenche parâmetros do método, não uma [BindProperty]:
    // cada formulário envia um campo diferente.
    public Task<IActionResult> OnPostIniciarAsync(Guid id, [Required(ErrorMessage = Obrigatorio)] string responsavel,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, () => servico.IniciarAtendimentoAsync(id, responsavel, cancellationToken),
            "Atendimento iniciado.", cancellationToken);

    public Task<IActionResult> OnPostResolverAsync(Guid id, [Required(ErrorMessage = Obrigatorio)] string solucao,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, () => servico.ResolverAsync(id, solucao, cancellationToken),
            "Chamado resolvido.", cancellationToken);

    public Task<IActionResult> OnPostCancelarAsync(Guid id, [Required(ErrorMessage = Obrigatorio)] string motivo,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, () => servico.CancelarAsync(id, motivo, cancellationToken),
            "Chamado cancelado.", cancellationToken);

    private async Task<IActionResult> TransicionarAsync(Guid id, Func<Task<Chamado>> transicao, string sucesso,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await transicao();
                TempData["Mensagem"] = sucesso;
                return RedirectToPage(new { id });
            }
            catch (ChamadoNaoEncontradoException)
            {
                return NotFound();
            }
            catch (TransicaoInvalidaException ex)
            {
                // Ex.: o chamado foi resolvido em outra aba enquanto esta estava aberta.
                ModelState.AddModelError(string.Empty,
                    $"O chamado está {ex.Atual.Rotulo().ToLowerInvariant()} e não pode passar para " +
                    $"{ex.Pretendido.Rotulo().ToLowerInvariant()}. A página foi atualizada.");
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        // Erro: recarrega o chamado e mostra a mesma página com as mensagens.
        return await OnGetAsync(id, cancellationToken);
    }
}
