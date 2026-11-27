using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Chamados.Domain;
using Chamados.Web;
using Chamados.Web.Api;
using Chamados.Web.Components;
using Chamados.Web.Configuracao;
using Chamados.Web.Diagnostico;
using Chamados.Web.Estado;
using Chamados.Web.Seguranca;
using Chamados.Infra;
using Microsoft.AspNetCore.Identity;
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

// Métricas de negócio (Módulo 8): medidor "Chamados", lido por dotnet-counters ou OpenTelemetry.
builder.Services.AddSingleton<MetricasChamados>();

// Prazos: política de prazos por prioridade, da seção "Prazos" (Configuracao/PrazosOptions.cs),
// conferida pela regra do domínio na inicialização. O ChamadoServico a recebe por injeção.
builder.Services.AddOptions<PrazosOptions>()
    .BindConfiguration(PrazosOptions.Secao)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PrazosOptions>, ValidacaoDePrazos>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PrazosOptions>>().Value.ParaPolitica());

// ============ Segurança (Módulo 8) ============

// ASP.NET Core Identity: usuários, senhas (só o hash), papéis e claims, guardados pelo EF Core
// nas tabelas AspNet* do mesmo banco. AddIdentity também registra o login por cookie.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(opcoes =>
    {
        opcoes.Password.RequiredLength = 10;
        opcoes.Lockout.MaxFailedAccessAttempts = 5;
        opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        opcoes.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ChamadosDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opcoes =>
{
    opcoes.Cookie.Name = EncaminharCookieHandler.NomeDoCookie;
    opcoes.Cookie.HttpOnly = true;                        // JavaScript não lê o cookie (padrão; aqui, explícito)
    opcoes.Cookie.SameSite = SameSiteMode.Lax;            // não vai em POST vindo de outro site
    opcoes.LoginPath = "/Conta/Entrar";
    opcoes.LogoutPath = "/Conta/Sair";
    opcoes.AccessDeniedPath = "/Conta/AcessoNegado";
    opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);        // um turno de trabalho
    opcoes.SlidingExpiration = true;
});

// Token para clientes que não são navegador (o .http hoje, o app MAUI no Módulo 10).
// Opaco e protegido pela Data Protection do ASP.NET Core, como o cookie.
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme, opcoes => opcoes.BearerTokenExpiration = TimeSpan.FromHours(1));

// Políticas de autorização (Seguranca/Politicas.cs), com fallback: tudo exige login.
builder.Services.AddPoliticasChamados();

// Melhorias: limite de tentativas de login por origem (Seguranca/LimiteDeLogin.cs).
builder.Services.AddLimiteDeLogin();

// Blazor: o estado de autenticação chega aos componentes como parâmetro em cascata.
builder.Services.AddCascadingAuthenticationState();

// Estado compartilhado entre componentes Blazor. Scoped = um por circuito (uma aba conectada).
builder.Services.AddScoped<EstadoFila>();

// Interface web: Razor Pages (formulários) e JSON da API.
builder.Services.AddRazorPages(opcoes =>
    {
        // Exceções à política de fallback: login e erro abertos; edição só para atendentes.
        opcoes.Conventions.AllowAnonymousToFolder("/Conta");
        opcoes.Conventions.AllowAnonymousToPage("/Erro");
        opcoes.Conventions.AuthorizePage("/Chamados/Editar", Politicas.Atender);
    })
    .AddMvcOptions(options =>
    {
        // Erros de conversão do model binding ("abc" num campo numérico, valor fora do enum)
        // acontecem antes das Data Annotations e vêm em inglês por padrão.
        var mensagens = options.ModelBindingMessageProvider;
        mensagens.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        mensagens.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetValueMustNotBeNullAccessor(_ => "Preencha este campo.");
    });
builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
{
    // Melhorias: o erro 500 da API (UseExceptionHandler, só em produção) sai em português, como os
    // outros Problem Details do projeto, e nunca com o detalhe da exceção: esse fica no log.
    if (contexto.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
    {
        contexto.ProblemDetails.Title = "Erro interno";
        contexto.ProblemDetails.Detail = "Não foi possível concluir a operação. Se o problema continuar, informe o traceId ao suporte.";
    }
});

// Blazor: componentes Razor renderizados no servidor, com interatividade por conexão (SignalR).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Módulo 7: validação automática dos corpos das Minimal APIs (Data Annotations) e documento OpenAPI.
builder.Services.AddValidation();
builder.Services.AddOpenApi();

