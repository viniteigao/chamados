namespace Chamados.Domain;

/// <summary>
/// Responsável atendente: quantos chamados de um filtro estão com cada responsável (contagem no banco).
/// <paramref name="ResponsavelId"/> nulo agrupa os chamados atribuídos antes deste recurso, que têm
/// só o nome digitado; <paramref name="Nome"/> é o nome gravado nos chamados.
/// </summary>
public sealed record ContagemPorResponsavel(string? ResponsavelId, string? Nome, int Total);

/// <summary>
/// Responsável atendente: a carga de cada atendente, para a coordenação. Chamados em atendimento com
/// ele e, desses, quantos estão com o prazo vencido.
/// </summary>
public sealed record CargaDeAtendente(string? ResponsavelId, string? Nome, int EmAtendimento, int Vencidos);
