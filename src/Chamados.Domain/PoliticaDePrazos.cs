namespace Chamados.Domain;

/// <summary>
/// Prazos de atendimento (SLA) por prioridade, em horas corridas, contados a partir da abertura.
/// O prazo é calculado, não gravado: vencimento = abertura + prazo da prioridade atual. Por isso,
/// mudar a prioridade de um chamado muda o vencimento dele, e mudar a política muda o de todos.
/// </summary>
public sealed class PoliticaDePrazos
{
    /// <summary>Faltando menos de um quarto do prazo, o chamado está "perto de vencer".</summary>
    public const double FracaoDeAlerta = 0.25;

    private readonly Dictionary<Prioridade, TimeSpan> _prazos = [];

    public PoliticaDePrazos(IReadOnlyDictionary<Prioridade, TimeSpan> prazos)
    {
        ArgumentNullException.ThrowIfNull(prazos);

        foreach (var prioridade in Enum.GetValues<Prioridade>())
        {
            if (!prazos.TryGetValue(prioridade, out var prazo))
            {
                throw new ArgumentException($"Falta o prazo da prioridade {prioridade}.", nameof(prazos));
            }

            if (prazo <= TimeSpan.Zero)
            {
                throw new ArgumentException($"O prazo da prioridade {prioridade} precisa ser maior que zero.", nameof(prazos));
            }

            _prazos[prioridade] = prazo;
        }

        // Prioridade mais alta nunca espera mais que uma mais baixa: Crítica ≤ Alta ≤ Média ≤ Baixa.
        var daMaisBaixa = Enum.GetValues<Prioridade>().Order().ToList();
        for (var i = 1; i < daMaisBaixa.Count; i++)
        {
            if (_prazos[daMaisBaixa[i]] > _prazos[daMaisBaixa[i - 1]])
            {
                throw new ArgumentException(
                    $"O prazo da prioridade {daMaisBaixa[i]} não pode ser maior que o da prioridade {daMaisBaixa[i - 1]}.",
                    nameof(prazos));
            }
        }
    }

    /// <summary>Crítica em 4 horas, Alta em 1 dia, Média em 3 dias, Baixa em 7 dias.</summary>
    public static PoliticaDePrazos Padrao { get; } = new(new Dictionary<Prioridade, TimeSpan>
    {
        [Prioridade.Critica] = TimeSpan.FromHours(4),
        [Prioridade.Alta] = TimeSpan.FromDays(1),
        [Prioridade.Media] = TimeSpan.FromDays(3),
        [Prioridade.Baixa] = TimeSpan.FromDays(7),
    });

    /// <summary>Os prazos de todas as prioridades, da mais urgente à menos.</summary>
    public IReadOnlyDictionary<Prioridade, TimeSpan> Prazos
        => _prazos.OrderByDescending(p => p.Key).ToDictionary(p => p.Key, p => p.Value);

    public TimeSpan PrazoDe(Prioridade prioridade)
        => _prazos.TryGetValue(prioridade, out var prazo)
            ? prazo
            : throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "Prioridade inexistente.");

    public DateTimeOffset VencimentoDe(Prioridade prioridade, DateTimeOffset criadoEm) => criadoEm + PrazoDe(prioridade);

    /// <summary>
    /// Avalia o prazo no instante <paramref name="agora"/>. Chamado cancelado não tem prazo a cumprir:
    /// devolve null. Exatamente no vencimento, ainda não venceu.
    /// </summary>
    public PrazoDoChamado? Avaliar(StatusChamado status, Prioridade prioridade, DateTimeOffset criadoEm,
        DateTimeOffset? encerradoEm, DateTimeOffset agora)
    {
        if (status == StatusChamado.Cancelado)
        {
            return null;
        }

        var prazo = PrazoDe(prioridade);
        var vencimento = criadoEm + prazo;

        if (status == StatusChamado.Resolvido)
        {
            var folgaFinal = vencimento - (encerradoEm ?? agora);
            return new PrazoDoChamado(vencimento,
                folgaFinal >= TimeSpan.Zero ? SituacaoPrazo.Cumprido : SituacaoPrazo.Descumprido, folgaFinal);
        }

        // Comparações estritas, as mesmas do filtro no banco (AberturasVencidas, AberturasEmAlerta):
        // a tela e a contagem nunca discordam sobre um chamado na fronteira.
        var folga = vencimento - agora;
        var situacao = folga < TimeSpan.Zero ? SituacaoPrazo.Vencido
            : folga < prazo * FracaoDeAlerta ? SituacaoPrazo.PertoDeVencer
            : SituacaoPrazo.NoPrazo;
        return new PrazoDoChamado(vencimento, situacao, folga);
    }

    public PrazoDoChamado? Avaliar(Chamado chamado, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        return Avaliar(chamado.Status, chamado.Prioridade, chamado.CriadoEm, chamado.EncerradoEm, agora);
    }

    public PrazoDoChamado? Avaliar(ResumoChamado chamado, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        return Avaliar(chamado.Status, chamado.Prioridade, chamado.CriadoEm, chamado.EncerradoEm, agora);
    }

    /// <summary>
    /// Para consultar vencidos no banco: em cada prioridade, um chamado em andamento aberto antes
    /// do instante devolvido já está vencido em <paramref name="agora"/>.
    /// </summary>
    public IReadOnlyDictionary<Prioridade, DateTimeOffset> AberturasVencidas(DateTimeOffset agora)
        => _prazos.ToDictionary(p => p.Key, p => agora - p.Value);

    /// <summary>Como <see cref="AberturasVencidas"/>, mas para "vencido ou perto de vencer".</summary>
    public IReadOnlyDictionary<Prioridade, DateTimeOffset> AberturasEmAlerta(DateTimeOffset agora)
        => _prazos.ToDictionary(p => p.Key, p => agora - (p.Value - (p.Value * FracaoDeAlerta)));
}
