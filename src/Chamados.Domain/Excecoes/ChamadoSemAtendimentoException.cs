namespace Chamados.Domain;

/// <summary>
/// Responsável atendente: trocar o responsável só faz sentido num chamado em atendimento. Num chamado
/// aberto, o responsável é escolhido ao iniciar o atendimento.
/// </summary>
public sealed class ChamadoSemAtendimentoException(Guid chamadoId, StatusChamado status)
    : InvalidOperationException($"O chamado {chamadoId} está {status} e ainda não tem responsável: inicie o atendimento escolhendo quem vai atender.")
{
    public Guid ChamadoId { get; } = chamadoId;

    public StatusChamado Status { get; } = status;
}
