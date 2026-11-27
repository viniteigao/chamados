namespace Chamados.Domain;

/// <summary>
/// Projeção de leitura de um chamado, usada em listagens.
/// Prazos: <paramref name="EncerradoEm"/> entrou para a lista dizer se um resolvido cumpriu o prazo.
/// Meus chamados: <paramref name="AbertoPorNome"/>, para a fila mostrar quem pediu.
/// </summary>
public sealed record ResumoChamado(
    Guid Id,
    string Titulo,
    StatusChamado Status,
    Prioridade Prioridade,
    string? Responsavel,
    DateTimeOffset CriadoEm,
    int Versao,
    DateTimeOffset? EncerradoEm = null,
    string? AbertoPorNome = null)
{
    public static ResumoChamado De(Chamado chamado)
    {
        ArgumentNullException.ThrowIfNull(chamado);

        return new(
            chamado.Id,
            chamado.Titulo,
            chamado.Status,
            chamado.Prioridade,
            chamado.Responsavel,
            chamado.CriadoEm,
            chamado.Versao,
            chamado.EncerradoEm,
            chamado.AbertoPorNome);
    }
}
