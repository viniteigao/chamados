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

        chamado.IniciarAtendimento(Responsavel(" Elétrica "), Abertura.AddMinutes(10));

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
        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddMinutes(10));
        chamado.Resolver("Tomada fixada", Abertura.AddHours(1));

        Assert.Throws<TransicaoInvalidaException>(() => chamado.Cancelar("Engano", Abertura.AddHours(2)));
    }

    [Fact]
    public void Resolver_DefineEncerramentoETempoAteEncerramento()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddMinutes(10));

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
            () => chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddMinutes(-1)));
    }

    [Fact]
    public void Historico_NaoPodeSerConvertidoEmListaMutavel()
    {
        var chamado = new Chamado("Tomada solta");

        Assert.False(chamado.Historico is List<RegistroHistorico>);
    }

    // --- Módulo 3: limites que o formulário web também exibe ---

    [Fact]
    public void NovoChamado_TituloAcimaDoLimite_LancaArgumentException()
    {
        var titulo = new string('a', Chamado.TamanhoMaximoTitulo + 1);

        var erro = Assert.Throws<ArgumentException>(() => new Chamado(titulo));
        Assert.Equal("titulo", erro.ParamName);
    }

    [Fact]
    public void NovoChamado_TituloNoLimiteComEspacosNasPontas_EhAceito()
    {
        // O limite vale para o texto já sem espaços: o Trim acontece antes da verificação.
        var titulo = "  " + new string('a', Chamado.TamanhoMaximoTitulo) + "  ";

        var chamado = new Chamado(titulo);

        Assert.Equal(Chamado.TamanhoMaximoTitulo, chamado.Titulo.Length);
    }

    [Fact]
    public void NovoChamado_DescricaoAcimaDoLimite_LancaArgumentException()
    {
        var descricao = new string('a', Chamado.TamanhoMaximoDescricao + 1);

        Assert.Throws<ArgumentException>(() => new Chamado("Tomada solta", descricao: descricao));
    }

    [Fact]
    public void NovoChamado_PrioridadeInexistente_LancaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Chamado("Tomada solta", (Prioridade)99));
    }

    // --- Módulo 4: edição e versão ---

    [Fact]
    public void NovoChamado_NasceNaVersao1()
    {
        Assert.Equal(1, new Chamado("Tomada solta").Versao);
    }

    [Fact]
    public void Editar_AlteraDadosEIncrementaVersao()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, criadoEm: Abertura);

        chamado.Editar("  Tomada solta na recepção  ", Prioridade.Alta, "Perto da porta", Abertura.AddMinutes(5));

        Assert.Equal("Tomada solta na recepção", chamado.Titulo);
        Assert.Equal(Prioridade.Alta, chamado.Prioridade);
        Assert.Equal("Perto da porta", chamado.Descricao);
        Assert.Equal(2, chamado.Versao);
    }

    [Fact]
    public void Editar_TituloInvalido_NaoAlteraNada()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, "Original", Abertura);

        Assert.Throws<ArgumentOutOfRangeException>(() => chamado.Editar("Título válido", (Prioridade)99, "Nova", Abertura.AddMinutes(5)));

        Assert.Equal("Tomada solta", chamado.Titulo);
        Assert.Equal("Original", chamado.Descricao);
        Assert.Equal(1, chamado.Versao);
    }

    [Fact]
    public void Editar_ChamadoResolvido_LancaChamadoEncerrado()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddHours(1));
        chamado.Resolver("Tomada fixada", Abertura.AddHours(2));

        var erro = Assert.Throws<ChamadoEncerradoException>(() => chamado.Editar("Outro título", Prioridade.Alta, null, Abertura.AddHours(3)));

        Assert.Equal(StatusChamado.Resolvido, erro.Status);
    }

    [Fact]
    public void TransicaoDeStatus_TambemIncrementaVersao()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddHours(1));

        Assert.Equal(2, chamado.Versao);
    }

    // --- Melhorias: tamanho máximo dos textos das transições ---

    [Fact]
    public void IniciarAtendimento_ResponsavelLongoDemais_LancaArgumentExceptionSemMudarNada()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        var nome = new string('a', Chamado.TamanhoMaximoResponsavel + 1);

        var erro = Assert.Throws<ArgumentException>(() => chamado.IniciarAtendimento(Responsavel(nome), Abertura.AddMinutes(10)));

        Assert.Equal("responsavel", erro.ParamName);
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Null(chamado.Responsavel);
        Assert.Empty(chamado.Historico);
        Assert.Equal(1, chamado.Versao);
    }

    [Fact]
    public void IniciarAtendimento_ResponsavelNoLimite_AceitaEMedeDepoisDeTirarOsEspacos()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        var nome = new string('a', Chamado.TamanhoMaximoResponsavel);

        chamado.IniciarAtendimento(Responsavel($"  {nome}  "), Abertura.AddMinutes(10));

        Assert.Equal(nome, chamado.Responsavel);
    }

    [Fact]
    public void Resolver_SolucaoLongaDemais_LancaArgumentExceptionSemEncerrar()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddMinutes(10));
        var solucao = new string('x', Chamado.TamanhoMaximoJustificativa + 1);

        var erro = Assert.Throws<ArgumentException>(() => chamado.Resolver(solucao, Abertura.AddHours(1)));

        Assert.Equal("solucao", erro.ParamName);
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
        Assert.Null(chamado.Solucao);
        Assert.Null(chamado.EncerradoEm);
    }

    [Fact]
    public void Cancelar_MotivoLongoDemais_LancaArgumentExceptionSemEncerrar()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        var motivo = new string('x', Chamado.TamanhoMaximoJustificativa + 1);

        var erro = Assert.Throws<ArgumentException>(() => chamado.Cancelar(motivo, Abertura.AddHours(1)));

        Assert.Equal("motivo", erro.ParamName);
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Null(chamado.EncerradoEm);
        Assert.Empty(chamado.Historico);
    }

    // --- Meus chamados ---

    private static readonly Autor Bruno = new("id-bruno", "Bruno");
    private static readonly Autor Ana = new("id-ana", "Ana");

    [Fact]
    public void NovoChamado_GuardaQuemAbriu()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);

        Assert.Equal(Bruno, chamado.AbertoPor);
        Assert.True(chamado.FoiAbertoPor("id-bruno"));
        Assert.False(chamado.FoiAbertoPor("id-ana"));
    }

    [Fact]
    public void NovoChamado_SemAutor_NaoTemDonoENinguemEDono()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        Assert.Null(chamado.AbertoPor);
        Assert.False(chamado.FoiAbertoPor("id-bruno"));
    }

    [Fact]
    public void Transicoes_RegistramQuemFezCadaMudanca()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);

        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddMinutes(10), por: Ana);
        chamado.Resolver("Tomada fixada", Abertura.AddHours(1));   // sem autor: registro sem "por"

        Assert.Equal(Ana, chamado.Historico[0].Por);
        Assert.Null(chamado.Historico[1].Por);
        Assert.Equal(Bruno, chamado.AbertoPor);   // quem abriu não muda com o atendimento
    }

    [Theory]
    [InlineData("", "Bruno")]
    [InlineData("id-bruno", "  ")]
    public void Autor_SemIdOuSemNome_EhRecusado(string id, string nome)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Autor(id, nome));
    }

    // --- Comentários ---

    [Fact]
    public void Comentar_GuardaTextoAutorDataEHora_NaOrdemEmQueChegam()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);

        chamado.Comentar("  Faísca ao ligar o carregador  ", Abertura.AddMinutes(5), Bruno);
        var resposta = chamado.Comentar("Vou desligar o disjuntor.", Abertura.AddMinutes(7), Ana);

        Assert.Equal(2, chamado.Comentarios.Count);
        Assert.Equal("Faísca ao ligar o carregador", chamado.Comentarios[0].Texto);   // sem os espaços das pontas
        Assert.Equal(Abertura.AddMinutes(5), chamado.Comentarios[0].Em);
        Assert.Equal(Bruno, chamado.Comentarios[0].Autor);
        Assert.Same(resposta, chamado.Comentarios[1]);
        Assert.Equal(Ana, resposta.Autor);
    }

    [Fact]
    public void Comentar_NaoMudaAVersaoNemOHistorico()
    {
        // Comentar não é editar: quem está editando o chamado ao mesmo tempo não recebe conflito.
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        chamado.Comentar("Alguma novidade?", Abertura.AddMinutes(5), Bruno);

        Assert.Equal(1, chamado.Versao);
        Assert.Empty(chamado.Historico);
    }

    [Fact]
    public void Comentar_DepoisDeResolvido_EhPermitido()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Responsavel("Elétrica"), Abertura.AddMinutes(10));
        chamado.Resolver("Tomada fixada", Abertura.AddHours(1));

        chamado.Comentar("Voltou a soltar hoje.", Abertura.AddDays(1), Bruno);

        Assert.Single(chamado.Comentarios);
        Assert.Equal(StatusChamado.Resolvido, chamado.Status);   // conversar não reabre o chamado
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Comentar_SemTexto_EhRecusado(string texto)
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        Assert.ThrowsAny<ArgumentException>(() => chamado.Comentar(texto, Abertura.AddMinutes(1), Bruno));
        Assert.Empty(chamado.Comentarios);
    }

    [Fact]
    public void Comentar_LongoDemais_EhRecusadoComMensagemEmPortugues()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        var longo = new string('a', Chamado.TamanhoMaximoComentario + 1);

        var erro = Assert.Throws<ArgumentException>(() => chamado.Comentar(longo, Abertura.AddMinutes(1), Bruno));

        Assert.StartsWith($"O comentário deve ter no máximo {Chamado.TamanhoMaximoComentario} caracteres.", erro.Message,
            StringComparison.Ordinal);
        Assert.Empty(chamado.Comentarios);
    }

    [Fact]
    public void Comentar_NoLimiteExato_EhAceito()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        chamado.Comentar(new string('a', Chamado.TamanhoMaximoComentario), Abertura.AddMinutes(1), Bruno);

        Assert.Single(chamado.Comentarios);
    }

    [Fact]
    public void Comentar_AntesDaAbertura_OuSemAutor_EhRecusado()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        Assert.Throws<ArgumentOutOfRangeException>(() => chamado.Comentar("Oi", Abertura.AddSeconds(-1), Bruno));
        Assert.Throws<ArgumentNullException>(() => chamado.Comentar("Oi", Abertura, null!));
        Assert.Empty(chamado.Comentarios);
    }

    // --- Histórico das edições ---

    [Fact]
    public void Editar_RegistraOQueMudouQuandoEPorQuem()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, "Perto da porta", Abertura);

        chamado.Editar("Tomada solta na recepção", Prioridade.Alta, "Perto da porta", Abertura.AddMinutes(30), Ana);

        var edicao = Assert.Single(chamado.Edicoes);
        Assert.Equal(Abertura.AddMinutes(30), edicao.Em);
        Assert.Equal("Tomada solta", edicao.TituloAnterior);
        Assert.Equal("Tomada solta na recepção", edicao.TituloNovo);
        Assert.Equal(Prioridade.Baixa, edicao.PrioridadeAnterior);
        Assert.Equal(Prioridade.Alta, edicao.PrioridadeNova);
        Assert.False(edicao.DescricaoAlterada);
        Assert.Equal(Ana, edicao.Por);
        Assert.Empty(chamado.Historico);   // o status não mudou
    }

    [Fact]
    public void Editar_SoADescricao_NaoRegistraTituloNemPrioridade()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, "Perto da porta", Abertura);

        chamado.Editar("Tomada solta", Prioridade.Baixa, "Ao lado do bebedouro", Abertura.AddMinutes(5), Ana);

        var edicao = Assert.Single(chamado.Edicoes);
        Assert.Null(edicao.TituloAnterior);
        Assert.Null(edicao.TituloNovo);
        Assert.Null(edicao.PrioridadeAnterior);
        Assert.Null(edicao.PrioridadeNova);
        Assert.True(edicao.DescricaoAlterada);
    }

    [Fact]
    public void Editar_SemMudarNada_NaoEntraNoHistorico()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, "Perto da porta", Abertura);

        // Os espaços das pontas são removidos: o título final é o mesmo de antes.
        chamado.Editar("  Tomada solta ", Prioridade.Baixa, "Perto da porta", Abertura.AddMinutes(5), Ana);

        Assert.Empty(chamado.Edicoes);
        Assert.Equal(2, chamado.Versao);   // a versão aumenta, como sempre aumentou
    }

    [Fact]
    public void Editar_AntesDaAbertura_EhRecusadoSemAlterarNada()
    {
        var chamado = new Chamado("Tomada solta", Prioridade.Baixa, criadoEm: Abertura);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => chamado.Editar("Outro título", Prioridade.Alta, null, Abertura.AddSeconds(-1), Ana));

        Assert.Equal("Tomada solta", chamado.Titulo);
        Assert.Empty(chamado.Edicoes);
        Assert.Equal(1, chamado.Versao);
    }

    [Fact]
    public void Listas_SaemEmOrdemCronologica_MesmoComRegistrosForaDeOrdem()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        chamado.Comentar("Segunda", Abertura.AddMinutes(10), Bruno);
        chamado.Comentar("Primeira", Abertura.AddMinutes(5), Bruno);
        chamado.Editar("B", Prioridade.Alta, null, Abertura.AddMinutes(20), Ana);
        chamado.Editar("A", Prioridade.Media, null, Abertura.AddMinutes(15), Ana);

        Assert.Equal(["Primeira", "Segunda"], chamado.Comentarios.Select(m => m.Texto));
        Assert.Equal([Abertura.AddMinutes(15), Abertura.AddMinutes(20)], chamado.Edicoes.Select(e => e.Em));
    }

    // Responsável atendente: o responsável é uma pessoa (Id e nome). Nos testes, o Id deriva do nome.
    private static Autor Responsavel(string nome) => new($"id-{nome.Trim()}", nome);

    // --- Responsável atendente ---

    [Fact]
    public void IniciarAtendimento_GuardaIdENomeDoResponsavel()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        chamado.IniciarAtendimento(Ana, Abertura.AddMinutes(10), por: Ana);

        Assert.Equal(Ana.Id, chamado.ResponsavelId);
        Assert.Equal(Ana.Nome, chamado.Responsavel);
        Assert.True(chamado.FoiAtribuidoA(Ana.Id));
        Assert.False(chamado.FoiAtribuidoA(Bruno.Id));
    }

    [Fact]
    public void Reatribuir_EmAtendimento_TrocaOResponsavelERegistraNasEdicoes()
    {
        var davi = new Autor("id-davi", "Davi");
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Ana, Abertura.AddMinutes(10), por: Ana);

        chamado.Reatribuir(davi, Abertura.AddMinutes(30), por: Ana);

        Assert.Equal("id-davi", chamado.ResponsavelId);
        Assert.Equal("Davi", chamado.Responsavel);
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);   // o status não muda
        Assert.Equal(3, chamado.Versao);
        var edicao = Assert.Single(chamado.Edicoes);
        Assert.Equal(Ana.Nome, edicao.ResponsavelAnterior);
        Assert.Equal("Davi", edicao.ResponsavelNovo);
        Assert.Equal(Abertura.AddMinutes(30), edicao.Em);
        Assert.Equal(Ana, edicao.Por);
        Assert.Single(chamado.Historico);   // só o início do atendimento
    }

    [Fact]
    public void Reatribuir_ParaQuemJaEOResponsavel_NaoMudaNada()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Ana, Abertura.AddMinutes(10));

        chamado.Reatribuir(Ana, Abertura.AddMinutes(30));

        Assert.Empty(chamado.Edicoes);
        Assert.Equal(2, chamado.Versao);
    }

    [Fact]
    public void Reatribuir_ChamadoAberto_PedeParaIniciarOAtendimento()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);

        var erro = Assert.Throws<ChamadoSemAtendimentoException>(() => chamado.Reatribuir(Ana, Abertura.AddMinutes(5)));

        Assert.Equal(StatusChamado.Aberto, erro.Status);
        Assert.Null(chamado.ResponsavelId);
    }

    [Fact]
    public void Reatribuir_ChamadoEncerrado_LancaChamadoEncerrado()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento(Ana, Abertura.AddMinutes(10));
        chamado.Resolver("Tomada fixada", Abertura.AddHours(1));

        Assert.Throws<ChamadoEncerradoException>(() => chamado.Reatribuir(Bruno, Abertura.AddHours(2)));
        Assert.Equal(Ana.Id, chamado.ResponsavelId);
    }
}
