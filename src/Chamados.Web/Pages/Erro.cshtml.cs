using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages;

/// <summary>
/// Página única para erros: /Erro (exceção, via UseExceptionHandler, fora de Development)
/// e /Erro/404, /Erro/400... (respostas sem corpo, via UseStatusCodePagesWithReExecute).
/// Mostra só um código de rastreio: o detalhe da exceção fica no log, nunca na tela.
/// </summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErroModel : PageModel
{
    public int Codigo { get; private set; }

    public string CodigoRequisicao { get; private set; } = string.Empty;

    /// <summary>Endereço que o usuário pediu, quando a página veio de um 404.</summary>
    public string? CaminhoOriginal { get; private set; }

    public string Titulo => Codigo switch
    {
        404 => "Página não encontrada",
        400 => "Requisição inválida",
        _ => "Algo deu errado"
    };

    public string Mensagem => Codigo switch
    {
        404 => "O endereço não existe ou o chamado foi digitado errado. Volte à lista e procure por lá.",
        400 => "Não foi possível processar o envio. Recarregue a página e tente de novo.",
        _ => "Não foi possível concluir a operação. Tente novamente em instantes."
    };

    // OnGet e OnPost: a requisição que falhou pode ter sido um POST de formulário.
    public void OnGet(int? codigo)
    {
        Codigo = codigo ?? 500;
        CodigoRequisicao = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        CaminhoOriginal = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath;
    }

    public void OnPost(int? codigo) => OnGet(codigo);
}
