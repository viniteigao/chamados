using Chamados.Domain;

namespace Chamados.Domain.Tests;

public class ChamadoTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    // --- Herdados do Módulo 1 ---

    [Fact]
    public void NovoChamado_NasceComStatusAberto()
    {
        var chamado = new Chamado("Trocar lâmpada do corredor");

        Assert.Equal(StatusChamado.Aberto, chamado.Status);
    }

    [Fact]
    public void NovoChamado_RemoveEspacosDoTitulo()
    {
        var chamado = new Chamado("  Vazamento na copa  ");

        Assert.Equal("Vazamento na copa", chamado.Titulo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NovoChamado_TituloEmBranco_LancaArgumentException(string titulo)
    {
        Assert.Throws<ArgumentException>(() => new Chamado(titulo));
    }

    [Fact]
    public void NovoChamado_TituloNulo_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Chamado(null!));
    }

    // --- Módulo 2 ---

    [Fact]
    public void NovoChamado_DescricaoEmBranco_FicaNula()
    {
        var chamado = new Chamado("Tomada solta", descricao: "   ");

        Assert.Null(chamado.Descricao);
    }

    [Fact]
    public void IniciarAtendimento_DeAberto_MudaStatusERegistraHistorico()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        chamado.IniciarAtendimento(" Elétrica ", Abertura.AddMinutes(10));

        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
        Assert.Equal("Elétrica", chamado.Responsavel);
        var registro = Assert.Single(chamado.Historico);
        Assert.Equal(new RegistroHistorico(
            StatusChamado.Aberto, StatusChamado.EmAtendimento, Abertura.AddMinutes(10), "Atribuído a Elétrica"), registro);
    }

    [Fact]
    public void Resolver_SemAtendimento_LancaTransicaoInvalida()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        var erro = Assert.Throws<TransicaoInvalidaException>(
            () => chamado.Resolver("Tomada fixada", Abertura.AddHours(1)));

        Assert.Equal(StatusChamado.Aberto, erro.Atual);
        Assert.Equal(StatusChamado.Resolvido, erro.Pretendido);
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Empty(chamado.Historico);
    }

    [Fact]
    public void Cancelar_ChamadoResolvido_LancaTransicaoInvalida()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento("Elétrica", Abertura.AddMinutes(10));
        chamado.Resolver("Tomada fixada", Abertura.AddHours(1));

        Assert.Throws<TransicaoInvalidaException>(() => chamado.Cancelar("Engano", Abertura.AddHours(2)));
    }

    [Fact]
    public void Resolver_DefineEncerramentoETempoAteEncerramento()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento("Elétrica", Abertura.AddMinutes(10));

        chamado.Resolver("Tomada fixada", Abertura.AddHours(2));

        Assert.True(chamado.EstaEncerrado);
        Assert.Equal(Abertura.AddHours(2), chamado.EncerradoEm);
        Assert.Equal(TimeSpan.FromHours(2), chamado.TempoAteEncerramento);
        Assert.Equal(2, chamado.Historico.Count);
    }

    [Fact]
    public void ChamadoAberto_NaoTemTempoAteEncerramento()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        Assert.Null(chamado.TempoAteEncerramento);
    }

    [Fact]
    public void Transicao_ComDataAnteriorAAbertura_LancaArgumentOutOfRange()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => chamado.IniciarAtendimento("Elétrica", Abertura.AddMinutes(-1)));
    }

    [Fact]
    public void Historico_NaoPodeSerConvertidoEmListaMutavel()
    {
        var chamado = new Chamado("Tomada solta");

        Assert.False(chamado.Historico is List<RegistroHistorico>);
    }
}