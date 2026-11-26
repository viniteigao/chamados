using Chamados.Web.Api;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Web.Tests;

/// <summary>
/// A aplicação inteira, com o mesmo Program.cs, rodando em memória para os testes.
/// Cada classe de teste ganha a sua (IClassFixture) e, com ela, um banco SQLite próprio,
/// criado pelas migrations e populado pelas sementes, como em Development.
/// </summary>
public sealed class FabricaChamados : WebApplicationFactory<Program>
{
    // Senha nova a cada execução dos testes: nada fixo no código, nada dos Segredos de ninguém.
    // "Teste#1" garante maiúscula, símbolo e número; o Guid completa o tamanho.
    public static readonly string Senha = $"Teste#1{Guid.NewGuid():N}";

    private readonly string _arquivoDoBanco =
        Path.Combine(Path.GetTempPath(), $"chamados-teste-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting vale antes do builder.Build() do Program.cs: a string de conexão já chega trocada.
        builder.UseSetting("ConnectionStrings:Chamados", $"Data Source={_arquivoDoBanco}");
        builder.UseSetting(SementeUsuarios.ChaveSenha, Senha);
        builder.UseSetting("Simulacao:LatenciaMs", "0");
        builder.UseSetting("Api:UrlBase", "http://localhost/");

        builder.ConfigureTestServices(services =>
            // A página /indicadores chama a API por HTTP. Nos testes não há porta aberta: o cliente
            // tipado passa a falar direto com o servidor em memória.
            services.AddHttpClient<ChamadosApiCliente>()
                .ConfigurePrimaryHttpMessageHandler(sp =>
                    ((TestServer)sp.GetRequiredService<IServer>()).CreateHandler()));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // O SQLite mantém conexões num pool: libera antes de apagar o arquivo.
        SqliteConnection.ClearAllPools();
        foreach (var arquivo in new[] { _arquivoDoBanco, _arquivoDoBanco + "-shm", _arquivoDoBanco + "-wal" })
        {
            File.Delete(arquivo);
        }
    }
}
