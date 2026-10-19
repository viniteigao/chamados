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

        return chamados
            .Where(c => filtro.Status is null || c.Status == filtro.Status)
            .Where(c => filtro.PrioridadeMinima is null || c.Prioridade >= filtro.PrioridadeMinima)
            .Where(c => string.IsNullOrWhiteSpace(filtro.Texto)
                        || c.Titulo.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase)
                        || (c.Descricao?.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(c => c.Prioridade)
            .ThenBy(c => c.CriadoEm)
            .Select(ResumoChamado.De)
            .ToList();
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

    private async Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken)
    {
        var chamado = await repositorio.ObterAsync(id, cancellationToken)
            ?? throw new ChamadoNaoEncontradoException(id);

        alteracao(chamado);
        await repositorio.AtualizarAsync(chamado, cancellationToken);
        return chamado;
    }
}