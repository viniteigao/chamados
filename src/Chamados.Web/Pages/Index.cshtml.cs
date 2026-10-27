using Chamados.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages;

/// <summary>Painel inicial: a primeira página que conversa com a camada de negócio.</summary>
public sealed class IndexModel(ChamadoServico servico) : PageModel
{
    public EstatisticasChamados Estatisticas { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Estatisticas = await servico.ObterEstatisticasAsync(cancellationToken);
    }
}
