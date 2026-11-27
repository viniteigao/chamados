namespace Chamados.Domain;

/// <summary>
/// Dados necessários para abrir um chamado.
/// Meus chamados: <paramref name="AbertoPor"/> é quem está abrindo (o usuário conectado).
/// </summary>
public sealed record NovoChamado(
    string Titulo,
    Prioridade Prioridade = Prioridade.Media,
    string? Descricao = null,
    Autor? AbertoPor = null);
