namespace Chamados.Domain;

/// <summary>Indicadores agregados da base de chamados.</summary>
public sealed record EstatisticasChamados(
    int Total,
    IReadOnlyDictionary<StatusChamado, int> PorStatus,
    IReadOnlyDictionary<Prioridade, int> PorPrioridade,
    TimeSpan? TempoMedioResolucao);