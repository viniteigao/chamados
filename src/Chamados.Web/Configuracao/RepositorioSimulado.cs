using Chamados.Domain;

namespace Chamados.Web.Configuracao;

/// <summary>
/// Envolve o repositório real (o do banco, desde o Módulo 7) e acrescenta, só para o laboratório,
/// latência artificial nas consultas e falhas propositais na listagem.
/// Ligado por Simulacao:LatenciaMs e Simulacao:FalhaNaListagemACada (Development).
/// </summary>
public sealed class RepositorioSimulado(IChamadoRepositorio interno, TimeProvider relogio, SimulacaoOptions opcoes)
    : IChamadoRepositorio
{
    private int _listagens;

    public async Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro, int pular = 0, int? pegar = null, CancellationToken cancellationToken = default)
    {
        await AtrasarAsync(cancellationToken);
        FalharSePedido();
        return await interno.ListarAsync(filtro, pular, pegar, cancellationToken);
    }

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(CancellationToken cancellationToken = default)
    {
        await AtrasarAsync(cancellationToken);
        FalharSePedido();
        return await interno.ObterEstatisticasAsync(cancellationToken);
    }

    public async Task<int> ContarAsync(FiltroChamados filtro, CancellationToken cancellationToken = default)
    {
        await AtrasarAsync(cancellationToken);
        return await interno.ContarAsync(filtro, cancellationToken);
    }

    // Leituras e gravações de um chamado só repassam ao repositório real.
    public Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
        => interno.ObterAsync(id, cancellationToken);

    public Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default)
        => interno.AdicionarAsync(chamado, cancellationToken);

    public Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken = default)
        => interno.AlterarAsync(id, alteracao, cancellationToken);

    private Task AtrasarAsync(CancellationToken cancellationToken)
        => opcoes.LatenciaMs > 0
            ? Task.Delay(TimeSpan.FromMilliseconds(opcoes.LatenciaMs), relogio, cancellationToken)
            : Task.CompletedTask;

    private void FalharSePedido()
    {
        // Interlocked: várias abas (circuitos) consultam ao mesmo tempo no mesmo singleton.
        if (opcoes.FalhaNaListagemACada > 0
            && Interlocked.Increment(ref _listagens) % opcoes.FalhaNaListagemACada == 0)
        {
            throw new InvalidOperationException(
                "Falha simulada na listagem (Simulacao:FalhaNaListagemACada).");
        }
    }
}
