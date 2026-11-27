using System.ComponentModel.DataAnnotations;
using Chamados.Domain;
using Chamados.Web.Pages.Chamados;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Chamados.Web.Api;

/// <summary>
/// API JSON do Módulo 2, agrupada sob /api desde o Módulo 3.
/// Módulo 7: detalhe com DTO próprio, validação do corpo e descrição OpenAPI de cada endpoint.
/// Módulo 8: todo o grupo exige autenticação (cookie ou token); ações de atendimento e indicadores
/// exigem as políticas correspondentes. Sem login: 401. Logado sem permissão: 403.
/// </summary>
public static class EndpointsChamados
{
    public static RouteGroupBuilder MapApiChamados(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/chamados")
            .AddEndpointFilter<FiltroExcecoesDominio>()
            .RequireAuthorization(Politicas.Api)   // cookie ou token; vale para todos os endpoints abaixo
            .WithTags("Chamados");   // agrupa os endpoints no documento OpenAPI

        api.MapGet("/", (StatusChamado? status, Prioridade? prioridadeMinima, string? texto, OrdemChamados? ordem,
                bool? somenteVencidos, ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ListarAsync(
                new FiltroChamados(status, prioridadeMinima, texto, ordem ?? OrdemChamados.Prioridade, somenteVencidos ?? false),
                cancellationToken))
            .WithSummary("Lista chamados, com filtros opcionais por status, prioridade mínima, texto e prazo vencido");

        api.MapGet("/estatisticas", (ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ObterEstatisticasAsync(cancellationToken))
            .RequireAuthorization(Politicas.VerIndicadores)
            .WithSummary("Totais por status e prioridade e tempo médio de resolução");

        // Prazos: indicadores de prazo, para a coordenação (mesma política das estatísticas).
        api.MapGet("/prazos", (ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ObterIndicadoresDePrazoAsync(cancellationToken))
            .RequireAuthorization(Politicas.VerIndicadores)
            .WithSummary("Vencidos, perto de vencer, percentual resolvido no prazo e a tabela de prazos");

        // Módulo 8: busca por responsável. No laboratório, esta rota começa vulnerável a injeção de SQL.
        api.MapGet("/por-responsavel", (string nome, ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ListarPorResponsavelAsync(nome, cancellationToken))
            .WithSummary("Chamados atribuídos a um responsável");

        // Results<Ok<…>, NotFound>: o tipo de retorno declara os dois status possíveis,
        // e o documento OpenAPI os lista sem anotação extra.
        api.MapGet("/{id:guid}", async Task<Results<Ok<DetalheChamadoResposta>, NotFound>> (Guid id,
                ChamadoServico servico, CancellationToken cancellationToken)
            => await servico.ObterAsync(id, cancellationToken) is { } chamado
                ? TypedResults.Ok(DetalheChamadoResposta.De(chamado, servico.PrazoDe(chamado)))
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
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Edita título, prioridade e descrição, conferindo a versão lida");

        // Transições sem try/catch: o FiltroExcecoesDominio responde 404, 409 ou 400.
        // Módulo 8: as três mudanças de status exigem a política Atender, como os handlers da página Detalhes.
        api.MapPost("/{id:guid}/atendimento", async (Guid id, IniciarAtendimentoRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.IniciarAtendimentoAsync(id, requisicao.Responsavel, cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Inicia o atendimento, atribuindo um responsável");

        api.MapPost("/{id:guid}/resolucao", async (Guid id, ResolverRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.ResolverAsync(id, requisicao.Solucao, cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Resolve um chamado em atendimento, registrando a solução");

        api.MapPost("/{id:guid}/cancelamento", async (Guid id, CancelarRequest requisicao,
                ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.CancelarAsync(id, requisicao.Motivo, cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Cancela um chamado aberto ou em atendimento, registrando o motivo");

        return api;
    }
}

// Melhorias: os corpos das transições passam pela validação automática (AddValidation), como o
// ChamadoInput: campo ausente ou longo demais responde 400 com o erro no campo, em português.
// O domínio confere de novo e tem a palavra final.
public sealed record IniciarAtendimentoRequest(
    [Required(ErrorMessage = "Informe o responsável.")]
    [StringLength(Chamado.TamanhoMaximoResponsavel, ErrorMessage = "O responsável deve ter no máximo {1} caracteres.")]
    string Responsavel);

public sealed record ResolverRequest(
    [Required(ErrorMessage = "Informe a solução.")]
    [StringLength(Chamado.TamanhoMaximoJustificativa, ErrorMessage = "A solução deve ter no máximo {1} caracteres.")]
    string Solucao);

public sealed record CancelarRequest(
    [Required(ErrorMessage = "Informe o motivo.")]
    [StringLength(Chamado.TamanhoMaximoJustificativa, ErrorMessage = "O motivo deve ter no máximo {1} caracteres.")]
    string Motivo);
