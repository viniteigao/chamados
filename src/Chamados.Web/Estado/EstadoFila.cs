using Chamados.Domain;

namespace Chamados.Web.Estado;

/// <summary>
/// Estado compartilhado entre componentes que não são pai e filho: a página de
/// atendimento altera os chamados, o resumo do cabeçalho mostra os totais.
/// Registrado como Scoped: no Blazor Server, uma instância por circuito (por aba).
/// </summary>
public sealed class EstadoFila(ChamadoServico servico, ILogger<EstadoFila> logger)
{
    /// <summary>Totais da última atualização bem-sucedida; null enquanto não houver nenhuma.</summary>
    public EstatisticasChamados? Estatisticas { get; private set; }

    /// <summary>Avisa os componentes inscritos que os totais mudaram.</summary>
    public event Action? Mudou;

    public int Contar(StatusChamado status)
        => Estatisticas?.PorStatus.GetValueOrDefault(status) ?? 0;

    public async Task AtualizarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Estatisticas = await servico.ObterEstatisticasAsync(cancellationToken);
            Mudou?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Os totais são informação de apoio: numa falha, ficam os últimos conhecidos.
            logger.LogWarning(ex, "Não foi possível atualizar os totais da fila.");
        }
    }
}
