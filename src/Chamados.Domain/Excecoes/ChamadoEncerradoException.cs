namespace Chamados.Domain;

/// <summary>Tentativa de editar um chamado resolvido ou cancelado.</summary>
public sealed class ChamadoEncerradoException(Guid chamadoId, StatusChamado status)
    : InvalidOperationException($"O chamado {chamadoId} está {status} e não pode ser editado.")
{
    public Guid ChamadoId { get; } = chamadoId;

    public StatusChamado Status { get; } = status;
}
