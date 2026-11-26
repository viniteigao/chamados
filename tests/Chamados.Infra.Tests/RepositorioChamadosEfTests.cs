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

    [Fact]
    public async Task Migrations_estao_em_dia_com_o_modelo()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        await using var db = banco.CreateDbContext();

        // Falha se alguém mudou o mapeamento e esqueceu de criar a migration.
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
