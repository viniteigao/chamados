using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Chamados.Domain;
using Chamados.Web.Diagnostico;
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
            .AddEndpointFilter<FiltroSessaoValida>()   // Gestão de usuários: token de conta desativada para de valer
            .RequireAuthorization(Politicas.Api)   // cookie ou token; vale para todos os endpoints abaixo
            .WithTags("Chamados");   // agrupa os endpoints no documento OpenAPI

        // Meus chamados: quem não é atendente nem da coordenação recebe só os chamados que abriu;
        // quem vê todos pode pedir só os seus com ?meus=true.
        api.MapGet("/", (StatusChamado? status, Prioridade? prioridadeMinima, string? texto, OrdemChamados? ordem,
                bool? somenteVencidos, bool? meus, ClaimsPrincipal usuario, ChamadoServico servico,
                CancellationToken cancellationToken)
            => servico.ListarAsync(
                new FiltroChamados(status, prioridadeMinima, texto, ordem ?? OrdemChamados.Prioridade, somenteVencidos ?? false,
                    usuario.DonoParaListar(meus ?? false)),
                cancellationToken))
            .WithSummary("Lista chamados (só os próprios, para quem não é atendente nem da coordenação), com filtros opcionais");

        api.MapGet("/estatisticas", (ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ObterEstatisticasAsync(cancellationToken: cancellationToken))
            .RequireAuthorization(Politicas.VerIndicadores)
            .WithSummary("Totais por status e prioridade e tempo médio de resolução");

        // Prazos: indicadores de prazo, para a coordenação (mesma política das estatísticas).
        api.MapGet("/prazos", (ChamadoServico servico, CancellationToken cancellationToken)
            => servico.ObterIndicadoresDePrazoAsync(cancellationToken))
            .RequireAuthorization(Politicas.VerIndicadores)
            .WithSummary("Vencidos, perto de vencer, percentual resolvido no prazo e a tabela de prazos");

        // Módulo 8: busca por responsável. No laboratório, esta rota começa vulnerável a injeção de SQL.
        // Meus chamados: para quem não vê todos, a busca fica restrita aos chamados que a pessoa abriu.
        api.MapGet("/por-responsavel", (string nome, ClaimsPrincipal usuario, ChamadoServico servico,
                CancellationToken cancellationToken)
            => servico.ListarPorResponsavelAsync(nome, usuario.RestricaoDeDono(), cancellationToken))
            .WithSummary("Chamados atribuídos a um responsável");

        // Results<Ok<…>, NotFound>: o tipo de retorno declara os dois status possíveis,
        // e o documento OpenAPI os lista sem anotação extra.
        // Meus chamados: chamado de outra pessoa responde 404, como se não existisse: um 403 confirmaria
        // que o Id existe. A tentativa fica no log (evento 1009).
        api.MapGet("/{id:guid}", async Task<Results<Ok<DetalheChamadoResposta>, NotFound>> (Guid id,
                ClaimsPrincipal usuario, ChamadoServico servico, ILoggerFactory registros, CancellationToken cancellationToken) =>
            {
                var chamado = await ObterVisivelAsync(id, usuario, servico, registros, cancellationToken);
                return chamado is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(DetalheChamadoResposta.De(chamado, servico.PrazoDe(chamado)));
            })
            .WithSummary("Detalhe de um chamado, com o histórico de status, as edições e a conversa");

        // Comentários: a conversa do chamado. Quem pode ver o chamado pode ler e escrever nela
        // (o dono, atendentes e coordenação); para os demais, 404, como no detalhe.
        api.MapGet("/{id:guid}/comentarios", async Task<Results<Ok<IReadOnlyList<ComentarioResposta>>, NotFound>> (Guid id,
                ClaimsPrincipal usuario, ChamadoServico servico, ILoggerFactory registros, CancellationToken cancellationToken) =>
            {
                var chamado = await ObterVisivelAsync(id, usuario, servico, registros, cancellationToken);
                return chamado is null ? TypedResults.NotFound() : TypedResults.Ok(ComentarioResposta.Da(chamado));
            })
            .WithSummary("Mensagens da conversa de um chamado, da mais antiga para a mais nova");

        // A data e a hora da mensagem vêm do relógio do servidor, nunca do corpo da requisição.
        api.MapPost("/{id:guid}/comentarios", async Task<Results<Created<ComentarioResposta>, NotFound, UnauthorizedHttpResult>> (
                Guid id, ComentarioRequest requisicao, ClaimsPrincipal usuario, ChamadoServico servico,
                ILoggerFactory registros, CancellationToken cancellationToken) =>
            {
                if (usuario.ComoAutor() is not { } autor)
                {
                    return TypedResults.Unauthorized();
                }

                if (await ObterVisivelAsync(id, usuario, servico, registros, cancellationToken) is null)
                {
                    return TypedResults.NotFound();
                }

                var comentario = await servico.ComentarAsync(id, requisicao.Texto, autor, cancellationToken);
                var logger = Registros(registros);
                Registro.ComentarioAdicionado(logger, id, autor.Id);
                return TypedResults.Created($"/api/chamados/{id}/comentarios", ComentarioResposta.De(comentario));
            })
            .WithSummary("Escreve uma mensagem na conversa do chamado")
            .ProducesValidationProblem();

        // O corpo usa o mesmo ChamadoInput das telas: as Data Annotations são conferidas
        // pela validação de Minimal APIs (AddValidation) antes do código rodar; inválido = 400.
        // Meus chamados: quem abre é o usuário do token ou do cookie.
        api.MapPost("/", async (ChamadoInput entrada, ClaimsPrincipal usuario, ChamadoServico servico,
            CancellationToken cancellationToken) =>
        {
            var chamado = await servico.AbrirAsync(entrada.ParaComando(usuario.ComoAutor()), cancellationToken);
            return TypedResults.Created($"/api/chamados/{chamado.Id}", ResumoChamado.De(chamado));
        })
        .WithSummary("Abre um chamado")
        .ProducesValidationProblem();

        // Edição: o corpo traz a versão lida. Versão desatualizada → 409 pelo filtro.
        // Histórico das edições: quem edita vem do token ou do cookie, nunca do corpo.
        api.MapPut("/{id:guid}", async (Guid id, EdicaoChamado comando, ClaimsPrincipal usuario, ChamadoServico servico,
                CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.EditarAsync(id, comando, usuario.ComoAutor(), cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Edita título, prioridade e descrição, conferindo a versão lida");

        // Transições sem try/catch: o FiltroExcecoesDominio responde 404, 409 ou 400.
        // Módulo 8: as três mudanças de status exigem a política Atender, como os handlers da página Detalhes.
        // Meus chamados: quem faz a mudança (usuário do token ou do cookie) vai para o histórico.
        api.MapPost("/{id:guid}/atendimento", async (Guid id, IniciarAtendimentoRequest requisicao,
                ClaimsPrincipal usuario, ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.IniciarAtendimentoAsync(id, requisicao.Responsavel, usuario.ComoAutor(), cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Inicia o atendimento, atribuindo um responsável");

        api.MapPost("/{id:guid}/resolucao", async (Guid id, ResolverRequest requisicao,
                ClaimsPrincipal usuario, ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.ResolverAsync(id, requisicao.Solucao, usuario.ComoAutor(), cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Resolve um chamado em atendimento, registrando a solução");

        api.MapPost("/{id:guid}/cancelamento", async (Guid id, CancelarRequest requisicao,
                ClaimsPrincipal usuario, ChamadoServico servico, CancellationToken cancellationToken)
            => ResumoChamado.De(await servico.CancelarAsync(id, requisicao.Motivo, usuario.ComoAutor(), cancellationToken)))
            .RequireAuthorization(Politicas.Atender)
            .WithSummary("Cancela um chamado aberto ou em atendimento, registrando o motivo");

        return api;
    }

    private static ILogger Registros(ILoggerFactory registros) => registros.CreateLogger(typeof(EndpointsChamados).FullName!);

    // O chamado, se existir e o usuário puder vê-lo; senão null (e, se existir, a tentativa vai para o log).
    private static async Task<Chamado?> ObterVisivelAsync(Guid id, ClaimsPrincipal usuario, ChamadoServico servico,
        ILoggerFactory registros, CancellationToken cancellationToken)
    {
        if (await servico.ObterAsync(id, cancellationToken) is not { } chamado)
        {
            return null;
        }

        if (!usuario.PodeVer(chamado))
        {
            var logger = Registros(registros);
            Registro.ChamadoDeOutroUsuario(logger, id, usuario.IdDoUsuario() ?? "desconhecido");
            return null;
        }

        return chamado;
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

// Comentários: a mensagem, com o mesmo limite do domínio.
public sealed record ComentarioRequest(
    [Required(ErrorMessage = "Escreva a mensagem.")]
    [StringLength(Chamado.TamanhoMaximoComentario, ErrorMessage = "A mensagem deve ter no máximo {1} caracteres.")]
    string Texto);

public sealed record CancelarRequest(
    [Required(ErrorMessage = "Informe o motivo.")]
    [StringLength(Chamado.TamanhoMaximoJustificativa, ErrorMessage = "O motivo deve ter no máximo {1} caracteres.")]
    string Motivo);
