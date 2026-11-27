using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Chamados.Web.Apresentacao;

/// <summary>
/// Melhorias: as regras do domínio lançam ArgumentException com mensagem pensada para o usuário,
/// mas o .NET acrescenta " (Parameter 'titulo')" e, em ArgumentOutOfRangeException,
/// "Actual value was 99." numa linha nova. Telas e API mostram só a frase do domínio.
/// </summary>
public static class MensagensDeErro
{
    public static string ParaUsuario(this ArgumentException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        // O sufixo do parâmetro vem sempre em inglês: o .NET não traduz mensagens do runtime.
        var mensagem = excecao.Message;
        var corte = mensagem.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return corte >= 0 ? mensagem[..corte] : mensagem;
    }

    /// <summary>
    /// Gestão de usuários: põe cada erro do Identity ao lado do campo a que se refere (senha, e-mail);
    /// os demais vão para o resumo no topo do formulário. Como o login é o e-mail, um e-mail repetido
    /// gera dois erros (login e e-mail repetidos): fica só o do e-mail. Ao lado de um campo aparece só
    /// a primeira mensagem dele; por isso os erros de um mesmo campo viram uma frase só ("falta
    /// maiúscula" e "falta símbolo" juntos), em vez de a pessoa descobrir um de cada vez.
    /// </summary>
    public static void AdicionarErros(this ModelStateDictionary estado, IdentityResult resultado,
        string? campoEmail = null, string? campoSenha = null, string? campoSenhaAtual = null)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(resultado);

        var emailRepetido = resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
        var porCampo = resultado.Errors
            .Where(erro => !(emailRepetido && erro.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
            .GroupBy(erro => erro.Code switch
            {
                nameof(IdentityErrorDescriber.PasswordMismatch) => campoSenhaAtual,
                _ when erro.Code.StartsWith("Password", StringComparison.Ordinal) => campoSenha,
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail)
                    or nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.InvalidUserName) => campoEmail,
                _ => null,
            } ?? string.Empty);

        foreach (var grupo in porCampo)
        {
            if (grupo.Key.Length == 0)
            {
                // O resumo do topo lista todas as mensagens, uma por linha.
                foreach (var erro in grupo)
                {
                    estado.AddModelError(string.Empty, erro.Description);
                }
            }
            else
            {
                estado.AddModelError(grupo.Key, string.Join(" ", grupo.Select(erro => erro.Description)));
            }
        }
    }
}
