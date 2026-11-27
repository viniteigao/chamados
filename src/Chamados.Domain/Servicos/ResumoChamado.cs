namespace Chamados.Domain;

/// <summary>
/// Projeção de leitura de um chamado, usada em listagens.
/// Prazos: <paramref name="EncerradoEm"/> entrou para a lista dizer se um resolvido cumpriu o prazo.
/// Meus chamados: <paramref name="AbertoPorNome"/>, para a fila mostrar quem pediu.
/// Comentários: <paramref name="TotalComentarios"/> é quantas mensagens a conversa tem (contadas no banco).
/// Mensagens não lidas: <paramref name="ComentariosNaoLidos"/> é quantas delas o leitor da consulta
/// (<see cref="FiltroChamados.LeitorId"/>) ainda não leu; null quando a consulta não teve leitor
/// (por exemplo, no resumo devolvido depois de uma ação, que não é feito para ninguém em especial).
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
    string? AbertoPorNome = null,
    int TotalComentarios = 0,
    int? ComentariosNaoLidos = null)
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
            chamado.AbertoPorNome,
            chamado.Comentarios.Count);
    }
}
