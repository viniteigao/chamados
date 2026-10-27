using Chamados.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Formulário de abertura. GET mostra o formulário; POST valida, chama a camada de negócio
/// e redireciona (Post/Redirect/Get), para que F5 não reenvie o chamado.
/// </summary>
public sealed class NovoModel(ChamadoServico servico, ILogger<NovoModel> logger) : PageModel
{
    // [BindProperty] liga os campos do formulário (name="Input.Titulo"...) a esta propriedade no POST.
    [BindProperty]
    public NovoChamadoInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // 1. Validação da tela: Data Annotations do NovoChamadoInput, executadas no servidor.
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // 2. Camada de negócio: o domínio confere de novo e tem a palavra final.
            var chamado = await servico.AbrirAsync(Input.ParaComando(), cancellationToken);

            // Log estruturado: Id e prioridade, nunca o texto digitado (pode conter dados pessoais).
            logger.LogInformation("Chamado {ChamadoId} aberto com prioridade {Prioridade}", chamado.Id, chamado.Prioridade);

            // 3. Post/Redirect/Get: a mensagem sobrevive ao redirect via TempData.
            TempData["Mensagem"] = $"Chamado \"{chamado.Titulo}\" aberto.";
            return RedirectToPage("./Detalhes", new { id = chamado.Id });
        }
        catch (ArgumentException ex)
        {
            // Regra que só o domínio conhece: volta ao formulário com a mensagem dele.
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }
}
