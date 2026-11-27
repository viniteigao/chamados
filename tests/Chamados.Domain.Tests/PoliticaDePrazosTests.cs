namespace Chamados.Domain.Tests;

/// <summary>Prazos: a regra de prazos por prioridade, sem banco e sem relógio (o "agora" é parâmetro).</summary>
public class PoliticaDePrazosTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly PoliticaDePrazos Politica = PoliticaDePrazos.Padrao;

    [Theory]
    [InlineData(Prioridade.Critica, 4)]
    [InlineData(Prioridade.Alta, 24)]
    [InlineData(Prioridade.Media, 72)]
    [InlineData(Prioridade.Baixa, 168)]
    public void Padrao_TemOsPrazosCombinados(Prioridade prioridade, int horas)
    {
        Assert.Equal(TimeSpan.FromHours(horas), Politica.PrazoDe(prioridade));
    }

    [Fact]
    public void Avaliar_LogoDepoisDaAbertura_EstaNoPrazoComAFolgaCerta()
    {
        var prazo = Politica.Avaliar(StatusChamado.Aberto, Prioridade.Critica, Abertura, null, Abertura.AddMinutes(10));

        Assert.NotNull(prazo);
        Assert.Equal(SituacaoPrazo.NoPrazo, prazo.Situacao);
        Assert.Equal(Abertura.AddHours(4), prazo.Vencimento);
        Assert.Equal(TimeSpan.FromMinutes(230), prazo.Folga);
    }

    // Crítica: 240 minutos de prazo; "perto de vencer" com menos de 60 minutos pela frente.
    [Theory]
    [InlineData(179, SituacaoPrazo.NoPrazo)]
    [InlineData(180, SituacaoPrazo.NoPrazo)]         // faltam exatamente 60 min: ainda não é "menos de"
    [InlineData(181, SituacaoPrazo.PertoDeVencer)]
    [InlineData(240, SituacaoPrazo.PertoDeVencer)]   // exatamente no vencimento: ainda não venceu
    [InlineData(241, SituacaoPrazo.Vencido)]
    public void Avaliar_EmAndamento_MudaDeSituacaoNasFronteiras(int minutosDepoisDaAbertura, SituacaoPrazo esperada)
    {
        var prazo = Politica.Avaliar(StatusChamado.EmAtendimento, Prioridade.Critica, Abertura, null,
            Abertura.AddMinutes(minutosDepoisDaAbertura));

        Assert.Equal(esperada, prazo!.Situacao);
    }

    [Fact]
    public void Avaliar_ResolvidoAntesDoVencimento_CumpriuMesmoAvaliadoMuitoDepois()
    {
        var prazo = Politica.Avaliar(StatusChamado.Resolvido, Prioridade.Critica, Abertura, Abertura.AddHours(3),
            agora: Abertura.AddDays(30));

        Assert.Equal(SituacaoPrazo.Cumprido, prazo!.Situacao);
        Assert.Equal(TimeSpan.FromHours(1), prazo.Folga);
    }

    [Fact]
    public void Avaliar_ResolvidoDepoisDoVencimento_DescumpriuComFolgaNegativa()
    {
        var prazo = Politica.Avaliar(StatusChamado.Resolvido, Prioridade.Critica, Abertura, Abertura.AddHours(6),
            agora: Abertura.AddHours(7));

        Assert.Equal(SituacaoPrazo.Descumprido, prazo!.Situacao);
        Assert.Equal(TimeSpan.FromHours(-2), prazo.Folga);
    }

    [Fact]
    public void Avaliar_Cancelado_NaoTemPrazo()
    {
        Assert.Null(Politica.Avaliar(StatusChamado.Cancelado, Prioridade.Critica, Abertura, Abertura.AddDays(1), Abertura.AddDays(2)));
    }

    [Fact]
    public void Avaliar_UsaAPrioridadeAtualDoChamado()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, criadoEm: Abertura);
        var agora = Abertura.AddHours(5);
        var antes = Politica.Avaliar(chamado, agora)!.Situacao;

        chamado.Editar("Tomada solta", Prioridade.Critica, null);

        Assert.Equal(SituacaoPrazo.NoPrazo, antes);
        Assert.Equal(SituacaoPrazo.Vencido, Politica.Avaliar(chamado, agora)!.Situacao);
    }

    [Fact]
    public void AberturasVencidas_ConcordaComAvaliarEmCadaPrioridade()
    {
        // O critério do banco (aberto antes do corte) e o da tela (Avaliar) precisam dar o mesmo resultado.
        var agora = Abertura.AddDays(10);
        var cortes = Politica.AberturasVencidas(agora);

        foreach (var prioridade in Enum.GetValues<Prioridade>())
        {
            var noCorte = Politica.Avaliar(StatusChamado.Aberto, prioridade, cortes[prioridade], null, agora)!;
            var antesDoCorte = Politica.Avaliar(StatusChamado.Aberto, prioridade, cortes[prioridade].AddTicks(-1), null, agora)!;

            Assert.NotEqual(SituacaoPrazo.Vencido, noCorte.Situacao);
            Assert.Equal(SituacaoPrazo.Vencido, antesDoCorte.Situacao);
        }
    }

    [Fact]
    public void Politica_SemUmaPrioridade_EhRecusada()
    {
        var prazos = new Dictionary<Prioridade, TimeSpan>
        {
            [Prioridade.Critica] = TimeSpan.FromHours(4),
            [Prioridade.Alta] = TimeSpan.FromDays(1),
            [Prioridade.Media] = TimeSpan.FromDays(3),
        };

        var erro = Assert.Throws<ArgumentException>(() => new PoliticaDePrazos(prazos));

        Assert.Contains("Baixa", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Politica_ComPrazoZero_EhRecusada()
    {
        var prazos = Prazos(critica: TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new PoliticaDePrazos(prazos));
    }

    [Fact]
    public void Politica_ComPrioridadeMaisAltaEsperandoMais_EhRecusada()
    {
        // Crítica com 2 dias e Alta com 1 dia: o urgente esperaria mais que o menos urgente.
        var prazos = Prazos(critica: TimeSpan.FromDays(2));

        var erro = Assert.Throws<ArgumentException>(() => new PoliticaDePrazos(prazos));

        Assert.Contains("Critica", erro.Message, StringComparison.Ordinal);
    }

    private static Dictionary<Prioridade, TimeSpan> Prazos(TimeSpan critica) => new()
    {
        [Prioridade.Critica] = critica,
        [Prioridade.Alta] = TimeSpan.FromDays(1),
        [Prioridade.Media] = TimeSpan.FromDays(3),
        [Prioridade.Baixa] = TimeSpan.FromDays(7),
    };
}
