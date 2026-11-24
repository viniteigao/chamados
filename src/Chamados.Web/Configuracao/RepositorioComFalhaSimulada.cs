using Chamados.Domain;

namespace Chamados.Web.Configuracao;

/// <summary>
/// Envolve o repositório real e faz a listagem falhar de tempos em tempos.
/// Existe só para o laboratório: ver a fila tratando uma falha sem derrubar a tela.
/// Ligado por Simulacao:FalhaNaListagemACada no appsettings.Development.json.
/// </summary>
public sealed class RepositorioComFalhaSimulada(IChamadoRepositorio interno, int aCada) : IChamadoRepositorio
{
    private int _listagens;

    public Task<IReadOnlyList<Chamado>> ListarAsync(CancellationToken cancellationToken = default)
    {
        // Interlocked: várias abas (circuitos) listam ao mesmo tempo no mesmo singleton.
        if (Interlocked.Increment(ref _listagens) % aCada == 0)
        {
            throw new InvalidOperationException(
                "Falha simulada na listagem (Simulacao:FalhaNaListagemACada).");
        }

        return interno.ListarAsync(cancellationToken);
    }

    // O resto só repassa ao repositório real.
    public Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
        => interno.ObterAsync(id, cancellationToken);

    public Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default)
        => interno.AdicionarAsync(chamado, cancellationToken);

    public Task AtualizarAsync(Chamado chamado, CancellationToken cancellationToken = default)
        => interno.AtualizarAsync(chamado, cancellationToken);
}
