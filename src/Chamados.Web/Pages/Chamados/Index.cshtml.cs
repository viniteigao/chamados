using Chamados.Domain;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Lista com filtros, ordenação e paginação. Tudo vem da query string:
/// a URL descreve a consulta inteira e pode ser compartilhada.
/// Meus chamados: quem não vê todos os chamados vê sempre só os que abriu; quem vê todos
/// pode pedir só os seus com ?meus=true.
/// Mensagens não lidas: cada linha traz quantas mensagens o usuário ainda não leu, e ?comNaoLidas=true
/// deixa só os chamados que têm alguma.
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

    /// <summary>Prazos: só chamados em andamento com o prazo vencido (?somenteVencidos=true).</summary>
    [BindProperty(SupportsGet = true)]
    public bool SomenteVencidos { get; set; }

    /// <summary>Meus chamados: pedido de ver só os próprios (?meus=true), para quem vê todos.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Meus { get; set; }

    /// <summary>Responsável atendente: só os chamados que estão com quem pede (?atribuidosAMim=true).</summary>
    [BindProperty(SupportsGet = true)]
    public bool AtribuidosAMim { get; set; }

    /// <summary>Mensagens não lidas: só os chamados com mensagem que o usuário ainda não leu (?comNaoLidas=true).</summary>
    [BindProperty(SupportsGet = true)]
    public bool ComNaoLidas { get; set; }

    /// <summary>Responsável atendente: o filtro "Atribuídos a mim" é para quem atende.</summary>
    public bool PodeFiltrarAtribuidos { get; private set; }

    /// <summary>Meus chamados: verdadeiro quando a lista mostra só os chamados do usuário.</summary>
    public bool SoOsMeus { get; private set; }

    /// <summary>Meus chamados: quem vê todos ganha a opção "Só os meus"; os demais já veem só os seus.</summary>
    public bool PodeEscolher { get; private set; }

    public Pagina<ResumoChamado> Resultado { get; private set; } = null!;

    /// <summary>Prazos: o prazo de cada linha agora, calculado pelo serviço; null para cancelados.</summary>
    public PrazoDoChamado? PrazoDe(ResumoChamado chamado) => servico.PrazoDe(chamado);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Valor fora do enum na URL (?status=Xyz) não lança exceção: vira erro no ModelState.
        if (!ModelState.IsValid)
        {
            Status = null;
            PrioridadeMinima = null;
            Ordem = OrdemChamados.Prioridade;
            NumeroPagina = 1;
            SomenteVencidos = false;
            Meus = false;
            AtribuidosAMim = false;
            ComNaoLidas = false;
        }

        var dono = User.DonoParaListar(Meus);
        SoOsMeus = dono is not null;
        PodeEscolher = User.VeTodosOsChamados();

        PodeFiltrarAtribuidos = User.IsInRole(Politicas.PapelAtendente);
        var responsavel = AtribuidosAMim ? User.IdDoUsuario() ?? "-" : null;

        // Mensagens não lidas: o leitor é sempre quem pede; o filtro, só quando marcado.
        var filtro = new FiltroChamados(Status, PrioridadeMinima, Texto, Ordem, SomenteVencidos, dono, responsavel,
            User.IdDoUsuario(), ComNaoLidas);
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
        somenteVencidos = SomenteVencidos ? true : (bool?)null,
        meus = Meus ? true : (bool?)null,
        atribuidosAMim = AtribuidosAMim ? true : (bool?)null,
        comNaoLidas = ComNaoLidas ? true : (bool?)null,
        pagina = pagina == 1 ? (int?)null : pagina
    };
}
