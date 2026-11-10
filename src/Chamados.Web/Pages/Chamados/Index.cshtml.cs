using Chamados.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Lista com filtros, ordenação e paginação. Tudo vem da query string:
/// a URL descreve a consulta inteira e pode ser compartilhada.
/// </summary>
public sealed class IndexModel(ChamadoServico servico) : PageModel
{
    public const int TamanhoPagina = 5;

    // SupportsGet = true: o model binding também preenche estas propriedades em requisições GET.
    [BindProperty(SupportsGet = true)]
    public StatusChamado? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public Prioridade? PrioridadeMinima { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Texto { get; set; }

    [BindProperty(SupportsGet = true)]
    public OrdemChamados Ordem { get; set; } = OrdemChamados.Prioridade;

    [BindProperty(Name = "pagina", SupportsGet = true)]
    public int NumeroPagina { get; set; } = 1;

    public Pagina<ResumoChamado> Resultado { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Valor fora do enum na URL (?status=Xyz) não lança exceção: vira erro no ModelState.
        if (!ModelState.IsValid)
        {
            Status = null;
            PrioridadeMinima = null;
            Ordem = OrdemChamados.Prioridade;
            NumeroPagina = 1;
        }

        var filtro = new FiltroChamados(Status, PrioridadeMinima, Texto, Ordem);
        Resultado = await servico.ListarPaginaAsync(filtro, NumeroPagina, TamanhoPagina, cancellationToken);
    }

    /// <summary>
    /// Valores de rota para o link de outra página, preservando filtros e ordem.
    /// Valores nulos não entram na URL.
    /// </summary>
    public object RotaPara(int pagina) => new
    {
        status = Status,
        prioridadeMinima = PrioridadeMinima,
        texto = string.IsNullOrWhiteSpace(Texto) ? null : Texto,
        ordem = Ordem == OrdemChamados.Prioridade ? (OrdemChamados?)null : Ordem,
        pagina = pagina == 1 ? (int?)null : pagina
    };
}
