using Chamados.Infra;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chamados.Web.Tests;

/// <summary>
/// Melhorias: a aplicação em ambiente Production, com o banco "fora do ar" para as consultas:
/// toda listagem e toda estatística falham (Simulacao:FalhaNaListagemACada = 1).
/// É o cenário em que a API respondia HTML e a página de erro caía junto com o banco.
/// </summary>
public sealed class FabricaProducao : WebApplicationFactory<Program>
{
    private readonly string _arquivoDoBanco =
        Path.Combine(Path.GetTempPath(), $"chamados-producao-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.UseSetting("ConnectionStrings:Chamados", $"Data Source={_arquivoDoBanco}");
        builder.UseSetting(SementeUsuarios.ChaveSenha, FabricaChamados.Senha);
        builder.UseSetting("Simulacao:LatenciaMs", "0");
        builder.UseSetting("Simulacao:FalhaNaListagemACada", "1");
        builder.UseSetting($"{LimiteDeLoginOptions.Secao}:{nameof(LimiteDeLoginOptions.TentativasPorMinuto)}", "1000");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Em Production o Program.cs não aplica migrations nem cria usuários: o teste prepara o banco.
        // O login continua funcionando (não lista chamados); o que falha são as consultas da fila.
        host.Services.AplicarMigrationsAsync().GetAwaiter().GetResult();
        SementeUsuarios.PopularAsync(host.Services, host.Services.GetRequiredService<IConfiguration>())
            .GetAwaiter().GetResult();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SqliteConnection.ClearAllPools();
        foreach (var arquivo in new[] { _arquivoDoBanco, _arquivoDoBanco + "-shm", _arquivoDoBanco + "-wal" })
        {
            File.Delete(arquivo);
        }
    }
}
