namespace Chamados.Domain;

/// <summary>
/// Histórico das edições: uma edição de título, prioridade ou descrição, com quando e por quem.
/// Imutável, como os outros registros. Título e prioridade guardam o valor de antes e o de depois
/// (nulos quando não mudaram); da descrição, só se mudou: o texto pode ter 2000 caracteres e o
/// atual continua no chamado. Mudar a prioridade muda o prazo, e este registro é o rastro disso.
/// </summary>
public sealed record RegistroEdicao(
    DateTimeOffset Em,
    string? TituloAnterior,
    string? TituloNovo,
    Prioridade? PrioridadeAnterior,
    Prioridade? PrioridadeNova,
    bool DescricaoAlterada,
    string? PorId = null,
    string? PorNome = null)
{
    /// <summary>Quem editou; null quando a edição não informou o autor.</summary>
    public Autor? Por => PorId is null ? null : new Autor(PorId, PorNome ?? PorId);
}
