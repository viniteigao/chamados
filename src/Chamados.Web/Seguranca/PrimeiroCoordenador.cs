using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Gestão de usuários: fora de Development a semente não roda, e sem nenhum coordenador ninguém
/// conseguiria cadastrar o primeiro usuário. Com a seção PrimeiroCoordenador configurada (em produção,
/// por variáveis de ambiente: PrimeiroCoordenador__Email, __Nome e __Senha), a aplicação cria essa conta
/// ao iniciar, se ela ainda não existir. Depois do primeiro início, a senha pode sair da configuração:
/// uma conta que já existe nunca é alterada por aqui.
/// </summary>
public static class PrimeiroCoordenador
{
    public const string Secao = "PrimeiroCoordenador";

    public static async Task GarantirAsync(IServiceProvider services, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        var secao = configuracao.GetSection(Secao);
        var email = secao["Email"];
        if (string.IsNullOrWhiteSpace(email))
        {
            return;   // não configurado: nada a fazer
        }

        await using var escopo = services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await usuarios.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var senha = secao["Senha"];
        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new InvalidOperationException(
                $"Para criar o primeiro coordenador ({Secao}:Email), informe também {Secao}:Senha.");
        }

        var gestao = escopo.ServiceProvider.GetRequiredService<GestaoDeUsuarios>();
        var nome = string.IsNullOrWhiteSpace(secao["Nome"]) ? email : secao["Nome"]!;
        var resultado = await gestao.CriarAsync(new NovoUsuario(nome, email, senha, Atende: false, Coordena: true), "configuracao");
        if (!resultado.Succeeded)
        {
            // Ex.: a senha configurada não atende às regras de senha do Program.cs.
            throw new InvalidOperationException(
                "Não foi possível criar o primeiro coordenador: " + string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }
    }
}
