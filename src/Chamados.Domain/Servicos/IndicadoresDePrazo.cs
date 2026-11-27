namespace Chamados.Domain;

/// <summary>
/// Indicadores de prazo (SLA): os chamados em andamento que venceram ou estão perto de vencer
/// agora, e quantos dos resolvidos cumpriram o prazo. Leva junto a tabela de prazos em vigor.
/// </summary>
public sealed record IndicadoresDePrazo(
    int Vencidos,
    int PertoDeVencer,
    int ResolvidosNoPrazo,
    int ResolvidosForaDoPrazo,
    IReadOnlyDictionary<Prioridade, TimeSpan> Prazos)
{
    /// <summary>Percentual dos resolvidos que cumpriram o prazo; null enquanto não houver resolvidos.</summary>
    public double? PercentualNoPrazo
    {
        get
        {
            var resolvidos = ResolvidosNoPrazo + ResolvidosForaDoPrazo;
            return resolvidos == 0 ? null : Math.Round(100.0 * ResolvidosNoPrazo / resolvidos, 1);
        }
    }
}
