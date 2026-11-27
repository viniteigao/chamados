using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Usuarios;

/// <summary>Gestão de usuários: a lista. A pasta /Usuarios exige a política GerenciarUsuarios (Program.cs).</summary>
public sealed class IndexModel(GestaoDeUsuarios gestao) : PageModel
{
    public IReadOnlyList<UsuarioResumo> Usuarios { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Usuarios = await gestao.ListarAsync(cancellationToken);
}
