using Chamados.Domain;
using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.Mvc;

namespace Chamados.Web.ViewComponents;

/// <summary>
/// Contador de pendências no cabeçalho de todas as páginas.
/// Uma partial só recebe dados prontos; um View Component busca os próprios dados,
/// com injeção de dependência, e escolhe a view que vai renderizar.
/// </summary>
public sealed class ResumoStatusViewComponent(ChamadoServico servico, ILogger<ResumoStatusViewComponent> logger)
    : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        EstatisticasChamados estatisticas;
        try
        {
            // Totais da fila inteira: o _Layout só mostra o contador para quem vê todos os chamados.
            estatisticas = await servico.ObterEstatisticasAsync(cancellationToken: HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Melhorias: o contador é informação de apoio, como no EstadoFila do Blazor.
            // Numa falha, o cabeçalho sai sem ele e a página continua; o motivo vai para o log.
            Registro.FalhaAoAtualizarTotais(logger, ex);
            return Content(string.Empty);
        }

        var resumo = new ResumoStatus(
            estatisticas.PorStatus[StatusChamado.Aberto],
            estatisticas.PorStatus[StatusChamado.EmAtendimento]);

        // Procura Pages/Shared/Components/ResumoStatus/Default.cshtml.
        return View(resumo);
    }
}

/// <summary>Modelo da view do componente: só o que ela mostra.</summary>
public sealed record ResumoStatus(int Abertos, int EmAtendimento);
