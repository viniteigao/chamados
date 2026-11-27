using System.Security.Claims;
using Chamados.Domain;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Meus chamados: quem vê quais chamados. Atendentes (papel) e coordenação (claim) veem todos;
/// os demais usuários veem só os chamados que abriram. Chamados sem dono (anteriores a este
/// recurso) ficam visíveis só para quem vê todos.
/// Esta é a única regra; páginas, componentes e API citam estes métodos.
/// </summary>
public static class Visibilidade
{
    public static bool VeTodosOsChamados(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return usuario.IsInRole(Politicas.PapelAtendente)
            || usuario.HasClaim(Politicas.ClaimCargo, Politicas.CargoCoordenacao);
    }

    /// <summary>Id do usuário conectado (claim NameIdentifier, preenchida pelo Identity).</summary>
    public static string? IdDoUsuario(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return usuario.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// O dono a aplicar nas consultas: null para quem vê todos (sem restrição); o próprio Id para os
    /// demais. Sem Id (não deveria acontecer com login), um valor que não casa com nenhum chamado.
    /// </summary>
    public static string? RestricaoDeDono(this ClaimsPrincipal usuario)
        => usuario.VeTodosOsChamados() ? null : usuario.IdDoUsuario() ?? "-";

    /// <summary>
    /// O dono para a lista: quem vê todos escolhe (<paramref name="soOsMeus"/>); os demais sempre
    /// veem só os seus, peçam ou não.
    /// </summary>
    public static string? DonoParaListar(this ClaimsPrincipal usuario, bool soOsMeus)
        => usuario.RestricaoDeDono() ?? (soOsMeus ? usuario.IdDoUsuario() ?? "-" : null);

    public static bool PodeVer(this ClaimsPrincipal usuario, Chamado chamado)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        return usuario.VeTodosOsChamados()
            || (usuario.IdDoUsuario() is { } id && chamado.FoiAbertoPor(id));
    }

    /// <summary>
    /// O usuário conectado como autor de uma ação; null se não houver login.
    /// Nome de exibição: o nome gravado é o de exibição ("Ana"), não o e-mail de login.
    /// </summary>
    public static Autor? ComoAutor(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return usuario.IdDoUsuario() is { } id && usuario.NomeParaExibir() is { Length: > 0 } nome
            ? new Autor(id, nome)
            : null;
    }
}
