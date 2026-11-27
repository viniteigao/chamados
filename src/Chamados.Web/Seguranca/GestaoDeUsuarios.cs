using System.Security.Claims;
using Chamados.Infra;
using Chamados.Web.Diagnostico;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Web.Seguranca;

/// <summary>Um usuário como a tela de gestão o mostra.</summary>
public sealed record UsuarioResumo(string Id, string Email, string Nome, bool Atende, bool Coordena, bool Ativo);

/// <summary>Dados para cadastrar um usuário. A senha é a inicial; a pessoa pode trocá-la em "Meu perfil".</summary>
public sealed record NovoUsuario(string Nome, string Email, string Senha, bool Atende, bool Coordena);

/// <summary>
/// Gestão de usuários: a coordenação cadastra pessoas, define o que cada uma pode fazer, desativa e
/// reativa contas e redefine senhas. As regras ficam aqui; as páginas só chamam.
/// O que cada pessoa pode fazer é o mesmo de sempre: o papel Atendente e a claim cargo = Coordenacao.
/// Mudar o acesso (papel, coordenação, desativar, senha) troca o carimbo de segurança (security stamp)
/// do usuário: as sessões abertas dele deixam de valer (cookie e Blazor em até 1 minuto; API na próxima
/// requisição) e, ao entrar de novo, valem com o perfil novo. Mudar só o nome não derruba ninguém:
/// a revalidação do cookie já recarrega as claims, e o nome novo aparece em até 1 minuto.
/// </summary>
public sealed class GestaoDeUsuarios(
    UserManager<IdentityUser> usuarios,
    RoleManager<IdentityRole> papeis,
    ChamadosDbContext db,
    TimeProvider relogio,
    ILogger<GestaoDeUsuarios> logger)
{
    /// <summary>Tamanho máximo do nome de exibição (cabe com folga no nome do autor, no domínio).</summary>
    public const int TamanhoMaximoNome = 60;

    /// <summary>
    /// Desativar é um bloqueio sem fim. A data máxima não volta idêntica do banco (a conversão para
    /// número perde precisão), então "desativado" é bloqueio por mais de 100 anos. O bloqueio comum,
    /// de 5 minutos após 5 senhas erradas, não conta como desativado.
    /// </summary>
    public static bool EstaDesativado(IdentityUser usuario, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return usuario.LockoutEnd > agora.AddYears(100);
    }

    /// <summary>Todos os usuários, por nome. Quatro consultas, qualquer que seja o número de usuários.</summary>
    public async Task<IReadOnlyList<UsuarioResumo>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var todos = await usuarios.Users.AsNoTracking().ToListAsync(cancellationToken);
        var atendentes = (await usuarios.GetUsersInRoleAsync(Politicas.PapelAtendente)).Select(u => u.Id).ToHashSet();
        var coordenacao = (await usuarios.GetUsersForClaimAsync(new Claim(Politicas.ClaimCargo, Politicas.CargoCoordenacao)))
            .Select(u => u.Id).ToHashSet();
        var nomes = (await db.UserClaims.AsNoTracking()
                .Where(c => c.ClaimType == NomeDeExibicao.Claim)
                .Select(c => new { c.UserId, c.ClaimValue })
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.First().ClaimValue);

        var agora = relogio.GetUtcNow();
        return
        [
            .. todos
                .Select(u => new UsuarioResumo(u.Id, u.Email ?? string.Empty, nomes.GetValueOrDefault(u.Id) ?? u.UserName ?? string.Empty,
                    atendentes.Contains(u.Id), coordenacao.Contains(u.Id), !EstaDesativado(u, agora)))
                .OrderBy(u => u.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(u => u.Email, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<UsuarioResumo?> ObterAsync(string id)
    {
        if (await usuarios.FindByIdAsync(id) is not { } usuario)
        {
            return null;
        }

        var claims = await usuarios.GetClaimsAsync(usuario);
        return new UsuarioResumo(
            usuario.Id,
            usuario.Email ?? string.Empty,
            claims.FirstOrDefault(c => c.Type == NomeDeExibicao.Claim)?.Value ?? usuario.UserName ?? string.Empty,
            await usuarios.IsInRoleAsync(usuario, Politicas.PapelAtendente),
            claims.Any(EhCoordenacao),
            !EstaDesativado(usuario, relogio.GetUtcNow()));
    }

    /// <summary>
    /// Cadastra um usuário com nome, perfil e senha inicial. Tudo numa transação: se o perfil não
    /// puder ser gravado, o usuário também não fica criado pela metade.
    /// </summary>
    /// <param name="porId">Quem cadastrou (Id do usuário, para o log).</param>
    public async Task<IdentityResult> CriarAsync(NovoUsuario dados, string porId)
    {
        ArgumentNullException.ThrowIfNull(dados);

        var email = dados.Email.Trim();
        var usuario = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };

        await using var transacao = await db.Database.BeginTransactionAsync();
        var resultado = await usuarios.CreateAsync(usuario, dados.Senha);   // guarda só o hash da senha
        if (resultado.Succeeded)
        {
            resultado = await AplicarPerfilAsync(usuario, dados.Nome, dados.Atende, dados.Coordena);
        }

        if (!resultado.Succeeded)
        {
            return resultado;   // sem Commit: a transação desfaz o que foi gravado
        }

        await transacao.CommitAsync();
        Registro.UsuarioCriado(logger, porId, usuario.Id);
        return resultado;
    }

    /// <summary>Muda nome e perfil. Ninguém tira a própria coordenação: sempre sobra quem gerencie.</summary>
    public async Task<IdentityResult> AlterarAsync(string id, string nome, bool atende, bool coordena, string porId)
    {
        if (await usuarios.FindByIdAsync(id) is not { } usuario)
        {
            return NaoEncontrado();
        }

        if (id == porId && !coordena)
        {
            return Falha("Você não pode tirar a sua própria coordenação. Peça a outra pessoa da coordenação.");
        }

        await using var transacao = await db.Database.BeginTransactionAsync();
        var resultado = await AplicarPerfilAsync(usuario, nome, atende, coordena);
        if (!resultado.Succeeded)
        {
            return resultado;
        }

        await transacao.CommitAsync();
        Registro.PerfilDeUsuarioAlterado(logger, porId, usuario.Id, atende, coordena);
        return resultado;
    }

    /// <summary>"Meu perfil": a própria pessoa troca o nome de exibição.</summary>
    public async Task<IdentityResult> AlterarNomeAsync(string id, string nome)
    {
        if (await usuarios.FindByIdAsync(id) is not { } usuario)
        {
            return NaoEncontrado();
        }

        return await DefinirNomeAsync(usuario, nome);
    }

    /// <summary>
    /// Desativa a conta: bloqueio sem fim e carimbo de segurança novo, que derruba as sessões abertas.
    /// Os chamados e mensagens da pessoa continuam como estão, com o nome dela.
    /// </summary>
    public async Task<IdentityResult> DesativarAsync(string id, string porId)
    {
        if (await usuarios.FindByIdAsync(id) is not { } usuario)
        {
            return NaoEncontrado();
        }

        if (id == porId)
        {
            return Falha("Você não pode desativar a sua própria conta.");
        }

        var resultado = await Encadear(
            () => usuarios.SetLockoutEnabledAsync(usuario, true),
            () => usuarios.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue),
            () => usuarios.UpdateSecurityStampAsync(usuario));
        if (resultado.Succeeded)
        {
            Registro.UsuarioDesativado(logger, porId, usuario.Id);
        }

        return resultado;
    }

    public async Task<IdentityResult> ReativarAsync(string id, string porId)
    {
        if (await usuarios.FindByIdAsync(id) is not { } usuario)
        {
            return NaoEncontrado();
        }

        var resultado = await Encadear(
            () => usuarios.SetLockoutEndDateAsync(usuario, null),
            () => usuarios.ResetAccessFailedCountAsync(usuario));
        if (resultado.Succeeded)
        {
            Registro.UsuarioReativado(logger, porId, usuario.Id);
        }

        return resultado;
    }

    /// <summary>
    /// Redefine a senha de quem esqueceu (o sistema não envia e-mail). O Identity troca o carimbo
    /// de segurança: as sessões abertas com a senha antiga deixam de valer.
    /// </summary>
    public async Task<IdentityResult> RedefinirSenhaAsync(string id, string novaSenha, string porId)
    {
        if (await usuarios.FindByIdAsync(id) is not { } usuario)
        {
            return NaoEncontrado();
        }

        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await usuarios.ResetPasswordAsync(usuario, token, novaSenha);
        if (resultado.Succeeded)
        {
            Registro.SenhaRedefinida(logger, porId, usuario.Id);
        }

        return resultado;
    }

    // Nome, papel e cargo, mudando só o que for diferente. Se o acesso mudou, carimbo de segurança novo.
    private async Task<IdentityResult> AplicarPerfilAsync(IdentityUser usuario, string nome, bool atende, bool coordena)
    {
        var resultado = await DefinirNomeAsync(usuario, nome);
        if (!resultado.Succeeded)
        {
            return resultado;
        }

        var atendeHoje = await usuarios.IsInRoleAsync(usuario, Politicas.PapelAtendente);
        var cargo = (await usuarios.GetClaimsAsync(usuario)).FirstOrDefault(EhCoordenacao);
        var acessoMudou = atende != atendeHoje || coordena != (cargo is not null);
        if (atende && !atendeHoje)
        {
            // Em produção o papel pode ainda não existir (a semente só roda em Development).
            if (!await papeis.RoleExistsAsync(Politicas.PapelAtendente))
            {
                resultado = await papeis.CreateAsync(new IdentityRole(Politicas.PapelAtendente));
                if (!resultado.Succeeded)
                {
                    return resultado;
                }
            }

            resultado = await usuarios.AddToRoleAsync(usuario, Politicas.PapelAtendente);
        }
        else if (!atende && atendeHoje)
        {
            resultado = await usuarios.RemoveFromRoleAsync(usuario, Politicas.PapelAtendente);
        }

        if (!resultado.Succeeded)
        {
            return resultado;
        }

        if (coordena && cargo is null)
        {
            resultado = await usuarios.AddClaimAsync(usuario, new Claim(Politicas.ClaimCargo, Politicas.CargoCoordenacao));
        }
        else if (!coordena && cargo is not null)
        {
            resultado = await usuarios.RemoveClaimAsync(usuario, cargo);
        }

        return resultado.Succeeded && acessoMudou ? await usuarios.UpdateSecurityStampAsync(usuario) : resultado;
    }

    private async Task<IdentityResult> DefinirNomeAsync(IdentityUser usuario, string nome)
    {
        nome = nome?.Trim() ?? string.Empty;
        if (nome.Length == 0 || nome.Length > TamanhoMaximoNome)
        {
            return Falha($"Informe um nome de até {TamanhoMaximoNome} caracteres.");
        }

        var atual = (await usuarios.GetClaimsAsync(usuario)).FirstOrDefault(c => c.Type == NomeDeExibicao.Claim);
        if (atual is null)
        {
            return await usuarios.AddClaimAsync(usuario, new Claim(NomeDeExibicao.Claim, nome));
        }

        return atual.Value == nome
            ? IdentityResult.Success
            : await usuarios.ReplaceClaimAsync(usuario, atual, new Claim(NomeDeExibicao.Claim, nome));
    }

    private static bool EhCoordenacao(Claim claim)
        => claim.Type == Politicas.ClaimCargo && claim.Value == Politicas.CargoCoordenacao;

    // Executa os passos em ordem e para no primeiro que falhar.
    private static async Task<IdentityResult> Encadear(params Func<Task<IdentityResult>>[] passos)
    {
        foreach (var passo in passos)
        {
            var resultado = await passo();
            if (!resultado.Succeeded)
            {
                return resultado;
            }
        }

        return IdentityResult.Success;
    }

    private static IdentityResult NaoEncontrado() => Falha("Usuário não encontrado.");

    private static IdentityResult Falha(string mensagem)
        => IdentityResult.Failed(new IdentityError { Code = "Gestao", Description = mensagem });
}
