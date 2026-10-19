namespace Chamados.Domain;

public sealed class ChamadoNaoEncontradoException(Guid chamadoId)
    : Exception($"Chamado {chamadoId} não encontrado.")
{
    public Guid ChamadoId { get; } = chamadoId;
}