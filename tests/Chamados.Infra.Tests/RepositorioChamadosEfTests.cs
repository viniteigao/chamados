using Chamados.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Infra.Tests;

public class RepositorioChamadosEfTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Adicionar_e_obter_devolve_o_chamado_com_o_historico()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Projetor sem imagem", Prioridade.Alta, "Sala 12", Abertura);
        chamado.IniciarAtendimento("Ana", Abertura.AddMinutes(10));

        await repositorio.AdicionarAsync(chamado);
        var lido = await repositorio.ObterAsync(chamado.Id);

        Assert.NotNull(lido);
        Assert.NotSame(chamado, lido);   // veio do banco, não da memória
        Assert.Equal("Projetor sem imagem", lido.Titulo);
        Assert.Equal(StatusChamado.EmAtendimento, lido.Status);
        Assert.Equal(Abertura, lido.CriadoEm);
        Assert.Equal(2, lido.Versao);
        var registro = Assert.Single(lido.Historico);
        Assert.Equal("Atribuído a Ana", registro.Observacao);
    }

    [Fact]
    public async Task Alterar_grava_a_transicao_e_acrescenta_ao_historico()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        await repositorio.AdicionarAsync(chamado);

        await repositorio.AlterarAsync(chamado.Id, c => c.IniciarAtendimento("Bruno", Abertura.AddHours(1)));
        await repositorio.AlterarAsync(chamado.Id, c => c.Resolver("Tomada fixada", Abertura.AddHours(2)));

        var lido = await repositorio.ObterAsync(chamado.Id);
        Assert.Equal(StatusChamado.Resolvido, lido!.Status);
        Assert.Equal(3, lido.Versao);
        Assert.Equal(2, lido.Historico.Count);
        Assert.Equal(Abertura.AddHours(2), lido.EncerradoEm);
    }

    [Fact]
    public async Task Alterar_quando_outra_gravacao_chega_antes_lanca_concorrencia()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Wi-Fi instável", criadoEm: Abertura);
        await repositorio.AdicionarAsync(chamado);

        // Enquanto a primeira alteração está em andamento (já leu a versão 1),
        // outra pessoa inicia o atendimento e grava a versão 2.
        var erro = await Assert.ThrowsAsync<ConcorrenciaException>(() =>
            repositorio.AlterarAsync(chamado.Id, c =>
            {
                repositorio.AlterarAsync(chamado.Id, outro => outro.IniciarAtendimento("Carla", Abertura.AddMinutes(5)))
                    .GetAwaiter().GetResult();
                c.Cancelar("Duplicado", Abertura.AddMinutes(6));
            }));

        Assert.Equal(1, erro.VersaoEsperada);
        Assert.Equal(2, erro.VersaoAtual);
        var lido = await repositorio.ObterAsync(chamado.Id);
        Assert.Equal(StatusChamado.EmAtendimento, lido!.Status);   // a gravação de Carla ficou; o cancelamento, não
    }

    [Fact]
    public async Task Alterar_id_inexistente_lanca_nao_encontrado()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);

        await Assert.ThrowsAsync<ChamadoNaoEncontradoException>(
            () => repositorio.AlterarAsync(Guid.NewGuid(), c => c.Cancelar("x", Abertura)));
    }

    [Fact]
    public async Task Listar_filtra_ordena_e_pagina_no_banco()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        for (var i = 1; i <= 7; i++)
        {
            await repositorio.AdicionarAsync(new Chamado($"Chamado {i:00}", Prioridade.Media, criadoEm: Abertura.AddMinutes(i)));
        }
        await repositorio.AdicionarAsync(new Chamado("Porta não tranca", Prioridade.Critica, criadoEm: Abertura));

        var filtro = FiltroChamados.Todos with { Ordem = OrdemChamados.MaisRecentes };
        var pagina2 = await repositorio.ListarAsync(filtro, pular: 3, pegar: 3);
        var total = await repositorio.ContarAsync(filtro with { PrioridadeMinima = Prioridade.Alta });

        Assert.Equal(["Chamado 04", "Chamado 03", "Chamado 02"], pagina2.Select(c => c.Titulo));
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task Listar_trata_porcentagem_digitada_como_texto()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        await repositorio.AdicionarAsync(new Chamado("Bateria em 5% no nobreak", criadoEm: Abertura));
        // Sem o escape, "5%" viraria o padrão %5%%, "qualquer coisa com 5", e este também apareceria.
        await repositorio.AdicionarAsync(new Chamado("Sala 5 sem luz", criadoEm: Abertura));

        var resultado = await repositorio.ListarAsync(FiltroChamados.Todos with { Texto = "5%" });

        Assert.Equal("Bateria em 5% no nobreak", Assert.Single(resultado).Titulo);
    }

    [Fact]
    public async Task Listar_por_responsavel_trata_o_texto_como_valor_e_nao_como_sql()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura);
        chamado.IniciarAtendimento("O'Brien", Abertura.AddMinutes(5));   // apóstrofo de verdade no nome
        await repositorio.AdicionarAsync(chamado);
        await repositorio.AdicionarAsync(new Chamado("Cadeira quebrada", criadoEm: Abertura));

        var doOBrien = await repositorio.ListarPorResponsavelAsync("O'Brien");
        var injecao = await repositorio.ListarPorResponsavelAsync("x' OR '1'='1");

        // Com texto concatenado, o primeiro quebraria o SQL e o segundo devolveria todos os chamados.
        Assert.Equal("Tomada solta", Assert.Single(doOBrien).Titulo);
        Assert.Empty(injecao);
    }

    [Fact]
    public async Task Estatisticas_contam_no_banco_e_calculam_o_tempo_medio()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var resolvido = new Chamado("Lâmpada queimada", criadoEm: Abertura);
        resolvido.IniciarAtendimento("Elétrica", Abertura.AddMinutes(30));
        resolvido.Resolver("Trocada", Abertura.AddHours(2));
        await repositorio.AdicionarAsync(resolvido);
        await repositorio.AdicionarAsync(new Chamado("Cadeira quebrada", Prioridade.Baixa, criadoEm: Abertura));

        var estatisticas = await repositorio.ObterEstatisticasAsync();

        Assert.Equal(2, estatisticas.Total);
        Assert.Equal(1, estatisticas.PorStatus[StatusChamado.Resolvido]);
        Assert.Equal(0, estatisticas.PorStatus[StatusChamado.Cancelado]);
        Assert.Equal(TimeSpan.FromHours(2), estatisticas.TempoMedioResolucao);
    }

    // --- Prazos ---

    [Fact]
    public async Task Listar_so_vencidos_compara_datas_no_banco_com_um_corte_por_prioridade()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var politica = PoliticaDePrazos.Padrao;
        var agora = Abertura.AddDays(10);

        // Em cada prioridade: um aberto 1 minuto antes de vencer e um que venceu há 1 minuto.
        foreach (var prioridade in Enum.GetValues<Prioridade>())
        {
            var prazo = politica.PrazoDe(prioridade);
            await repositorio.AdicionarAsync(new Chamado($"{prioridade} vencido", prioridade,
                criadoEm: agora - prazo - TimeSpan.FromMinutes(1)));
            await repositorio.AdicionarAsync(new Chamado($"{prioridade} no prazo", prioridade,
                criadoEm: agora - prazo + TimeSpan.FromMinutes(1)));
        }

        // Aberto há 10 dias, mas resolvido: encerrado não está "vencido".
        var resolvido = new Chamado("Baixa resolvido", Prioridade.Baixa, criadoEm: Abertura);
        resolvido.IniciarAtendimento("Elétrica", Abertura.AddHours(1));
        resolvido.Resolver("Feito", Abertura.AddHours(2));
        await repositorio.AdicionarAsync(resolvido);

        var filtro = new FiltroChamados { AbertosAntesDe = politica.AberturasVencidas(agora) };
        var vencidos = await repositorio.ListarAsync(filtro);
        var total = await repositorio.ContarAsync(filtro);

        Assert.Equal(4, total);
        Assert.Equal(4, vencidos.Count);
        Assert.All(vencidos, c => Assert.EndsWith(" vencido", c.Titulo, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Listar_resolucoes_traz_so_os_resolvidos_e_a_lista_leva_a_data_de_encerramento()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var resolvido = new Chamado("Projetor sem imagem", Prioridade.Alta, criadoEm: Abertura);
        resolvido.IniciarAtendimento("Ana", Abertura.AddHours(1));
        resolvido.Resolver("Cabo trocado", Abertura.AddHours(2));
        var cancelado = new Chamado("Duplicado", criadoEm: Abertura);
        cancelado.Cancelar("Aberto em duplicidade", Abertura.AddHours(1));
        await repositorio.AdicionarAsync(resolvido);
        await repositorio.AdicionarAsync(cancelado);
        await repositorio.AdicionarAsync(new Chamado("Ainda aberto", criadoEm: Abertura));

        var resolucao = Assert.Single(await repositorio.ListarResolucoesAsync());
        var resumo = Assert.Single(await repositorio.ListarAsync(FiltroChamados.Todos with { Status = StatusChamado.Resolvido }));

        Assert.Equal(Prioridade.Alta, resolucao.Prioridade);
        Assert.Equal(Abertura, resolucao.CriadoEm);
        Assert.Equal(Abertura.AddHours(2), resolucao.ResolvidoEm);
        Assert.Equal(Abertura.AddHours(2), resumo.EncerradoEm);
    }

    // --- Meus chamados ---

    private static readonly Autor Bruno = new("id-bruno", "bruno@chamados.local");
    private static readonly Autor Carla = new("id-carla", "carla@chamados.local");
    private static readonly Autor Ana = new("id-ana", "ana@chamados.local");

    [Fact]
    public async Task Dono_e_autores_do_historico_sao_gravados_e_lidos_do_banco()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Cadeira quebrada", criadoEm: Abertura, abertoPor: Bruno);
        await repositorio.AdicionarAsync(chamado);

        await repositorio.AlterarAsync(chamado.Id, c => c.IniciarAtendimento("Elétrica", Abertura.AddHours(1), Ana));
        var lido = await repositorio.ObterAsync(chamado.Id);
        var resumo = Assert.Single(await repositorio.ListarAsync(FiltroChamados.Todos));

        Assert.Equal(Bruno, lido!.AbertoPor);
        Assert.Equal(Ana, Assert.Single(lido.Historico).Por);
        Assert.Equal("bruno@chamados.local", resumo.AbertoPorNome);
    }

    [Fact]
    public async Task Filtros_por_dono_viram_sql_na_lista_nas_estatisticas_e_na_busca_por_responsavel()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var doBruno = new Chamado("Cadeira quebrada", Prioridade.Baixa, criadoEm: Abertura, abertoPor: Bruno);
        doBruno.IniciarAtendimento("Redes", Abertura.AddHours(1), Ana);
        var daCarla = new Chamado("Wi-Fi instável", Prioridade.Alta, criadoEm: Abertura, abertoPor: Carla);
        daCarla.IniciarAtendimento("Redes", Abertura.AddHours(1), Ana);
        await repositorio.AdicionarAsync(doBruno);
        await repositorio.AdicionarAsync(daCarla);
        await repositorio.AdicionarAsync(new Chamado("Sem dono", criadoEm: Abertura));

        var lista = await repositorio.ListarAsync(FiltroChamados.Todos with { AbertoPorId = Bruno.Id });
        var total = await repositorio.ContarAsync(FiltroChamados.Todos with { AbertoPorId = Bruno.Id });
        var estatisticas = await repositorio.ObterEstatisticasAsync(Bruno.Id);
        var porResponsavel = await repositorio.ListarPorResponsavelAsync("Redes", Bruno.Id);

        Assert.Equal("Cadeira quebrada", Assert.Single(lista).Titulo);
        Assert.Equal(1, total);
        Assert.Equal(1, estatisticas.Total);
        Assert.Equal(1, estatisticas.PorPrioridade[Prioridade.Baixa]);
        Assert.Equal(doBruno.Id, Assert.Single(porResponsavel).Id);
    }

    [Fact]
    public async Task Migrations_estao_em_dia_com_o_modelo()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        await using var db = banco.CreateDbContext();

        // Falha se alguém mudou o mapeamento e esqueceu de criar a migration.
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
