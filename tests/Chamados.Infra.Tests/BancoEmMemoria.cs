using Chamados.Infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Infra.Tests;

/// <summary>
/// SQLite de verdade, mas em memória: o banco existe enquanto a conexão estiver aberta.
/// Cada teste cria o seu, aplica as migrations e descarta no fim. Rápido e isolado.
/// </summary>
internal sealed class BancoEmMemoria : IDbContextFactory<ChamadosDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly DbContextOptions<ChamadosDbContext> _opcoes;

    private BancoEmMemoria()
    {
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<ChamadosDbContext>().UseSqlite(_conexao).Options;
    }

    public static async Task<BancoEmMemoria> CriarAsync()
    {
        var banco = new BancoEmMemoria();
        await using var db = banco.CreateDbContext();
        await db.Database.MigrateAsync();   // as mesmas migrations da aplicação
        return banco;
    }

    public ChamadosDbContext CreateDbContext() => new(_opcoes);

    public ValueTask DisposeAsync() => _conexao.DisposeAsync();
}
