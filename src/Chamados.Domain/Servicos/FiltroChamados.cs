namespace Chamados.Domain;

/// <summary>
/// Critérios de consulta. Imutável: para variar um critério,
/// use uma expressão with, que cria uma cópia alterada.
/// </summary>
public sealed record FiltroChamados(
    StatusChamado? Status = null,
    Prioridade? PrioridadeMinima = null,
    string? Texto = null,
    OrdemChamados Ordem = OrdemChamados.Prioridade,
    bool SomenteVencidos = false,
    string? AbertoPorId = null,
    string? ResponsavelId = null,
    string? LeitorId = null,
    bool SomenteComNaoLidas = false)
{
    // Meus chamados: AbertoPorId restringe aos chamados que esse usuário abriu. Quem preenche é a
    // camada Web, a partir do login (Seguranca/Visibilidade.cs); o domínio só aplica o critério.
    // Responsável atendente: ResponsavelId restringe aos chamados com esse atendente ("Meus atendimentos").
    // Mensagens não lidas: LeitorId não restringe nada; diz para quem contar as mensagens não lidas de
    // cada resumo (a regra está em MensagensNaoLidas). SomenteComNaoLidas restringe aos chamados com
    // alguma mensagem que esse leitor ainda não leu; sem leitor, nenhum chamado tem mensagem não lida.

    public static FiltroChamados Todos { get; } = new();

    /// <summary>
    /// Prazos: o pedido "só vencidos" traduzido para o repositório. Em cada prioridade, só entram
    /// chamados em andamento abertos antes do instante indicado. Quem preenche é o ChamadoServico,
    /// que conhece a política de prazos e o relógio; o repositório só aplica o critério.
    /// </summary>
    public IReadOnlyDictionary<Prioridade, DateTimeOffset>? AbertosAntesDe { get; init; }
}
