using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Usuários de exemplo, só em Development. A senha NÃO está aqui: vem da configuração
/// (Segredos do Usuário no computador do desenvolvedor), na chave Semente:SenhaInicial.
/// Nome de exibição: cada usuário ganha a claim "nome". O login continua pelo e-mail.
/// </summary>
public static class SementeUsuarios
{
    public const string ChaveSenha = "Semente:SenhaInicial";

    public static readonly (string Email, string Nome, string? Papel, string? Cargo)[] Usuarios =
    [
        ("ana@chamados.local", "Ana", Politicas.PapelAtendente, null),        // atende chamados
        ("bruno@chamados.local", "Bruno", null, null),                         // só abre e acompanha
        ("carla@chamados.local", "Carla", null, Politicas.CargoCoordenacao),  // vê os indicadores
    ];

    public static async Task PopularAsync(IServiceProvider services, IConfiguration configuracao)
    {
        var senha = configuracao[ChaveSenha];
        if (string.IsNullOrWhiteSpace(senha))
        {
            // Falha cedo e diz como resolver. Nunca um valor padrão escrito no código.
            throw new InvalidOperationException(
                $"Defina a senha dos usuários de exemplo nos Segredos do Usuário: chave {ChaveSenha}. " +
                $"No Visual Studio: botão direito em Chamados.Web > Gerenciar Segredos do Usuário. " +
                $"Pela CLI: dotnet user-secrets set \"{ChaveSenha}\" \"<senha>\" --project src/Chamados.Web");
        }

        await using var escopo = services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var papeis = escopo.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await papeis.RoleExistsAsync(Politicas.PapelAtendente))
        {
            Garantir(await papeis.CreateAsync(new IdentityRole(Politicas.PapelAtendente)));
        }

        foreach (var (email, nome, papel, cargo) in Usuarios)
        {
            if (await usuarios.FindByEmailAsync(email) is { } existente)
            {
                // Um banco criado antes do nome de exibição: o usuário já existe, só falta o nome.
                await GarantirNomeAsync(usuarios, existente, nome);
                continue;
            }

            var usuario = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };

            // CreateAsync guarda só o hash da senha (PBKDF2 com sal), nunca a senha.
            Garantir(await usuarios.CreateAsync(usuario, senha));

            if (papel is not null)
            {
                Garantir(await usuarios.AddToRoleAsync(usuario, papel));
            }

            if (cargo is not null)
            {
                Garantir(await usuarios.AddClaimAsync(usuario, new Claim(Politicas.ClaimCargo, cargo)));
            }

            await GarantirNomeAsync(usuarios, usuario, nome);
        }
    }

    // Acrescenta a claim "nome" se o usuário ainda não tiver uma. Um nome já gravado não é trocado.
    private static async Task GarantirNomeAsync(UserManager<IdentityUser> usuarios, IdentityUser usuario, string nome)
    {
        var claims = await usuarios.GetClaimsAsync(usuario);
        if (!claims.Any(c => c.Type == NomeDeExibicao.Claim))
        {
            Garantir(await usuarios.AddClaimAsync(usuario, new Claim(NomeDeExibicao.Claim, nome)));
        }
    }

    private static void Garantir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            // Ex.: a senha configurada não atende às regras de senha do Program.cs.
            throw new InvalidOperationException(
                "Não foi possível criar os usuários de exemplo: " +
                string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }
    }
}
