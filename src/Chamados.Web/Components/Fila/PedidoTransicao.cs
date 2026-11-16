namespace Chamados.Web.Components.Fila;

/// <summary>As três ações que um cartão da fila pode pedir.</summary>
public enum TipoTransicao
{
    Iniciar,
    Resolver,
    Cancelar
}

/// <summary>
/// O que o cartão envia à página quando o usuário confirma uma ação.
/// O cartão só pede; quem fala com o ChamadoServico é a página.
/// </summary>
public sealed record PedidoTransicao(Guid ChamadoId, TipoTransicao Tipo, string Texto);
