namespace Chamados.Web.Components.Fila;

/// <summary>As ações que um cartão da fila pode pedir.</summary>
public enum TipoTransicao
{
    Iniciar,
    Resolver,
    Cancelar,

    /// <summary>Responsável atendente: passar o chamado em atendimento para outra pessoa.</summary>
    Reatribuir
}

/// <summary>
/// O que o cartão envia à página quando o usuário confirma uma ação.
/// O cartão só pede; quem fala com o ChamadoServico é a página.
/// Responsável atendente: em Iniciar e Reatribuir, <paramref name="Texto"/> é o Id do atendente escolhido;
/// em Resolver e Cancelar, a solução ou o motivo.
/// </summary>
public sealed record PedidoTransicao(Guid ChamadoId, TipoTransicao Tipo, string Texto);
