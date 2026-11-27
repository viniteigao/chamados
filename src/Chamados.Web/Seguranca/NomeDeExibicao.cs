using System.Security.Claims;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Nome de exibição: o login continua pelo e-mail (o nome de usuário do Identity), mas o cabeçalho,
/// o histórico, a conversa e a API mostram um nome, como "Ana". O nome é uma claim do usuário,
/// guardada na tabela de claims que o Identity já tem (a mesma da claim "cargo"): sem coluna nova
/// nem migration. Na hora do login, o Identity põe as claims do usuário no cookie e no token.
/// </summary>
public static class NomeDeExibicao
{
    /// <summary>Tipo da claim com o nome de exibição.</summary>
    public const string Claim = "nome";

    /// <summary>
    /// O nome para mostrar. Sem a claim (por exemplo, numa sessão aberta antes deste recurso),
    /// vale o nome de login, que é o e-mail.
    /// </summary>
    public static string? NomeParaExibir(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        var nome = usuario.FindFirstValue(Claim);
        return string.IsNullOrWhiteSpace(nome) ? usuario.Identity?.Name : nome.Trim();
    }
}
