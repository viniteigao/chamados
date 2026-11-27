namespace Chamados.Domain;

/// <summary>Como um chamado está em relação ao prazo da sua prioridade.</summary>
public enum SituacaoPrazo
{
    /// <summary>Em andamento, com folga.</summary>
    NoPrazo,

    /// <summary>Em andamento, no último quarto do prazo.</summary>
    PertoDeVencer,

    /// <summary>Em andamento, com o prazo já vencido.</summary>
    Vencido,

    /// <summary>Resolvido até o vencimento.</summary>
    Cumprido,

    /// <summary>Resolvido depois do vencimento.</summary>
    Descumprido
}
