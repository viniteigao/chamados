namespace Chamados.Domain;

/// <summary>
/// Uma transição de status já ocorrida. Imutável: o passado não muda.
/// Meus chamados: <paramref name="PorId"/> e <paramref name="PorNome"/> dizem quem fez a mudança
/// (nulos nos registros anteriores a este recurso). O nome fica como era no momento da ação.
/// </summary>
public sealed record RegistroHistorico(
    StatusChamado De,
    StatusChamado Para,
    DateTimeOffset Em,
    string? Observacao,
    string? PorId = null,
    string? PorNome = null)
{
    /// <summary>Quem fez a mudança; null nos registros antigos.</summary>
    public Autor? Por => PorId is null ? null : new Autor(PorId, PorNome ?? PorId);
}
