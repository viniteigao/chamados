using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// DTO de resposta do detalhe. A API nunca devolve a entidade Chamado: o contrato JSON fica
/// estável mesmo que a entidade mude, e só sai o que o cliente precisa ver.
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
    IReadOnlyList<RegistroHistoricoResposta> Historico)
{
    public static DetalheChamadoResposta De(Chamado chamado) => new(
        chamado.Id, chamado.Titulo, chamado.Descricao, chamado.Status, chamado.Prioridade,
        chamado.Responsavel, chamado.Solucao, chamado.CriadoEm, chamado.EncerradoEm, chamado.Versao,
        [.. chamado.Historico.Select(r => new RegistroHistoricoResposta(r.De, r.Para, r.Em, r.Observacao))]);
}

public sealed record RegistroHistoricoResposta(StatusChamado De, StatusChamado Para, DateTimeOffset Em, string? Observacao);
