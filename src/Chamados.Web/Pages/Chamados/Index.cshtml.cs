using Chamados.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>Lista com filtros. O formulário de filtro usa GET: a busca fica na URL e pode ser compartilhada.</summary>
public sealed class IndexModel(ChamadoServico servico) : PageModel
{
    // SupportsGet = true: o model binding também preenche estas propriedades em requisições GET.
    [BindProperty(SupportsGet = true)]
    public StatusChamado? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public Prioridade? PrioridadeMinima { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Texto { get; set; }

    public IReadOnlyList<ResumoChamado> Chamados { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Valor fora do enum na URL (?status=Xyz) não lança exceção: vira erro no ModelState.
        if (!ModelState.IsValid)
        {
            Status = null;
            PrioridadeMinima = null;
        }

        Chamados = await servico.ListarAsync(new FiltroChamados(Status, PrioridadeMinima, Texto), cancellationToken);
    }
}
