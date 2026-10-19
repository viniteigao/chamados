using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Chamados.Domain;
using Chamados.Web;

var builder = WebApplication.CreateBuilder(args);

// --- Composição: quem implementa cada dependência e por quanto tempo vive ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IChamadoRepositorio>(sp =>
    new RepositorioChamadosEmMemoria(
        sp.GetRequiredService<TimeProvider>(),
        TimeSpan.FromMilliseconds(builder.Configuration.GetValue("Simulacao:LatenciaMs", 0))));
builder.Services.AddScoped<ChamadoServico>();

// Enums como texto no JSON (resposta da Q10 do Módulo 1).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await SementeDados.PopularAsync(app.Services);
}

app.MapGet("/", () => "Chamados.Web em execução");

app.MapGet("/status", () => Enum.GetNames<StatusChamado>());

app.MapGet("/chamados", async (
    StatusChamado? status,
    Prioridade? prioridadeMinima,
    string? texto,
    ChamadoServico servico,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        var filtro = new FiltroChamados(status, prioridadeMinima, texto);
        return Results.Ok(await servico.ListarAsync(filtro, cancellationToken));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // O cliente desistiu (fechou a aba, timeout): não há para quem responder.
        logger.LogInformation("Listagem de chamados cancelada pelo cliente.");
        return Results.Empty;
    }
});

app.MapGet("/chamados/estatisticas", (ChamadoServico servico, CancellationToken cancellationToken)
    => servico.ObterEstatisticasAsync(cancellationToken));

app.MapGet("/chamados/{id:guid}", async (Guid id, IChamadoRepositorio repositorio, CancellationToken cancellationToken)
    => await repositorio.ObterAsync(id, cancellationToken) is { } chamado
        ? Results.Ok(ResumoChamado.De(chamado))
        : Results.NotFound());

// --- Transições: as três rotas compartilham o mesmo tratamento de erros ---
app.MapPost("/chamados/{id:guid}/atendimento", (
    Guid id, IniciarAtendimentoRequest requisicao, ChamadoServico servico, CancellationToken cancellationToken)
    => ExecutarTransicaoAsync(() => servico.IniciarAtendimentoAsync(id, requisicao.Responsavel, cancellationToken)));

app.MapPost("/chamados/{id:guid}/resolucao", (
    Guid id, ResolverRequest requisicao, ChamadoServico servico, CancellationToken cancellationToken)
    => ExecutarTransicaoAsync(() => servico.ResolverAsync(id, requisicao.Solucao, cancellationToken)));

app.MapPost("/chamados/{id:guid}/cancelamento", (
    Guid id, CancelarRequest requisicao, ChamadoServico servico, CancellationToken cancellationToken)
    => ExecutarTransicaoAsync(() => servico.CancelarAsync(id, requisicao.Motivo, cancellationToken)));

// Diagnóstico exposto somente em Development:
// em produção, revelar a versão do runtime ajuda um atacante.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/ambiente", (IHostEnvironment env) => new
    {
        env.EnvironmentName,
        Runtime = RuntimeInformation.FrameworkDescription
    });
}

app.Run();

// Exceções de domínio viram respostas HTTP aqui, no limite da aplicação.
// O mais específico primeiro: ArgumentException por último porque também pega as derivadas.
static async Task<IResult> ExecutarTransicaoAsync(Func<Task<Chamado>> transicao)
{
    try
    {
        return Results.Ok(ResumoChamado.De(await transicao()));
    }
    catch (ChamadoNaoEncontradoException)
    {
        return Results.NotFound();
    }
    catch (TransicaoInvalidaException ex)
    {
        return Results.Conflict(new { erro = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { erro = ex.Message });
    }
}

public sealed record IniciarAtendimentoRequest(string Responsavel);

public sealed record ResolverRequest(string Solucao);

public sealed record CancelarRequest(string Motivo);