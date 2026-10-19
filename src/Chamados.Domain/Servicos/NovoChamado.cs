namespace Chamados.Domain;

/// <summary>Dados necessários para abrir um chamado.</summary>
public sealed record NovoChamado(
    string Titulo,
    Prioridade Prioridade = Prioridade.Media,
    string? Descricao = null);