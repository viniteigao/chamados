using Chamados.Domain;
using Chamados.Web.Diagnostico;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Mvc;

namespace Chamados.Web.ViewComponents;

/// <summary>
/// Mensagens não lidas: o aviso do cabeçalho, ao lado do nome de quem está conectado. Diz em quantos
/// chamados há mensagem que a pessoa ainda não leu e leva à lista filtrada. Sem nenhuma, não aparece.
/// Vale para todos: quem não vê todos os chamados conta só entre os que abriu.
/// O cabeçalho das páginas Blazor tem o mesmo aviso (Components/Layout/AvisoMensagensNovas.razor).
/// </summary>
public sealed class MensagensNovasViewComponent(ChamadoServico servico, ILogger<MensagensNovasViewComponent> logger)
    : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (UserClaimsPrincipal.IdDoUsuario() is not { } usuarioId)
        {
            return Content(string.Empty);
        }

        int chamados;
        try
        {
            chamados = await servico.ContarComNaoLidasAsync(
                usuarioId, UserClaimsPrincipal.RestricaoDeDono(), HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Como o contador de pendências: numa falha, o cabeçalho sai sem o aviso e a página continua.
            Registro.FalhaAoContarNaoLidas(logger, ex);
            return Content(string.Empty);
        }

        // Procura Pages/Shared/Components/MensagensNovas/Default.cshtml.
        return chamados == 0 ? Content(string.Empty) : View(chamados);
    }
}
