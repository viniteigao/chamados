using System.ComponentModel.DataAnnotations;
using Chamados.Domain;
using Chamados.Web.Apresentacao;
using Chamados.Web.Diagnostico;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Detalhe e mudança de status. Uma página, três formulários, três handlers nomeados:
/// asp-page-handler="Iniciar" chama OnPostIniciarAsync, e assim por diante.
/// Módulo 8: qualquer usuário conectado vê o chamado; só atendentes mudam o status.
/// Razor Pages não aceitam [Authorize] por handler, então a regra é conferida no código
/// (autorização imperativa), com a mesma política usada no resto do sistema.
/// </summary>
public sealed class DetalhesModel(ChamadoServico servico, IAuthorizationService autorizacao,
    ILogger<DetalhesModel> logger) : PageModel
{
    private const string Obrigatorio = "Preencha este campo antes de confirmar.";

    public Chamado Chamado { get; private set; } = null!;

    /// <summary>A página mostra os formulários de transição só quando isto é verdadeiro.</summary>
    public bool PodeAtender { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await servico.ObterAsync(id, cancellationToken) is not { } chamado)
        {
            return NotFound();
        }

        Chamado = chamado;
        PodeAtender = (await autorizacao.AuthorizeAsync(User, Politicas.Atender)).Succeeded;
        return Page();
    }

    // Aqui o model binding preenche parâmetros do método, não uma [BindProperty]:
    // cada formulário envia um campo diferente.
    public Task<IActionResult> OnPostIniciarAsync(Guid id, [Required(ErrorMessage = Obrigatorio)] string responsavel,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, "Iniciar", () => servico.IniciarAtendimentoAsync(id, responsavel, cancellationToken),
            "Atendimento iniciado.", cancellationToken);

    public Task<IActionResult> OnPostResolverAsync(Guid id, [Required(ErrorMessage = Obrigatorio)] string solucao,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, "Resolver", () => servico.ResolverAsync(id, solucao, cancellationToken),
            "Chamado resolvido.", cancellationToken);

    public Task<IActionResult> OnPostCancelarAsync(Guid id, [Required(ErrorMessage = Obrigatorio)] string motivo,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, "Cancelar", () => servico.CancelarAsync(id, motivo, cancellationToken),
            "Chamado cancelado.", cancellationToken);

    private async Task<IActionResult> TransicionarAsync(Guid id, string acao, Func<Task<Chamado>> transicao, string sucesso,
        CancellationToken cancellationToken)
    {
        // Esconder o formulário não basta: um POST pode ser montado à mão. A regra vale aqui.
        if (!(await autorizacao.AuthorizeAsync(User, Politicas.Atender)).Succeeded)
        {
            Registro.AcaoNegada(logger, acao, id, Politicas.Atender);
            return Forbid();   // logado sem permissão: o cookie leva para /Conta/AcessoNegado
        }

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
            catch (ConcorrenciaException)
            {
                // Módulo 7: o banco recusou a gravação porque outra pessoa alterou o chamado antes.
                ModelState.AddModelError(string.Empty,
                    "Outra pessoa alterou este chamado ao mesmo tempo. A página foi atualizada; tente de novo.");
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
