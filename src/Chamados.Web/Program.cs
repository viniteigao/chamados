using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Chamados.Domain;
using Chamados.Web;
using Chamados.Web.Api;
using Chamados.Web.Configuracao;
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
builder.Services.AddSingleton<IChamadoRepositorio>(sp =>
{
    var simulacao = sp.GetRequiredService<IOptions<SimulacaoOptions>>().Value;
    return new RepositorioChamadosEmMemoria(
        sp.GetRequiredService<TimeProvider>(),
        TimeSpan.FromMilliseconds(simulacao.LatenciaMs));
});
builder.Services.AddScoped<ChamadoServico>();

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
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
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

app.UseHttpsRedirection();
app.UseRouting();

// ============ 3. Endpoints: quem responde a cada rota ============

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapApiChamados();

app.MapGet("/api/status", () => Enum.GetNames<StatusChamado>());

// Diagnóstico exposto somente em Development:
// em produção, revelar a versão do runtime ajuda um atacante.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/ambiente", (IHostEnvironment env) => new
    {
        env.EnvironmentName,
        Runtime = RuntimeInformation.FrameworkDescription
    });
}

app.Run();
