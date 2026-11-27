using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// DTO de resposta do detalhe. A API nunca devolve a entidade Chamado: o contrato JSON fica
/// estável mesmo que a entidade mude, e só sai o que o cliente precisa ver.
/// Prazos: o detalhe traz o prazo avaliado no momento da resposta (null para cancelados).
/// Meus chamados: quem abriu e, em cada registro do histórico, quem fez a mudança. Só os nomes:
/// o Id do usuário é interno e não sai na API.
/// Comentários: a conversa, da mensagem mais antiga para a mais nova, cada uma com autor, data e hora.
/// </summary>
public sealed record DetalheChamadoResposta(
    Guid Id,
    string Titulo,
    string? Descricao,
    StatusChamado Status,
    Prioridade Prioridade,
    string? Responsavel,
    string? Solucao,
    DateTimeOffset CriadoEm,
    DateTimeOffset? EncerradoEm,
    int Versao,
    IReadOnlyList<RegistroHistoricoResposta> Historico,
    PrazoResposta? Prazo,
    string? AbertoPor,
    IReadOnlyList<ComentarioResposta> Comentarios)
{
    public static DetalheChamadoResposta De(Chamado chamado, PrazoDoChamado? prazo) => new(
        chamado.Id, chamado.Titulo, chamado.Descricao, chamado.Status, chamado.Prioridade,
        chamado.Responsavel, chamado.Solucao, chamado.CriadoEm, chamado.EncerradoEm, chamado.Versao,
        [.. chamado.Historico.Select(r => new RegistroHistoricoResposta(r.De, r.Para, r.Em, r.Observacao, r.PorNome))],
        prazo is null ? null : new PrazoResposta(prazo.Vencimento, prazo.Situacao),
        chamado.AbertoPorNome,
        ComentarioResposta.Da(chamado));
}

public sealed record RegistroHistoricoResposta(StatusChamado De, StatusChamado Para, DateTimeOffset Em, string? Observacao,
    string? Por);

/// <summary>Comentários: uma mensagem da conversa. "Em" é a data e a hora, com fuso (ISO 8601).</summary>
public sealed record ComentarioResposta(string Texto, DateTimeOffset Em, string Autor)
{
    public static ComentarioResposta De(Comentario comentario)
    {
        ArgumentNullException.ThrowIfNull(comentario);
        return new(comentario.Texto, comentario.Em, comentario.AutorNome);
    }

    public static IReadOnlyList<ComentarioResposta> Da(Chamado chamado)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        return [.. chamado.Comentarios.Select(De)];
    }
}

/// <summary>Prazos: quando vence e a situação ("NoPrazo", "PertoDeVencer", "Vencido", "Cumprido", "Descumprido").</summary>
public sealed record PrazoResposta(DateTimeOffset Vencimento, SituacaoPrazo Situacao);
