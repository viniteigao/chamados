using Chamados.Domain;

namespace Chamados.Domain.Tests;

/// <summary>
/// Mensagens não lidas: a regra (quem acompanha a conversa, o que conta como não lido) e o serviço,
/// com o repositório em memória. O repositório do banco repete a regra em SQL (Chamados.Infra.Tests).
/// </summary>
public class MensagensNaoLidasTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly Autor Bruno = new("id-bruno", "Bruno");   // solicitante
    private static readonly Autor Ana = new("id-ana", "Ana");         // atendente
    private static readonly Autor Carla = new("id-carla", "Carla");   // coordenação
    private static readonly Autor Davi = new("id-davi", "Davi");      // outro atendente

    private readonly RelogioFixo _relogio = new(Inicio);
    private readonly RepositorioChamadosEmMemoria _repositorio;
    private readonly ChamadoServico _servico;

    public MensagensNaoLidasTests()
    {
        _repositorio = new RepositorioChamadosEmMemoria(_relogio);
        _servico = new ChamadoServico(_repositorio, _relogio);
    }

    // --- A regra ---

    [Fact]
    public void Acompanha_a_conversa_quem_abriu_quem_atende_e_quem_ja_escreveu()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Inicio, abertoPor: Bruno);
        chamado.IniciarAtendimento(Ana, Inicio.AddMinutes(1), Ana);
        chamado.Comentar("Vou acompanhar.", Inicio.AddMinutes(2), Carla);

        Assert.True(chamado.AcompanhaConversa(Bruno.Id));
        Assert.True(chamado.AcompanhaConversa(Ana.Id));
        Assert.True(chamado.AcompanhaConversa(Carla.Id));
        Assert.False(chamado.AcompanhaConversa(Davi.Id));
    }

    [Fact]
    public void Sem_leitura_contam_as_mensagens_das_outras_pessoas_e_as_proprias_nunca()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Inicio, abertoPor: Bruno);
        var daAna = chamado.Comentar("Chego às 10h.", Inicio.AddMinutes(1), Ana);
        var doBruno = chamado.Comentar("Combinado.", Inicio.AddMinutes(2), Bruno);
        var daAnaDeNovo = chamado.Comentar("Cheguei.", Inicio.AddMinutes(3), Ana);

        Assert.Equal([daAna, daAnaDeNovo], chamado.NaoLidasPor(Bruno.Id, lidoAte: null));
        Assert.Equal([doBruno], chamado.NaoLidasPor(Ana.Id, lidoAte: null));
        Assert.False(chamado.NaoLida(doBruno, Bruno.Id, lidoAte: null));
        Assert.True(chamado.NaoLida(daAna, Bruno.Id, lidoAte: null));
    }

    [Fact]
    public void Depois_da_leitura_so_contam_as_mensagens_posteriores_a_ela()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Inicio, abertoPor: Bruno);
        var primeira = chamado.Comentar("Chego às 10h.", Inicio.AddMinutes(1), Ana);
        var segunda = chamado.Comentar("Atraso de 5 minutos.", Inicio.AddMinutes(2), Ana);
        var terceira = chamado.Comentar("Cheguei.", Inicio.AddMinutes(3), Ana);

        // Lido até a segunda (inclusive): só a terceira é nova.
        Assert.Equal([terceira], chamado.NaoLidasPor(Bruno.Id, segunda.Em));
        Assert.False(chamado.NaoLida(segunda, Bruno.Id, segunda.Em));
        Assert.False(chamado.NaoLida(primeira, Bruno.Id, segunda.Em));
        Assert.Empty(chamado.NaoLidasPor(Bruno.Id, terceira.Em));
    }

    [Fact]
    public void Quem_nao_acompanha_a_conversa_nao_tem_mensagem_nova()
    {
        var chamado = new Chamado("Tomada solta", criadoEm: Inicio, abertoPor: Bruno);
        var mensagem = chamado.Comentar("Alguém viu?", Inicio.AddMinutes(1), Bruno);

        Assert.Empty(chamado.NaoLidasPor(Davi.Id, lidoAte: null));
        Assert.False(chamado.NaoLida(mensagem, Davi.Id, lidoAte: null));
    }

    // --- O serviço ---

    [Fact]
    public async Task Quem_escreve_le_a_conversa_ate_a_propria_mensagem()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));
        await ComentarAsync(chamado.Id, "Chego às 10h.", Ana);
        var resposta = await ComentarAsync(chamado.Id, "Combinado.", Bruno);

        Assert.Equal(resposta.Em, await _servico.ObterLeituraAsync(chamado.Id, Bruno.Id));
        Assert.Equal(0, (await ResumoParaAsync(chamado.Id, Bruno)).ComentariosNaoLidos);
        Assert.Equal(1, (await ResumoParaAsync(chamado.Id, Ana)).ComentariosNaoLidos);
    }

    [Fact]
    public async Task Marcar_como_lida_vai_ate_a_ultima_mensagem_e_a_leitura_nao_volta_atras()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));
        var primeira = await ComentarAsync(chamado.Id, "Chego às 10h.", Ana);
        var segunda = await ComentarAsync(chamado.Id, "Atraso de 5 minutos.", Ana);

        await _servico.MarcarComoLidaAsync((await _servico.ObterAsync(chamado.Id))!, Bruno.Id);
        var depoisDeLer = await _servico.ObterLeituraAsync(chamado.Id, Bruno.Id);

        // Uma aba antiga, aberta quando só havia a primeira mensagem, grava a leitura dela depois.
        await _repositorio.MarcarLeituraAsync(chamado.Id, Bruno.Id, primeira.Em);

        Assert.Equal(segunda.Em, depoisDeLer);
        Assert.Equal(segunda.Em, await _servico.ObterLeituraAsync(chamado.Id, Bruno.Id));
        Assert.Equal(0, (await ResumoParaAsync(chamado.Id, Bruno)).ComentariosNaoLidos);
    }

    [Fact]
    public async Task Marcar_como_lida_sem_mensagens_nao_grava_leitura()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));

        await _servico.MarcarComoLidaAsync(chamado, Bruno.Id);

        Assert.Null(await _servico.ObterLeituraAsync(chamado.Id, Bruno.Id));
    }

    [Fact]
    public async Task Lista_conta_as_nao_lidas_do_leitor_e_o_filtro_deixa_so_os_chamados_com_alguma()
    {
        var comResposta = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));
        await ComentarAsync(comResposta.Id, "Chego às 10h.", Ana);
        await ComentarAsync(comResposta.Id, "Leve uma escada.", Ana);
        var semResposta = await _servico.AbrirAsync(new NovoChamado("Lâmpada queimada", AbertoPor: Bruno));
        await ComentarAsync(semResposta.Id, "É a do corredor.", Bruno);

        var todos = await _servico.ListarAsync(new FiltroChamados(LeitorId: Bruno.Id));
        var comNovas = await _servico.ListarAsync(new FiltroChamados(LeitorId: Bruno.Id, SomenteComNaoLidas: true));

        Assert.Equal(2, todos.Single(c => c.Id == comResposta.Id).ComentariosNaoLidos);
        Assert.Equal(0, todos.Single(c => c.Id == semResposta.Id).ComentariosNaoLidos);
        Assert.Equal(comResposta.Id, Assert.Single(comNovas).Id);
        Assert.Equal(1, await _servico.ContarComNaoLidasAsync(Bruno.Id));
    }

    [Fact]
    public async Task Sem_leitor_o_resumo_nao_traz_contagem_e_o_filtro_nao_traz_nenhum_chamado()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));
        await ComentarAsync(chamado.Id, "Chego às 10h.", Ana);

        var resumo = Assert.Single(await _servico.ListarAsync(FiltroChamados.Todos));

        Assert.Null(resumo.ComentariosNaoLidos);
        Assert.Empty(await _servico.ListarAsync(new FiltroChamados(SomenteComNaoLidas: true)));
    }

    [Fact]
    public async Task Contagem_do_cabecalho_respeita_o_dono_e_so_vale_para_quem_acompanha()
    {
        var doBruno = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));
        await _servico.IniciarAtendimentoAsync(doBruno.Id, Ana, Ana);
        await ComentarAsync(doBruno.Id, "Está faiscando.", Bruno);
        var daCarla = await _servico.AbrirAsync(new NovoChamado("Wi-Fi instável", AbertoPor: Carla));
        await _servico.IniciarAtendimentoAsync(daCarla.Id, Ana, Ana);
        await ComentarAsync(daCarla.Id, "Cai a cada 15 minutos.", Carla);

        // A Ana atende os dois; o Davi não acompanha nenhum. Com dono, só entre os chamados dele.
        Assert.Equal(2, await _servico.ContarComNaoLidasAsync(Ana.Id));
        Assert.Equal(1, await _servico.ContarComNaoLidasAsync(Ana.Id, abertoPorId: Bruno.Id));
        Assert.Equal(0, await _servico.ContarComNaoLidasAsync(Davi.Id));
    }

    [Fact]
    public async Task Leitura_de_chamado_inexistente_e_recusada()
    {
        await Assert.ThrowsAsync<ChamadoNaoEncontradoException>(
            () => _repositorio.MarcarLeituraAsync(Guid.NewGuid(), Bruno.Id, Inicio));
    }

    // Cada mensagem um minuto depois da anterior, como numa conversa de verdade.
    private async Task<Comentario> ComentarAsync(Guid id, string texto, Autor autor)
    {
        _relogio.Avancar(TimeSpan.FromMinutes(1));
        return await _servico.ComentarAsync(id, texto, autor);
    }

    private async Task<ResumoChamado> ResumoParaAsync(Guid id, Autor leitor)
        => (await _servico.ListarAsync(new FiltroChamados(LeitorId: leitor.Id))).Single(c => c.Id == id);
}
