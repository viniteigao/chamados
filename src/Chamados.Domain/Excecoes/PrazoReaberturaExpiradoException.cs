namespace Chamados.Domain;

/// <summary>
/// Tentativa de reabrir um chamado resolvido depois do prazo de reabertura.
/// Carrega o limite para que a tela ou a API possam informá-lo.
/// </summary>
public sealed class PrazoReaberturaExpiradoException(Guid chamadoId, DateTimeOffset limite)
    : InvalidOperationException($"O chamado {chamadoId} só podia ser reaberto até {limite:dd/MM/yyyy HH:mm} (UTC).")
{
    public Guid ChamadoId { get; } = chamadoId;

    public DateTimeOffset Limite { get; } = limite;
}
