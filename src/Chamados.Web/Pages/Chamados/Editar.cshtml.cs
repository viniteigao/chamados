using Chamados.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Edição com concorrência otimista. O formulário leva escondida a versão lida no GET;
/// se outra pessoa alterou o chamado até o POST, o serviço recusa e a tela explica.
/// </summary>
public sealed class EditarModel(ChamadoServico servico, ILogger<EditarModel> logger) : PageModel
{
    [BindProperty]
    public ChamadoInput Input { get; set; } = new();

    [BindProperty]
    public int Versao { get; set; }

    /// <summary>Preenchido só quando há conflito: o que está gravado agora.</summary>
    public Chamado? VersaoAtual { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await servico.ObterAsync(id, cancellationToken) is not { } chamado)
        {
            return NotFound();
        }

        if (chamado.EstaEncerrado)
        {
            TempData["Mensagem"] = "Chamados resolvidos ou cancelados não podem ser editados.";
            return RedirectToPage("./Detalhes", new { id });
        }

        Input = ChamadoInput.De(chamado);
        Versao = chamado.Versao;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await servico.EditarAsync(id, Input.ParaEdicao(Versao), cancellationToken);

            logger.LogInformation("Chamado {ChamadoId} editado (versão lida {Versao})", id, Versao);
            TempData["Mensagem"] = "Alterações salvas.";
            return RedirectToPage("./Detalhes", new { id });
        }
        catch (ConcorrenciaException ex)
        {
            VersaoAtual = await servico.ObterAsync(id, cancellationToken);
            ModelState.AddModelError(string.Empty,
                "Outra pessoa alterou este chamado enquanto você editava. Compare com os valores atuais, " +
                "ajuste o que precisar e salve de novo.");

            // O campo oculto precisa sair com a versão nova. Sem o Remove, o Tag Helper
            // reexibiria o valor enviado (o antigo), que está guardado no ModelState.
            ModelState.Remove(nameof(Versao));
            Versao = ex.VersaoAtual;
            return Page();
        }
        catch (ChamadoEncerradoException)
        {
            TempData["Mensagem"] = "O chamado foi encerrado enquanto você editava. As alterações não foram salvas.";
            return RedirectToPage("./Detalhes", new { id });
        }
        catch (ChamadoNaoEncontradoException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }
}