// Cliente HTTP tipado da própria API. A página /indicadores o usa, e o app MAUI do Módulo 10 usará o mesmo contrato.
// Módulo 8: a API exige login; o handler leva o cookie do usuário na chamada feita pelo servidor.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<EncaminharCookieHandler>();
builder.Services.AddHttpClient<ChamadosApiCliente>(cliente =>
        cliente.BaseAddress = new Uri(builder.Configuration["Api:UrlBase"]
            ?? throw new InvalidOperationException("Configure Api:UrlBase.")))
    .AddHttpMessageHandler<EncaminharCookieHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Development: cria ou atualiza o banco (migrations pendentes) e, se estiver vazio, popula.
    // Em produção, as migrations são aplicadas por script revisado, não na inicialização.
    await app.Services.AplicarMigrationsAsync();
    // Meus chamados: usuários antes dos chamados, porque os exemplos têm dono.
    await SementeUsuarios.PopularAsync(app.Services, app.Configuration);
    await SementeDados.PopularAsync(app.Services);
}

// ============ 2. Pipeline: por onde cada requisição passa, nesta ordem ============

// Middleware próprio: mede cada requisição. Registra método, caminho e status;
// nunca a query string nem o corpo, que podem carregar dados pessoais.
var registroRequisicoes = app.Logger;
app.Use(async (contexto, proximo) =>
{
    var inicio = Stopwatch.GetTimestamp();
    await proximo(contexto);
    // Valores simples nos argumentos (CA1873): o método gerado confere o nível antes de formatar.
    var duracao = Stopwatch.GetElapsedTime(inicio);
    Registro.RequisicaoConcluida(registroRequisicoes, contexto.Request.Method, contexto.Request.Path.Value ?? "/",
        contexto.Response.StatusCode, duracao.TotalMilliseconds);
});

// Cabeçalhos de segurança (CSP, nosniff, frame) em todas as respostas.
app.UseCabecalhosSeguranca(app.Environment.IsDevelopment());

if (!app.Environment.IsDevelopment())
{
    // Em produção: erro sem stack trace e HSTS.
    // Em Development o ASP.NET Core já mostra a página de exceção do desenvolvedor.
    // Melhorias: antes, /Erro valia para tudo, e um erro 500 da API respondia a página HTML.
    // Agora a API responde Problem Details em JSON (com traceId), e as páginas, a página /Erro.
    app.UseWhen(EhApi, ramo => ramo.UseExceptionHandler());
    app.UseWhen(contexto => !EhApi(contexto), ramo => ramo.UseExceptionHandler("/Erro"));
    app.UseHsts();
}

// Respostas de erro sem corpo (404 de rota inexistente, NotFound() de uma página, 400)
// são reexecutadas em /Erro/{código}, com o layout do site. Só fora de /api:
// a API continua respondendo JSON, nunca HTML.
app.UseWhen(
    contexto => !EhApi(contexto),
    ramo => ramo.UseStatusCodePagesWithReExecute("/Erro/{0}"));

// Cultura pt-BR para formatar datas e números, nas páginas e nos circuitos do Blazor.
// A primeira cultura da lista é a padrão; outras entrariam aqui quando houver tradução.
app.UseRequestLocalization("pt-BR");

app.UseHttpsRedirection();
app.UseRouting();

// Melhorias: limite de tentativas de login. Depois do roteamento, porque o limite vale só
// para os endpoints que o pedem (formulário /Conta/Entrar e POST /api/conta/token).
app.UseRateLimiter();

// Quem é o usuário (cookie ou token) e o que ele pode fazer. Depois do roteamento,
// porque a autorização precisa saber qual endpoint foi escolhido e quais regras ele declara.
app.UseAuthentication();
app.UseAuthorization();

// Exigido pelos endpoints de componentes Razor: confere o token dos formulários Blazor.
// As Razor Pages continuam validando o próprio token como antes.
app.UseAntiforgery();

// ============ 3. Endpoints: quem responde a cada rota ============

// CSS, JavaScript e o blazor.web.js: públicos, senão a própria página de login sairia sem estilo.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
app.MapApiChamados();
app.MapApiConta();

// Páginas Blazor (componentes com @page), convivendo com as Razor Pages na mesma aplicação.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/status", () => Enum.GetNames<StatusChamado>())
    .RequireAuthorization(Politicas.Api);

// Diagnóstico exposto somente em Development:
// em produção, revelar a versão do runtime ajuda um atacante.
if (app.Environment.IsDevelopment())
{
    // Documento OpenAPI só em Development: em produção, publicar a descrição da API é uma decisão explícita.
    app.MapOpenApi().RequireAuthorization(Politicas.Api);

    app.MapGet("/api/ambiente", (IHostEnvironment env) => new
    {
        env.EnvironmentName,
        Runtime = RuntimeInformation.FrameworkDescription
    }).RequireAuthorization(Politicas.Api);
}

app.Run();

// Melhorias: um critério só para "é requisição da API", usado no tratamento de erros.
static bool EhApi(HttpContext contexto) => contexto.Request.Path.StartsWithSegments("/api");
