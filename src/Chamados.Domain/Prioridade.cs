namespace Chamados.Domain;

/// <summary>
/// Urgência do chamado. Os valores explícitos tornam a ordenação
/// (Baixa &lt; Critica) parte do contrato, e não um acidente da declaração.
/// </summary>
public enum Prioridade
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
    Critica = 4
}