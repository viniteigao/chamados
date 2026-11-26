using Chamados.Domain;
using Chamados.Web.Pages.Chamados;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chamados.Web.Api;

/// <summary>
/// API JSON do Módulo 2, agrupada sob /api desde o Módulo 3.
/// Módulo 7: detalhe com DTO próprio, validação do corpo e descrição OpenAPI de cada endpoint.
/// </summary>
public static class EndpointsChamados
{
    public static RouteGroupBuilder MapApiChamados(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/chamados")
            .AddEndpointFilter<FiltroExcecoesDominio>()
            .WithTags("Chamados");   // agrupa os endpoints no documento OpenAPI

        api.MapGet("/", (StatusChamado? status, Prioridade? prioridadeMinima, string? texto, OrdemChamados? ordem,
                ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ListarAsync(
                new FiltroChamados(status, prioridadeMinima, texto, ordem ?? OrdemChamados.Prioridade),
                cancellationToken))
            .WithSummary("Lista chamados, com filtros opcionais por status, prioridade mínima e texto");

        api.MapGet("/estatisticas", (ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ObterEstatisticasAsync(cancellationToken))
            .WithSummary("Totais por status e prioridade e tempo médio de resolução");

        // Results<Ok<…>, NotFound>: o tipo de retorno declara os dois status possíveis,
        // e o documento OpenAPI os lista sem anotação extra.
        api.MapGet("/{id:guid}", async Task<Results<Ok<DetalheChamadoResposta>, NotFound>> (Guid id,
                ChamadoServico servico, CancellationToken cancellationToken)
            => await servico.ObterAsync(id, cancellationToken) is { } chamado
                ? TypedResults.Ok(DetalheChamadoResposta.De(chamado))
                : TypedResults.NotFound())
            .WithSummary("Detalhe de um chamado, com o histórico de status");

        // O corpo usa o mesmo ChamadoInput das telas: as Data Annotations são conferidas
        // pela validação de Minimal APIs (AddValidation) antes do código rodar; inválido = 400.
        api.MapPost("/", async (ChamadoInput entrada, ChamadoServico servico, CancellationToken cancellationToken) =>
        {
            var chamado = await servico.AbrirAsync(entrada.ParaComando(), cancellationToken);
            return TypedResults.Created($"/api/chamados/{chamado.Id}", ResumoChamado.De(chamado));
        })
        .WithSummary("Abre um chamado")
        .ProducesValidationProblem();

        // Edição: o corpo traz a versão lida. Versão desatualizada → 409 pelo filtro.
        api.MapPut("/{id:guid}", async (Guid id, EdicaoChamado comando, ChamadoServico servico,
                CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.EditarAsync(id, comando, cancellationToken)))
            .WithSummary("Edita título, prioridade e descrição, conferindo a versão lida");

        // Transições sem try/catch: o FiltroExcecoesDominio responde 404, 409 ou 400.
        api.MapPost("/{id:guid}/atendimento", async (Guid id, IniciarAtendimentoRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.IniciarAtendimentoAsync(id, requisicao.Responsavel, cancellationToken)))
            .WithSummary("Inicia o atendimento, atribuindo um responsável");

        api.MapPost("/{id:guid}/resolucao", async (Guid id, ResolverRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.ResolverAsync(id, requisicao.Solucao, cancellationToken)))
            .WithSummary("Resolve um chamado em atendimento, registrando a solução");

        api.MapPost("/{id:guid}/cancelamento", async (Guid id, CancelarRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.CancelarAsync(id, requisicao.Motivo, cancellationToken)))
            .WithSummary("Cancela um chamado aberto ou em atendimento, registrando o motivo");

        return api;
    }
}

public sealed record IniciarAtendimentoRequest(string Responsavel);

public sealed record ResolverRequest(string Solucao);

public sealed record CancelarRequest(string Motivo);
