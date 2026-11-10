namespace Chamados.Domain;

/// <summary>
/// O chamado mudou depois que o usuário o leu. Carrega as duas versões
/// para que a tela possa explicar o conflito.
/// </summary>
public sealed class ConcorrenciaException(Guid chamadoId, int versaoEsperada, int versaoAtual)
    : Exception($"O chamado {chamadoId} foi alterado por outra pessoa (versão lida: {versaoEsperada}, atual: {versaoAtual}).")
{
    public Guid ChamadoId { get; } = chamadoId;

    public int VersaoEsperada { get; } = versaoEsperada;

    public int VersaoAtual { get; } = versaoAtual;
}
