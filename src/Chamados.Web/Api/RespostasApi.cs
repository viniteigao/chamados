using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// DTO de resposta do detalhe. A API nunca devolve a entidade Chamado: o contrato JSON fica
/// estável mesmo que a entidade mude, e só sai o que o cliente precisa ver.
/// Prazos: o detalhe traz o prazo avaliado no momento da resposta (null para cancelados).
/// Meus chamados: quem abriu e, em cada registro do histórico, quem fez a mudança. Só os nomes:
/// o Id do usuário é interno e não sai na API.
/// Comentários: a conversa, da mensagem mais antiga para a mais nova, cada uma com autor, data e hora.
/// Histórico das edições: cada edição de título, prioridade ou descrição, com quando e por quem.
/// Mensagens não lidas: cada mensagem diz se é nova para quem pediu o detalhe.
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
    IReadOnlyList<ComentarioResposta> Comentarios,
    IReadOnlyList<RegistroEdicaoResposta> Edicoes)
{
    public static DetalheChamadoResposta De(Chamado chamado, PrazoDoChamado? prazo, string? leitorId = null,
        DateTimeOffset? lidoAte = null) => new(
        chamado.Id, chamado.Titulo, chamado.Descricao, chamado.Status, chamado.Prioridade,
        chamado.Responsavel, chamado.Solucao, chamado.CriadoEm, chamado.EncerradoEm, chamado.Versao,
        [.. chamado.Historico.Select(r => new RegistroHistoricoResposta(r.De, r.Para, r.Em, r.Observacao, r.PorNome))],
        prazo is null ? null : new PrazoResposta(prazo.Vencimento, prazo.Situacao),
        chamado.AbertoPorNome,
        ComentarioResposta.Da(chamado, leitorId, lidoAte),
        [.. chamado.Edicoes.Select(e => new RegistroEdicaoResposta(e.Em, e.TituloAnterior, e.TituloNovo,
            e.PrioridadeAnterior, e.PrioridadeNova, e.DescricaoAlterada, e.PorNome, e.ResponsavelAnterior, e.ResponsavelNovo))]);
}

/// <summary>
/// Histórico das edições: título e prioridade de antes e de depois (nulos quando não mudaram) e se a
/// descrição mudou. Só o nome de quem editou, como no resto da API.
/// </summary>
public sealed record RegistroEdicaoResposta(DateTimeOffset Em, string? TituloAnterior, string? TituloNovo,
    Prioridade? PrioridadeAnterior, Prioridade? PrioridadeNova, bool DescricaoAlterada, string? Por,
    string? ResponsavelAnterior = null, string? ResponsavelNovo = null);

/// <summary>Responsável atendente: um atendente ativo, para escolher o responsável.</summary>
public sealed record AtendenteResposta(string Id, string Nome);

/// <summary>
/// Responsável atendente: a carga de um atendente. <paramref name="Atendente"/> nulo agrupa os chamados
/// atribuídos antes deste recurso, quando o responsável era um texto livre.
/// </summary>
public sealed record CargaResposta(string? Atendente, int EmAtendimento, int Vencidos);

public sealed record RegistroHistoricoResposta(StatusChamado De, StatusChamado Para, DateTimeOffset Em, string? Observacao,
    string? Por);

/// <summary>
/// Comentários: uma mensagem da conversa. "Em" é a data e a hora, com fuso (ISO 8601).
/// Mensagens não lidas: <paramref name="Nova"/> diz se quem pediu ainda não tinha lido a mensagem
/// (a regra de MensagensNaoLidas; ler pela API não marca nada: para isso há POST .../comentarios/lidos).
/// </summary>
public sealed record ComentarioResposta(string Texto, DateTimeOffset Em, string Autor, bool Nova = false)
{
    public static ComentarioResposta De(Comentario comentario, bool nova = false)
    {
        ArgumentNullException.ThrowIfNull(comentario);
        return new(comentario.Texto, comentario.Em, comentario.AutorNome, nova);
    }

    /// <summary>A conversa do chamado; com <paramref name="leitorId"/>, cada mensagem diz se é nova para ele.</summary>
    public static IReadOnlyList<ComentarioResposta> Da(Chamado chamado, string? leitorId = null, DateTimeOffset? lidoAte = null)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        var novas = leitorId is null ? [] : chamado.NaoLidasPor(leitorId, lidoAte);
        return [.. chamado.Comentarios.Select(m => De(m, novas.Contains(m)))];
    }
}

/// <summary>Prazos: quando vence e a situação ("NoPrazo", "PertoDeVencer", "Vencido", "Cumprido", "Descumprido").</summary>
public sealed record PrazoResposta(DateTimeOffset Vencimento, SituacaoPrazo Situacao);
