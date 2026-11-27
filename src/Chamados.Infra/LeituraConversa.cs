namespace Chamados.Infra;

/// <summary>
/// Mensagens não lidas: até quando um usuário leu a conversa de um chamado (a hora da última mensagem
/// que ele viu). Uma linha por chamado e usuário, na tabela LeiturasConversas.
/// Fica na infraestrutura, e não no domínio: o domínio só conhece a regra (MensagensNaoLidas) e o
/// contrato do repositório; esta classe existe para o EF Core ter o que mapear.
/// </summary>
public sealed class LeituraConversa(Guid chamadoId, string usuarioId, DateTimeOffset lidoAte)
{
    public Guid ChamadoId { get; private set; } = chamadoId;

    /// <summary>O Id do usuário, como no resto do sistema (sem chave estrangeira para o Identity).</summary>
    public string UsuarioId { get; private set; } = usuarioId;

    public DateTimeOffset LidoAte { get; private set; } = lidoAte;
}
