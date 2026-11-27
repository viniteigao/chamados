using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Gestão de usuários: as mensagens de erro do Identity em português. Sem isto, a coordenação veria
/// "Passwords must have at least one uppercase ('A'-'Z')." ao cadastrar alguém. Só os erros que as
/// telas deste sistema podem provocar; os demais ficam com o texto padrão.
/// </summary>
public sealed class ErrosDoIdentityEmPortugues : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email)
        => Erro(nameof(DuplicateEmail), $"Já existe um usuário com o e-mail {email}.");

    public override IdentityError DuplicateUserName(string userName)
        => Erro(nameof(DuplicateUserName), $"Já existe um usuário com o login {userName}.");

    public override IdentityError InvalidEmail(string? email)
        => Erro(nameof(InvalidEmail), $"O e-mail {email} não é válido.");

    public override IdentityError InvalidUserName(string? userName)
        => Erro(nameof(InvalidUserName), $"O login {userName} tem caracteres não aceitos. Use letras sem acento, números e @ . - _ +.");

    public override IdentityError PasswordMismatch()
        => Erro(nameof(PasswordMismatch), "Senha incorreta.");

    public override IdentityError PasswordTooShort(int length)
        => Erro(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Erro(nameof(PasswordRequiresNonAlphanumeric), "A senha deve ter pelo menos um símbolo (como ! @ # ou %).");

    public override IdentityError PasswordRequiresDigit()
        => Erro(nameof(PasswordRequiresDigit), "A senha deve ter pelo menos um número.");

    public override IdentityError PasswordRequiresLower()
        => Erro(nameof(PasswordRequiresLower), "A senha deve ter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper()
        => Erro(nameof(PasswordRequiresUpper), "A senha deve ter pelo menos uma letra maiúscula.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Erro(nameof(PasswordRequiresUniqueChars), $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes.");

    public override IdentityError InvalidToken()
        => Erro(nameof(InvalidToken), "O pedido expirou. Tente de novo.");

    public override IdentityError ConcurrencyFailure()
        => Erro(nameof(ConcurrencyFailure), "Outra pessoa alterou este usuário ao mesmo tempo. Tente de novo.");

    private static IdentityError Erro(string codigo, string descricao) => new() { Code = codigo, Description = descricao };
}
