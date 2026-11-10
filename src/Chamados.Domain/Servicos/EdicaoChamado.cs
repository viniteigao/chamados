namespace Chamados.Domain;

/// <summary>
/// Comando de edição. VersaoEsperada é a versão que o usuário tinha na tela
/// quando começou a editar.
/// </summary>
public sealed record EdicaoChamado(
    string Titulo,
    Prioridade Prioridade,
    string? Descricao,
    int VersaoEsperada);
