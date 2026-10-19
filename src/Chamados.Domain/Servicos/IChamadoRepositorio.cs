namespace Chamados.Domain;

/// <summary>
/// Contrato de armazenamento de chamados. Existe porque a implementação
/// vai mudar: hoje em memória, no Módulo 7 com Entity Framework Core.
/// </summary>
public interface IChamadoRepositorio
{
    Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chamado>> ListarAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Chamado chamado, CancellationToken cancellationToken = default);
}