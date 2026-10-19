namespace Chamados.Domain;

/// <summary>
/// Tentativa de mover o chamado para um status não permitido a partir do atual.
/// Deriva de InvalidOperationException: a operação é inválida para o estado do objeto.
/// </summary>
public sealed class TransicaoInvalidaException(Guid chamadoId, StatusChamado atual, StatusChamado pretendido)
    : InvalidOperationException($"O chamado {chamadoId} não pode passar de {atual} para {pretendido}.")
{
    public Guid ChamadoId { get; } = chamadoId;

    public StatusChamado Atual { get; } = atual;

    public StatusChamado Pretendido { get; } = pretendido;
}