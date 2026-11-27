using Chamados.Domain;

namespace Chamados.Domain.Tests;

public class ChamadoServicoTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly RelogioFixo _relogio = new(Inicio);
    private readonly ChamadoServico _servico;

    public ChamadoServicoTests()
    {
        // xUnit cria uma instância da classe por teste: cada teste tem seu próprio repositório.
        _servico = new ChamadoServico(new RepositorioChamadosEmMemoria(_relogio), _relogio);
    }

    [Fact]
    public async Task AbrirAsync_UsaHorarioDoRelogio()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta"));

        Assert.Equal(Inicio, chamado.CriadoEm);
    }

    [Fact]
    public async Task ListarAsync_OrdenaPorPrioridadeDecrescenteEDepoisPorAbertura()
    {
        await _servico.AbrirAsync(new NovoChamado("A", Prioridade.Baixa));
        _relogio.Avancar(TimeSpan.FromMinutes(1));
        await _servico.AbrirAsync(new NovoChamado("B", Prioridade.Alta));
        _relogio.Avancar(TimeSpan.FromMinutes(1));
        await _servico.AbrirAsync(new NovoChamado("C", Prioridade.Alta));

        var resultado = await _servico.ListarAsync(FiltroChamados.Todos);

        Assert.Equal(new[] { "B", "C", "A" }, resultado.Select(r => r.Titulo));
    }

    [Fact]
    public async Task ListarAsync_FiltraPorTextoNaDescricaoSemDiferenciarMaiusculas()
    {
        await _servico.AbrirAsync(new NovoChamado("Ar-condicionado", Prioridade.Alta, "Desliga na SALA 204"));
        await _servico.AbrirAsync(new NovoChamado("Lâmpada", Prioridade.Baixa));

        var resultado = await _servico.ListarAsync(new FiltroChamados(Texto: "sala"));

        Assert.Equal("Ar-condicionado", Assert.Single(resultado).Titulo);
    }

    [Fact]
    public async Task ListarAsync_CombinaStatusEPrioridadeMinima()
    {
        var alta = await _servico.AbrirAsync(new NovoChamado("Alta em atendimento", Prioridade.Alta));
        await _servico.IniciarAtendimentoAsync(alta.Id, "Equipe");
        var baixa = await _servico.AbrirAsync(new NovoChamado("Baixa em atendimento", Prioridade.Baixa));
        await _servico.IniciarAtendimentoAsync(baixa.Id, "Equipe");
        await _servico.AbrirAsync(new NovoChamado("Alta aberta", Prioridade.Alta));

        var filtro = new FiltroChamados(Status: StatusChamado.EmAtendimento, PrioridadeMinima: Prioridade.Media);
        var resultado = await _servico.ListarAsync(filtro);

        Assert.Equal("Alta em atendimento", Assert.Single(resultado).Titulo);
    }

    [Fact]
    public async Task ObterEstatisticasAsync_CalculaTotaisETempoMedio()
    {
        var c1 = await _servico.AbrirAsync(new NovoChamado("C1", Prioridade.Alta));
        var c2 = await _servico.AbrirAsync(new NovoChamado("C2", Prioridade.Alta));
        await _servico.AbrirAsync(new NovoChamado("C3", Prioridade.Baixa));

        await _servico.IniciarAtendimentoAsync(c1.Id, "Equipe");
        await _servico.IniciarAtendimentoAsync(c2.Id, "Equipe");
        _relogio.Avancar(TimeSpan.FromHours(1));
        await _servico.ResolverAsync(c1.Id, "Feito");
        _relogio.Avancar(TimeSpan.FromHours(2));
        await _servico.ResolverAsync(c2.Id, "Feito");

        var estatisticas = await _servico.ObterEstatisticasAsync();

        Assert.Equal(3, estatisticas.Total);
        Assert.Equal(2, estatisticas.PorStatus[StatusChamado.Resolvido]);
        Assert.Equal(0, estatisticas.PorStatus[StatusChamado.Cancelado]);
        Assert.Equal(2, estatisticas.PorPrioridade[Prioridade.Alta]);
        Assert.False(estatisticas.PorPrioridade.ContainsKey(Prioridade.Critica));
        Assert.Equal(TimeSpan.FromHours(2), estatisticas.TempoMedioResolucao); // (1h + 3h) / 2
    }

    [Fact]
    public async Task ObterEstatisticasAsync_SemResolvidos_TempoMedioNulo()
    {
        await _servico.AbrirAsync(new NovoChamado("C1"));

        var estatisticas = await _servico.ObterEstatisticasAsync();

        Assert.Null(estatisticas.TempoMedioResolucao);
    }

    [Fact]
    public async Task IniciarAtendimentoAsync_IdInexistente_LancaChamadoNaoEncontrado()
    {
        var id = Guid.NewGuid();

        var erro = await Assert.ThrowsAsync<ChamadoNaoEncontradoException>(
            () => _servico.IniciarAtendimentoAsync(id, "Equipe"));

        Assert.Equal(id, erro.ChamadoId);
    }

    [Fact]
    public async Task ListarAsync_TokenCancelado_LancaOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _servico.ListarAsync(FiltroChamados.Todos, cts.Token));
    }

    [Fact]
    public void FiltroChamados_ComWith_CriaCopiaSemAlterarOriginal()
    {
        var original = new FiltroChamados(Status: StatusChamado.Aberto);

        var copia = original with { Texto = "sala" };

        Assert.Null(original.Texto);
        Assert.Equal(StatusChamado.Aberto, copia.Status);
        Assert.NotEqual(original, copia);
        Assert.Equal(new FiltroChamados(Status: StatusChamado.Aberto), original);
    }

    [Fact]
    public async Task ObterAsync_IdExistente_DevolveOChamado()
    {
        var aberto = await _servico.AbrirAsync(new NovoChamado("Tomada solta"));

        var encontrado = await _servico.ObterAsync(aberto.Id);

        Assert.Same(aberto, encontrado);
    }

    [Fact]
    public async Task ObterAsync_IdInexistente_DevolveNull()
    {
        Assert.Null(await _servico.ObterAsync(Guid.NewGuid()));
    }

    // --- Módulo 4: edição com concorrência otimista ---

    [Fact]
    public async Task EditarAsync_ComVersaoCorreta_Edita()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta"));

        var editado = await _servico.EditarAsync(chamado.Id, new EdicaoChamado("Tomada solta na recepção", Prioridade.Alta, null, VersaoEsperada: 1));

        Assert.Equal("Tomada solta na recepção", editado.Titulo);
        Assert.Equal(2, editado.Versao);
    }

    [Fact]
    public async Task EditarAsync_ComVersaoDesatualizada_LancaConcorrenciaSemAlterar()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta"));
        await _servico.IniciarAtendimentoAsync(chamado.Id, "Elétrica"); // outra pessoa agiu: versão 2

        var erro = await Assert.ThrowsAsync<ConcorrenciaException>(
            () => _servico.EditarAsync(chamado.Id, new EdicaoChamado("Outro título", Prioridade.Alta, null, VersaoEsperada: 1)));

        Assert.Equal(1, erro.VersaoEsperada);
        Assert.Equal(2, erro.VersaoAtual);
        Assert.Equal("Tomada solta", chamado.Titulo);
    }

    [Fact]
    public async Task EditarAsync_IdInexistente_LancaChamadoNaoEncontrado()
    {
        await Assert.ThrowsAsync<ChamadoNaoEncontradoException>(
            () => _servico.EditarAsync(Guid.NewGuid(), new EdicaoChamado("Título", Prioridade.Media, null, 1)));
    }

    // --- Módulo 4: paginação e ordenação ---

    private async Task AbrirVariosAsync(int quantidade)
    {
        for (var i = 1; i <= quantidade; i++)
        {
            await _servico.AbrirAsync(new NovoChamado($"Chamado {i:00}"));
            _relogio.Avancar(TimeSpan.FromMinutes(1));
        }
    }

    [Fact]
    public async Task ListarPaginaAsync_SegundaPagina_TrazOsItensSeguintes()
    {
        await AbrirVariosAsync(12);

        var pagina = await _servico.ListarPaginaAsync(FiltroChamados.Todos, numero: 2, tamanhoPagina: 5);

        Assert.Equal(new[] { "Chamado 06", "Chamado 07", "Chamado 08", "Chamado 09", "Chamado 10" }, pagina.Itens.Select(c => c.Titulo));
        Assert.Equal(12, pagina.TotalItens);
        Assert.Equal(3, pagina.TotalPaginas);
        Assert.True(pagina.TemAnterior);
        Assert.True(pagina.TemProxima);
    }

    [Theory]
    [InlineData(99, 3)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task ListarPaginaAsync_NumeroForaDoIntervalo_AjustaParaPaginaValida(int pedido, int esperado)
    {
        await AbrirVariosAsync(12);

        var pagina = await _servico.ListarPaginaAsync(FiltroChamados.Todos, pedido, tamanhoPagina: 5);

        Assert.Equal(esperado, pagina.Numero);
    }

    [Fact]
    public async Task ListarPaginaAsync_SemResultados_TemUmaPaginaVazia()
    {
        var pagina = await _servico.ListarPaginaAsync(FiltroChamados.Todos, 1, 5);

        Assert.Empty(pagina.Itens);
        Assert.Equal(1, pagina.TotalPaginas);
        Assert.False(pagina.TemProxima);
    }

    [Fact]
    public async Task ListarAsync_OrdemPorTitulo_IgnoraAcentoEMaiusculas()
    {
        await _servico.AbrirAsync(new NovoChamado("bebedouro"));
        await _servico.AbrirAsync(new NovoChamado("Água na copa"));
        await _servico.AbrirAsync(new NovoChamado("Ar-condicionado"));

        var lista = await _servico.ListarAsync(FiltroChamados.Todos with { Ordem = OrdemChamados.Titulo });

        Assert.Equal(new[] { "Água na copa", "Ar-condicionado", "bebedouro" }, lista.Select(c => c.Titulo));
    }

    [Fact]
    public async Task ListarAsync_OrdemMaisRecentes_ComecaPeloUltimoAberto()
    {
        await AbrirVariosAsync(3);

        var lista = await _servico.ListarAsync(new FiltroChamados(Ordem: OrdemChamados.MaisRecentes));

        Assert.Equal("Chamado 03", lista[0].Titulo);
    }

    // --- Prazos ---

    [Fact]
    public async Task ListarAsync_SomenteVencidos_TrazSoOsEmAndamentoComPrazoVencido()
    {
        var vencido = await _servico.AbrirAsync(new NovoChamado("Porta não tranca", Prioridade.Critica));
        var encerrado = await _servico.AbrirAsync(new NovoChamado("Lâmpada queimada", Prioridade.Critica));
        await _servico.AbrirAsync(new NovoChamado("Cadeira quebrada", Prioridade.Baixa));
        await _servico.IniciarAtendimentoAsync(encerrado.Id, "Elétrica");
        _relogio.Avancar(TimeSpan.FromHours(5));                    // as Críticas venceram; a Baixa tem dias
        await _servico.ResolverAsync(encerrado.Id, "Trocada");     // fora do prazo, mas encerrado: não entra
        await _servico.AbrirAsync(new NovoChamado("Vazamento", Prioridade.Critica));   // aberto agora

        var vencidos = await _servico.ListarAsync(new FiltroChamados(SomenteVencidos: true));

        Assert.Equal(new[] { vencido.Id }, vencidos.Select(c => c.Id));
    }

    [Fact]
    public async Task ObterIndicadoresDePrazoAsync_ContaCadaSituacao()
    {
        // Linha do tempo (Crítica = 4 h), avaliada 5 h depois do início:
        var vencido = await _servico.AbrirAsync(new NovoChamado("Aberto há 5 h", Prioridade.Critica));
        var cumprido = await _servico.AbrirAsync(new NovoChamado("Resolvido em 1 h", Prioridade.Critica));
        var atrasado = await _servico.AbrirAsync(new NovoChamado("Resolvido em 5 h", Prioridade.Critica));
        await _servico.IniciarAtendimentoAsync(cumprido.Id, "Elétrica");
        await _servico.IniciarAtendimentoAsync(atrasado.Id, "Hidráulica");
        _relogio.Avancar(TimeSpan.FromHours(1));
        await _servico.ResolverAsync(cumprido.Id, "Feito");
        _relogio.Avancar(TimeSpan.FromMinutes(30));
        await _servico.AbrirAsync(new NovoChamado("Aberto há 3 h 30 min", Prioridade.Critica));   // faltam 30 min
        _relogio.Avancar(TimeSpan.FromMinutes(210));
        await _servico.ResolverAsync(atrasado.Id, "Feito");
        await _servico.AbrirAsync(new NovoChamado("Aberto agora", Prioridade.Baixa));

        var indicadores = await _servico.ObterIndicadoresDePrazoAsync();

        Assert.Equal(1, indicadores.Vencidos);
        Assert.Equal(1, indicadores.PertoDeVencer);
        Assert.Equal(1, indicadores.ResolvidosNoPrazo);
        Assert.Equal(1, indicadores.ResolvidosForaDoPrazo);
        Assert.Equal(50.0, indicadores.PercentualNoPrazo);
        Assert.Equal(TimeSpan.FromHours(4), indicadores.Prazos[Prioridade.Critica]);
        Assert.Equal(SituacaoPrazo.Vencido, _servico.PrazoDe(vencido)!.Situacao);
    }

    [Fact]
    public async Task ObterIndicadoresDePrazoAsync_SemResolvidos_NaoInventaPercentual()
    {
        await _servico.AbrirAsync(new NovoChamado("Tomada solta"));

        var indicadores = await _servico.ObterIndicadoresDePrazoAsync();

        Assert.Null(indicadores.PercentualNoPrazo);
    }

    [Fact]
    public async Task PrazoDe_UsaAPoliticaRecebidaPorInjecao()
    {
        var politica = new PoliticaDePrazos(new Dictionary<Prioridade, TimeSpan>
        {
            [Prioridade.Critica] = TimeSpan.FromHours(1),
            [Prioridade.Alta] = TimeSpan.FromHours(2),
            [Prioridade.Media] = TimeSpan.FromHours(3),
            [Prioridade.Baixa] = TimeSpan.FromHours(4),
        });
        var servico = new ChamadoServico(new RepositorioChamadosEmMemoria(_relogio), _relogio, prazos: politica);
        var chamado = await servico.AbrirAsync(new NovoChamado("Porta não tranca", Prioridade.Critica));

        _relogio.Avancar(TimeSpan.FromMinutes(90));

        Assert.Equal(SituacaoPrazo.Vencido, servico.PrazoDe(chamado)!.Situacao);
        Assert.Equal(SituacaoPrazo.NoPrazo, _servico.PrazoDe(chamado)!.Situacao);   // a padrão dá 4 h
    }

    // --- Meus chamados ---

    private static readonly Autor Bruno = new("id-bruno", "bruno@chamados.local");
    private static readonly Autor Carla = new("id-carla", "carla@chamados.local");
    private static readonly Autor Ana = new("id-ana", "ana@chamados.local");

    [Fact]
    public async Task ListarAsync_ComDono_TrazSoOsChamadosQueEleAbriu()
    {
        var doBruno = await _servico.AbrirAsync(new NovoChamado("Cadeira quebrada", AbertoPor: Bruno));
        await _servico.AbrirAsync(new NovoChamado("Wi-Fi instável", AbertoPor: Carla));
        await _servico.AbrirAsync(new NovoChamado("Sem dono"));

        var lista = await _servico.ListarAsync(new FiltroChamados(AbertoPorId: Bruno.Id));

        Assert.Equal(new[] { doBruno.Id }, lista.Select(c => c.Id));
        Assert.Equal("bruno@chamados.local", lista[0].AbertoPorNome);
    }

    [Fact]
    public async Task ObterEstatisticasAsync_ComDono_ContaSoOsDele()
    {
        var doBruno = await _servico.AbrirAsync(new NovoChamado("Cadeira quebrada", AbertoPor: Bruno));
        await _servico.AbrirAsync(new NovoChamado("Bebedouro sem água", AbertoPor: Bruno));
        await _servico.AbrirAsync(new NovoChamado("Wi-Fi instável", AbertoPor: Carla));
        await _servico.IniciarAtendimentoAsync(doBruno.Id, "Elétrica", Ana);

        var doBrunoApenas = await _servico.ObterEstatisticasAsync(Bruno.Id);
        var todas = await _servico.ObterEstatisticasAsync();

        Assert.Equal(2, doBrunoApenas.Total);
        Assert.Equal(1, doBrunoApenas.PorStatus[StatusChamado.EmAtendimento]);
        Assert.Equal(3, todas.Total);
    }

    [Fact]
    public async Task Transicoes_DoServico_RegistramQuemFez()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));

        await _servico.IniciarAtendimentoAsync(chamado.Id, "Elétrica", Ana);
        var cancelado = await _servico.CancelarAsync(chamado.Id, "Duplicado", Ana);

        Assert.All(cancelado.Historico, r => Assert.Equal(Ana, r.Por));
        Assert.Equal(Bruno, cancelado.AbertoPor);
    }

    [Fact]
    public async Task ListarPorResponsavelAsync_ComDono_FiltraOsDele()
    {
        var doBruno = await _servico.AbrirAsync(new NovoChamado("Cadeira quebrada", AbertoPor: Bruno));
        var daCarla = await _servico.AbrirAsync(new NovoChamado("Wi-Fi instável", AbertoPor: Carla));
        await _servico.IniciarAtendimentoAsync(doBruno.Id, "Redes", Ana);
        await _servico.IniciarAtendimentoAsync(daCarla.Id, "Redes", Ana);

        var doBrunoComRedes = await _servico.ListarPorResponsavelAsync("Redes", Bruno.Id);
        var todosComRedes = await _servico.ListarPorResponsavelAsync("Redes");

        Assert.Equal(new[] { doBruno.Id }, doBrunoComRedes.Select(c => c.Id));
        Assert.Equal(2, todosComRedes.Count);
    }

    // --- Comentários ---

    [Fact]
    public async Task ComentarAsync_UsaAHoraDoRelogio_EApareceNoChamadoENaContagemDaLista()
    {
        var chamado = await _servico.AbrirAsync(new NovoChamado("Tomada solta", AbertoPor: Bruno));
        _relogio.Avancar(TimeSpan.FromMinutes(42));

        var comentario = await _servico.ComentarAsync(chamado.Id, "Faísca ao ligar", Bruno);
        var lido = await _servico.ObterAsync(chamado.Id);
        var resumo = Assert.Single(await _servico.ListarAsync(FiltroChamados.Todos));

        Assert.Equal(Inicio.AddMinutes(42), comentario.Em);
        Assert.Equal(Bruno, comentario.Autor);
        Assert.Same(comentario, Assert.Single(lido!.Comentarios));
        Assert.Equal(1, resumo.TotalComentarios);
    }

    [Fact]
    public async Task ComentarAsync_ChamadoInexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<ChamadoNaoEncontradoException>(() => _servico.ComentarAsync(Guid.NewGuid(), "Oi", Bruno));
    }
}
