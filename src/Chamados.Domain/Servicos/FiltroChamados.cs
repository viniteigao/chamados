namespace Chamados.Domain;

/// <summary>
/// Critérios de consulta. Imutável: para variar um critério,
/// use uma expressão with, que cria uma cópia alterada.
/// </summary>
public sealed record FiltroChamados(
    StatusChamado? Status = null,
    Prioridade? PrioridadeMinima = null,
    string? Texto = null)
{
    public static FiltroChamados Todos { get; } = new();
}