using Chamados.Domain;
using Chamados.Infra;
using Microsoft.AspNetCore.Identity;

namespace Chamados.Web.Seguranca;

/// <summary>
/// Responsável atendente: quem pode ser responsável por um chamado. São as contas com o papel Atendente
/// que não estão desativadas, com o nome de exibição. Toda escolha de responsável que chega de fora
/// (formulário, fila ou API) é conferida aqui: não dá para atribuir um chamado a um solicitante, a uma
/// conta desativada nem a um Id inventado.
/// </summary>
public sealed class Atendentes(UserManager<IdentityUser> usuarios, ChamadosDbContext db, TimeProvider relogio)
{
    /// <summary>Os atendentes ativos, por nome. Duas consultas, qualquer que seja o número de usuários.</summary>
    public async Task<IReadOnlyList<Autor>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow();
        var ativos = (await usuarios.GetUsersInRoleAsync(Politicas.PapelAtendente))
            .Where(u => !GestaoDeUsuarios.EstaDesativado(u, agora))
            .ToList();
        var nomes = await NomeDeExibicao.NomesAsync(db, cancellationToken);

        return
        [
            .. ativos
                .Select(u => new Autor(u.Id, nomes.GetValueOrDefault(u.Id) ?? u.UserName ?? u.Id))
                .OrderBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    /// <summary>O atendente ativo com este Id; null se o Id não existir, não atender ou estiver desativado.</summary>
    public async Task<Autor?> ObterAsync(string? id, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(id)
            ? null
            : (await ListarAsync(cancellationToken)).FirstOrDefault(a => a.Id == id);
}
