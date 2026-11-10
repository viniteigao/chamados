using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// API JSON do Módulo 2, agora agrupada sob /api para não disputar rotas com as páginas.
/// </summary>
public static class EndpointsChamados
{
    public static RouteGroupBuilder MapApiChamados(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/chamados")
            .AddEndpointFilter<FiltroExcecoesDominio>();

        api.MapGet("/", (StatusChamado? status, Prioridade? prioridadeMinima, string? texto, OrdemChamados? ordem,
                ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ListarAsync(
                new FiltroChamados(status, prioridadeMinima, texto, ordem ?? OrdemChamados.Prioridade),
                cancellationToken));

        api.MapGet("/estatisticas", (ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ObterEstatisticasAsync(cancellationToken));

        api.MapGet("/{id:guid}", async (Guid id, ChamadoServico servico, CancellationToken cancellationToken)
            => await servico.ObterAsync(id, cancellationToken) is { } chamado
                ? Results.Ok(ResumoChamado.De(chamado))
                : Results.NotFound());

        api.MapPost("/", async (NovoChamado comando, ChamadoServico servico, CancellationToken cancellationToken) =>
        {
            var chamado = await servico.AbrirAsync(comando, cancellationToken);
            return Results.Created($"/api/chamados/{chamado.Id}", ResumoChamado.De(chamado));
        });

        // Edição: o corpo traz a versão lida. Versão desatualizada → 409 pelo filtro.
        api.MapPut("/{id:guid}", async (Guid id, EdicaoChamado comando, ChamadoServico servico,
                CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.EditarAsync(id, comando, cancellationToken)));

        // Transições sem try/catch: o FiltroExcecoesDominio responde 404, 409 ou 400.
        api.MapPost("/{id:guid}/atendimento", async (Guid id, IniciarAtendimentoRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.IniciarAtendimentoAsync(id, requisicao.Responsavel, cancellationToken)));

        api.MapPost("/{id:guid}/resolucao", async (Guid id, ResolverRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.ResolverAsync(id, requisicao.Solucao, cancellationToken)));

        api.MapPost("/{id:guid}/cancelamento", async (Guid id, CancelarRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.CancelarAsync(id, requisicao.Motivo, cancellationToken)));

        return api;
    }
}

public sealed record IniciarAtendimentoRequest(string Responsavel);

public sealed record ResolverRequest(string Solucao);

public sealed record CancelarRequest(string Motivo);
