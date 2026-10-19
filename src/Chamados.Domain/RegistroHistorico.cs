namespace Chamados.Domain;

/// <summary>
/// Uma transição de status já ocorrida. Imutável: o passado não muda.
/// </summary>
public sealed record RegistroHistorico(
    StatusChamado De,
    StatusChamado Para,
    DateTimeOffset Em,
    string? Observacao);