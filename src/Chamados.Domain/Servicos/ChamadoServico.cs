using System.Globalization;

namespace Chamados.Domain;

/// <summary>
/// Casos de uso dos chamados. Depende de abstrações recebidas por injeção:
/// o repositório (onde guardar) e o relógio (que horas são).
/// </summary>
public sealed class ChamadoServico(IChamadoRepositorio repositorio, TimeProvider relogio)
{
    public async Task<Chamado> AbrirAsync(NovoChamado comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var chamado = new Chamado(comando.Titulo, comando.Prioridade, comando.Descricao, relogio.GetUtcNow());
        await repositorio.AdicionarAsync(chamado, cancellationToken);
        return chamado;
    }

    /// <summary>Consulta um chamado. Devolve null quando o Id não existe.</summary>
    public Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
        => repositorio.ObterAsync(id, cancellationToken);

    /// <summary>
    /// Edita título, prioridade e descrição, desde que ninguém tenha alterado
    /// o chamado depois que o usuário o leu.
    /// </summary>
    public Task<Chamado> EditarAsync(Guid id, EdicaoChamado comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        return AlterarAsync(id, chamado =>
        {
            if (chamado.Versao != comando.VersaoEsperada)
            {
                throw new ConcorrenciaException(id, comando.VersaoEsperada, chamado.Versao);
            }

            chamado.Editar(comando.Titulo, comando.Prioridade, comando.Descricao);
        }, cancellationToken);
    }

    public Task<Chamado> IniciarAtendimentoAsync(Guid id, string responsavel, CancellationToken cancellationToken = default)
        => AlterarAsync(id, chamado => chamado.IniciarAtendimento(responsavel, relogio.GetUtcNow()), cancellationToken);

    public Task<Chamado> ResolverAsync(Guid id, string solucao, CancellationToken cancellationToken = default)
        => AlterarAsync(id, chamado => chamado.Resolver(solucao, relogio.GetUtcNow()), cancellationToken);

    public Task<Chamado> CancelarAsync(Guid id, string motivo, CancellationToken cancellationToken = default)
        => AlterarAsync(id, chamado => chamado.Cancelar(motivo, relogio.GetUtcNow()), cancellationToken);

    public async Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var chamados = await repositorio.ListarAsync(cancellationToken);
        return Consultar(chamados, filtro).Select(ResumoChamado.De).ToList();
    }

    /// <summary>
    /// Uma página da listagem. Número de página fora do intervalo é ajustado
    /// para o mais próximo válido: ?pagina=99 mostra a última, não um erro.
    /// </summary>
    public async Task<Pagina<ResumoChamado>> ListarPaginaAsync(
        FiltroChamados filtro,
        int numero,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanhoPagina, 1);

        var chamados = await repositorio.ListarAsync(cancellationToken);
        var resultado = Consultar(chamados, filtro).ToList();

        var totalPaginas = Math.Max(1, (resultado.Count + tamanhoPagina - 1) / tamanhoPagina);
        numero = Math.Clamp(numero, 1, totalPaginas);

        var itens = resultado
            .Skip((numero - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(ResumoChamado.De)
            .ToList();

        return new Pagina<ResumoChamado>(itens, numero, tamanhoPagina, resultado.Count);
    }

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(CancellationToken cancellationToken = default)
    {
        var chamados = await repositorio.ListarAsync(cancellationToken);

        // Todos os status aparecem, mesmo com zero: parte do enum, não dos dados.
        var porStatus = Enum.GetValues<StatusChamado>()
            .ToDictionary(status => status, status => chamados.Count(c => c.Status == status));

        // Só as prioridades presentes aparecem: GroupBy agrupa o que existe.
        var porPrioridade = chamados
            .GroupBy(c => c.Prioridade)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        var temposDeResolucao = chamados
            .Where(c => c.Status == StatusChamado.Resolvido)
            .Select(c => c.TempoAteEncerramento!.Value)
            .ToList();

        TimeSpan? tempoMedio = temposDeResolucao.Count == 0
            ? null
            : TimeSpan.FromTicks((long)temposDeResolucao.Average(t => t.Ticks));

        return new EstatisticasChamados(chamados.Count, porStatus, porPrioridade, tempoMedio);
    }

    // Comparação de textos em português: "Água" fica junto de "agua", antes de "Bebedouro".
    private static readonly StringComparer ComparadorTitulo =
        StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    private static IEnumerable<Chamado> Consultar(IEnumerable<Chamado> chamados, FiltroChamados filtro)
    {
        var filtrados = chamados
            .Where(c => filtro.Status is null || c.Status == filtro.Status)
            .Where(c => filtro.PrioridadeMinima is null || c.Prioridade >= filtro.PrioridadeMinima)
            .Where(c => string.IsNullOrWhiteSpace(filtro.Texto)
                        || c.Titulo.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase)
                        || (c.Descricao?.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase) ?? false));

        return filtro.Ordem switch
        {
            OrdemChamados.MaisRecentes => filtrados.OrderByDescending(c => c.CriadoEm),
            OrdemChamados.MaisAntigos => filtrados.OrderBy(c => c.CriadoEm),
            OrdemChamados.Titulo => filtrados.OrderBy(c => c.Titulo, ComparadorTitulo),
            _ => filtrados.OrderByDescending(c => c.Prioridade).ThenBy(c => c.CriadoEm)
        };
    }

    private async Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken)
    {
        var chamado = await repositorio.ObterAsync(id, cancellationToken)
            ?? throw new ChamadoNaoEncontradoException(id);

        alteracao(chamado);
        await repositorio.AtualizarAsync(chamado, cancellationToken);
        return chamado;
    }
}
