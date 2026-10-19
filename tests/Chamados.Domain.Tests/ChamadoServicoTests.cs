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
}