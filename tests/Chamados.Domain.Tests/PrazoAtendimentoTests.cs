using Chamados.Domain;

namespace Chamados.Domain.Tests;

public class PrazoAtendimentoTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    // --- Atividade C3 ---

    [Theory]
    [InlineData(Prioridade.Critica, 4)]
    [InlineData(Prioridade.Alta, 8)]
    [InlineData(Prioridade.Media, 24)]
    [InlineData(Prioridade.Baixa, 72)]
    public void VenceEm_SomaOPrazoDaPrioridadeAAbertura(Prioridade prioridade, int horas)
    {
        Assert.Equal(TimeSpan.FromHours(horas), PrazoAtendimento.Para(prioridade));
        Assert.Equal(Abertura.AddHours(horas), PrazoAtendimento.VenceEm(Abertura, prioridade));
    }

    [Fact]
    public void Para_PrioridadeInexistente_LancaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrazoAtendimento.Para((Prioridade)99));
    }
}
