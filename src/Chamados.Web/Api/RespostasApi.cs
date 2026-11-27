using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// DTO de resposta do detalhe. A API nunca devolve a entidade Chamado: o contrato JSON fica
/// estável mesmo que a entidade mude, e só sai o que o cliente precisa ver.
/// Prazos: o detalhe traz o prazo avaliado no momento da resposta (null para cancelados).
/// Meus chamados: quem abriu e, em cada registro do histórico, quem fez a mudança. Só os nomes:
/// o Id do usuário é interno e não sai na API.
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
    string? AbertoPor)
{
    public static DetalheChamadoResposta De(Chamado chamado, PrazoDoChamado? prazo) => new(
        chamado.Id, chamado.Titulo, chamado.Descricao, chamado.Status, chamado.Prioridade,
        chamado.Responsavel, chamado.Solucao, chamado.CriadoEm, chamado.EncerradoEm, chamado.Versao,
        [.. chamado.Historico.Select(r => new RegistroHistoricoResposta(r.De, r.Para, r.Em, r.Observacao, r.PorNome))],
        prazo is null ? null : new PrazoResposta(prazo.Vencimento, prazo.Situacao),
        chamado.AbertoPorNome);
}

public sealed record RegistroHistoricoResposta(StatusChamado De, StatusChamado Para, DateTimeOffset Em, string? Observacao,
    string? Por);

/// <summary>Prazos: quando vence e a situação ("NoPrazo", "PertoDeVencer", "Vencido", "Cumprido", "Descumprido").</summary>
public sealed record PrazoResposta(DateTimeOffset Vencimento, SituacaoPrazo Situacao);
