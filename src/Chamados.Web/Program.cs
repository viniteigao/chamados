using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Chamados.Domain;
using Chamados.Web;
using Chamados.Web.Api;
using Chamados.Web.Components;
using Chamados.Web.Configuracao;
using Chamados.Web.Estado;
using Chamados.Infra;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ============ 1. Serviços: o que a aplicação sabe fazer ============

// Configuração tipada e validada já na inicialização.
builder.Services.AddOptions<SimulacaoOptions>()
    .BindConfiguration(SimulacaoOptions.Secao)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Camada de negócio (Módulo 2).
builder.Services.AddSingleton(TimeProvider.System);

// Persistência (Módulo 7): EF Core com SQLite. A string de conexão vem da configuração,
// nunca do código: em produção, de variável de ambiente ou de um cofre de segredos.
var stringDeConexao = builder.Configuration.GetConnectionString("Chamados")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Chamados.");
builder.Services.AddPersistenciaChamados(stringDeConexao);

builder.Services.AddSingleton<IChamadoRepositorio>(sp =>
{
    IChamadoRepositorio repositorio = sp.GetRequiredService<RepositorioChamadosEf>();

    // Módulo 6: latência e falhas simuladas, só quando configuradas (Development).
    // Decorator: mesma interface, comportamento acrescentado por fora do repositório real.
    var simulacao = sp.GetRequiredService<IOptions<SimulacaoOptions>>().Value;
    return simulacao.LatenciaMs > 0 || simulacao.FalhaNaListagemACada > 0
        ? new RepositorioSimulado(repositorio, sp.GetRequiredService<TimeProvider>(), simulacao)
        : repositorio;
});
builder.Services.AddScoped<ChamadoServico>();

// Estado compartilhado entre componentes Blazor. Scoped = um por circuito (uma aba conectada).
builder.Services.AddScoped<EstadoFila>();

// Interface web: Razor Pages (formulários) e JSON da API.
builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        // Erros de conversão do model binding ("abc" num campo numérico, valor fora do enum)
        // acontecem antes das Data Annotations e vêm em inglês por padrão.
        var mensagens = options.ModelBindingMessageProvider;
        mensagens.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        mensagens.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetValueMustNotBeNullAccessor(_ => "Preencha este campo.");
    });
builder.Services.AddProblemDetails();

// Blazor: componentes Razor renderizados no servidor, com interatividade por conexão (SignalR).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Módulo 7: validação automática dos corpos das Minimal APIs (Data Annotations) e documento OpenAPI.
builder.Services.AddValidation();
builder.Services.AddOpenApi();

// Cliente HTTP tipado da própria API. A página /indicadores o usa, e o app MAUI do Módulo 10 usará o mesmo contrato.
builder.Services.AddHttpClient<ChamadosApiCliente>(cliente =>
    cliente.BaseAddress = new Uri(builder.Configuration["Api:UrlBase"]
        ?? throw new InvalidOperationException("Configure Api:UrlBase.")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Development: cria ou atualiza o banco (migrations pendentes) e, se estiver vazio, popula.
    // Em produção, as migrations são aplicadas por script revisado, não na inicialização.
    await app.Services.AplicarMigrationsAsync();
    await SementeDados.PopularAsync(app.Services);
}

// ============ 2. Pipeline: por onde cada requisição passa, nesta ordem ============

// Middleware próprio: mede cada requisição. Registra método, caminho e status;
// nunca a query string nem o corpo, que podem carregar dados pessoais.
app.Use(async (contexto, proximo) =>
{
    var inicio = Stopwatch.GetTimestamp();
    await proximo(contexto);
    app.Logger.LogInformation("{Metodo} {Caminho} respondeu {Status} em {Duracao:F1} ms",
        contexto.Request.Method, contexto.Request.Path, contexto.Response.StatusCode,
        Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
});

if (!app.Environment.IsDevelopment())
{
    // Em produção: página de erro genérica (sem stack trace) e HSTS.
    // Em Development o ASP.NET Core já mostra a página de exceção do desenvolvedor.
    app.UseExceptionHandler("/Erro");
    app.UseHsts();
}

// Respostas de erro sem corpo (404 de rota inexistente, NotFound() de uma página, 400)
// são reexecutadas em /Erro/{código}, com o layout do site. Só fora de /api:
// a API continua respondendo JSON, nunca HTML.
app.UseWhen(
    contexto => !contexto.Request.Path.StartsWithSegments("/api"),
    ramo => ramo.UseStatusCodePagesWithReExecute("/Erro/{0}"));

// Cultura pt-BR para formatar datas e números, nas páginas e nos circuitos do Blazor.
// A primeira cultura da lista é a padrão; outras entrariam aqui quando houver tradução.
app.UseRequestLocalization("pt-BR");

app.UseHttpsRedirection();
app.UseRouting();

// Exigido pelos endpoints de componentes Razor: confere o token dos formulários Blazor.
// As Razor Pages continuam validando o próprio token como antes.
app.UseAntiforgery();

// ============ 3. Endpoints: quem responde a cada rota ============

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapApiChamados();

// Páginas Blazor (componentes com @page), convivendo com as Razor Pages na mesma aplicação.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/status", () => Enum.GetNames<StatusChamado>());

// Diagnóstico exposto somente em Development:
// em produção, revelar a versão do runtime ajuda um atacante.
if (app.Environment.IsDevelopment())
{
    // Documento OpenAPI só em Development: em produção, publicar a descrição da API é uma decisão explícita.
    app.MapOpenApi();

    app.MapGet("/api/ambiente", (IHostEnvironment env) => new
    {
        env.EnvironmentName,
        Runtime = RuntimeInformation.FrameworkDescription
    });
}

app.Run();
