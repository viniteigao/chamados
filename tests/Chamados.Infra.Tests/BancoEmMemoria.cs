using Chamados.Infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chamados.Infra.Tests;

/// <summary>
/// SQLite de verdade, mas em memória: o banco existe enquanto a conexão estiver aberta.
/// Cada teste cria o seu, aplica as migrations e descarta no fim. Rápido e isolado.
/// Mensagens não lidas: o banco pode parar numa migration anterior (para testar como uma migration
/// trata dados já gravados) e receber interceptadores de comandos (para simular outra requisição
/// gravando no meio de uma operação).
/// </summary>
internal sealed class BancoEmMemoria : IDbContextFactory<ChamadosDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly DbContextOptions<ChamadosDbContext> _opcoes;

    private BancoEmMemoria(IInterceptor[] interceptadores)
    {
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<ChamadosDbContext>()
            .UseSqlite(_conexao)
            .AddInterceptors(interceptadores)
            .Options;
    }

    public static async Task<BancoEmMemoria> CriarAsync(params IInterceptor[] interceptadores)
    {
        var banco = new BancoEmMemoria(interceptadores);
        await using var db = banco.CreateDbContext();
        await db.Database.MigrateAsync();   // as mesmas migrations da aplicação
        return banco;
    }

    /// <summary>Um banco com as migrations até <paramref name="ultimaMigration"/> (inclusive), e só elas.</summary>
    public static async Task<BancoEmMemoria> CriarAteAsync(string ultimaMigration)
    {
        var banco = new BancoEmMemoria([]);
        await banco.MigrarAsync(ultimaMigration);
        return banco;
    }

    /// <summary>Aplica as migrations que faltam até <paramref name="migration"/>; sem ela, até a última.</summary>
    public async Task MigrarAsync(string? migration = null)
    {
        await using var db = CreateDbContext();
        await db.GetService<IMigrator>().MigrateAsync(migration);
    }

    public ChamadosDbContext CreateDbContext() => new(_opcoes);

    public ValueTask DisposeAsync() => _conexao.DisposeAsync();
}
