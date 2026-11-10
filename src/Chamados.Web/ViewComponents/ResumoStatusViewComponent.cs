using Chamados.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Chamados.Web.ViewComponents;

/// <summary>
/// Contador de pendências no cabeçalho de todas as páginas.
/// Uma partial só recebe dados prontos; um View Component busca os próprios dados,
/// com injeção de dependência, e escolhe a view que vai renderizar.
/// </summary>
public sealed class ResumoStatusViewComponent(ChamadoServico servico) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var estatisticas = await servico.ObterEstatisticasAsync(HttpContext.RequestAborted);

        var resumo = new ResumoStatus(
            estatisticas.PorStatus[StatusChamado.Aberto],
            estatisticas.PorStatus[StatusChamado.EmAtendimento]);

        // Procura Pages/Shared/Components/ResumoStatus/Default.cshtml.
        return View(resumo);
    }
}

/// <summary>Modelo da view do componente: só o que ela mostra.</summary>
public sealed record ResumoStatus(int Abertos, int EmAtendimento);
