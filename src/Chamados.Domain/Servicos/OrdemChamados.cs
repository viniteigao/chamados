namespace Chamados.Domain;

/// <summary>Critérios de ordenação da listagem.</summary>
public enum OrdemChamados
{
    /// <summary>Mais prioritário primeiro; empate, o mais antigo primeiro (padrão do Módulo 2).</summary>
    Prioridade,
    MaisRecentes,
    MaisAntigos,
    Titulo
}
