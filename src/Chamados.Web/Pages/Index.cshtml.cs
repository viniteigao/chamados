using Chamados.Domain;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages;

/// <summary>
/// Painel inicial: a primeira página que conversa com a camada de negócio.
/// Meus chamados: quem não vê todos os chamados vê os números só dos que abriu.
/// </summary>
public sealed class IndexModel(ChamadoServico servico) : PageModel
{
    public EstatisticasChamados Estatisticas { get; private set; } = null!;

    /// <summary>Prazos: chamados em andamento com o prazo vencido agora.</summary>
    public int Vencidos { get; private set; }

    /// <summary>Meus chamados: verdadeiro quando os números são só dos chamados do usuário.</summary>
    public bool SoOsMeus { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var dono = User.RestricaoDeDono();
        SoOsMeus = dono is not null;
        Estatisticas = await servico.ObterEstatisticasAsync(dono, cancellationToken);
        Vencidos = await servico.ContarVencidosAsync(dono, cancellationToken);
    }
}
