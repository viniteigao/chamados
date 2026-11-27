namespace Chamados.Domain;

/// <summary>
/// O prazo de um chamado avaliado num instante.
/// <paramref name="Folga"/> é o tempo entre o fim considerado e o vencimento: positiva quando sobra
/// tempo, negativa quando passou. Para chamados em andamento, o fim considerado é o instante da
/// avaliação; para os resolvidos, o momento em que foram resolvidos.
/// </summary>
public sealed record PrazoDoChamado(DateTimeOffset Vencimento, SituacaoPrazo Situacao, TimeSpan Folga);
