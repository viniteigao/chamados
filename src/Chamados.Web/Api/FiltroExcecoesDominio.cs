using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// Traduz exceções do domínio em respostas HTTP padronizadas (RFC 9457, Problem Details)
/// para todos os endpoints do grupo /api. Substitui os try/catch repetidos do Módulo 2.
/// </summary>
public sealed class FiltroExcecoesDominio : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ChamadoNaoEncontradoException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status404NotFound,
                title: "Chamado não encontrado");
        }
        catch (TransicaoInvalidaException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict,
                title: "Transição de status inválida");
        }
        catch (ArgumentException ex)
        {
            // Mensagens de ArgumentException vêm das regras do domínio, não de detalhes internos.
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest,
                title: "Dados inválidos");
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // O cliente desistiu (fechou a aba, timeout): não há para quem responder.
            return Results.Empty;
        }
    }
}
