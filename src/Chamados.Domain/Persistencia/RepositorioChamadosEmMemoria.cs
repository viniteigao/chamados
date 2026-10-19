using System.Collections.Concurrent;

namespace Chamados.Domain;

/// <summary>
/// Armazenamento provisório em memória, perdido ao reiniciar a aplicação.
/// Será substituído pelo Entity Framework Core no Módulo 7.
/// A latência simulada (só na listagem) existe para observar assincronismo e cancelamento.
/// </summary>
public sealed class RepositorioChamadosEmMemoria(TimeProvider relogio, TimeSpan latenciaSimulada = default)
    : IChamadoRepositorio
{
    private readonly ConcurrentDictionary<Guid, Chamado> _chamados = new();

    public Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_chamados.GetValueOrDefault(id));
    }

    public async Task<IReadOnlyList<Chamado>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        return [.. _chamados.Values];
    }

    public Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_chamados.TryAdd(chamado.Id, chamado))
        {
            throw new InvalidOperationException($"Já existe um chamado com o Id {chamado.Id}.");
        }

        return Task.CompletedTask;
    }

    public Task AtualizarAsync(Chamado chamado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_chamados.ContainsKey(chamado.Id))
        {
            throw new ChamadoNaoEncontradoException(chamado.Id);
        }

        _chamados[chamado.Id] = chamado;
        return Task.CompletedTask;
    }

    private Task SimularLatenciaAsync(CancellationToken cancellationToken)
    {
        if (latenciaSimulada > TimeSpan.Zero)
        {
            // Task.Delay com TimeProvider: em testes, um relógio falso controla a espera.
            return Task.Delay(latenciaSimulada, relogio, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}