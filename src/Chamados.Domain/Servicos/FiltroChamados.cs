namespace Chamados.Domain;

/// <summary>
/// Critérios de consulta. Imutável: para variar um critério,
/// use uma expressão with, que cria uma cópia alterada.
/// </summary>
public sealed record FiltroChamados(
    StatusChamado? Status = null,
    Prioridade? PrioridadeMinima = null,
    string? Texto = null,
    OrdemChamados Ordem = OrdemChamados.Prioridade,
    bool SomenteVencidos = false,
    string? AbertoPorId = null)
{
    // Meus chamados: AbertoPorId restringe aos chamados que esse usuário abriu. Quem preenche é a
    // camada Web, a partir do login (Seguranca/Visibilidade.cs); o domínio só aplica o critério.

    public static FiltroChamados Todos { get; } = new();

    /// <summary>
    /// Prazos: o pedido "só vencidos" traduzido para o repositório. Em cada prioridade, só entram
    /// chamados em andamento abertos antes do instante indicado. Quem preenche é o ChamadoServico,
    /// que conhece a política de prazos e o relógio; o repositório só aplica o critério.
    /// </summary>
    public IReadOnlyDictionary<Prioridade, DateTimeOffset>? AbertosAntesDe { get; init; }
}
