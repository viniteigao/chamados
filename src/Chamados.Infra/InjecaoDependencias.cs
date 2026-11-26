using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Infra;

public static class InjecaoDependencias
{
    /// <summary>
    /// Registra o EF Core com SQLite e o repositório. A fábrica de DbContext (Singleton) cria
    /// um contexto por operação; o repositório não guarda estado e pode ser Singleton também.
    /// </summary>
    public static IServiceCollection AddPersistenciaChamados(this IServiceCollection services, string stringDeConexao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stringDeConexao);

        services.AddDbContextFactory<ChamadosDbContext>(opcoes => opcoes.UseSqlite(stringDeConexao));
        services.AddSingleton<RepositorioChamadosEf>();
        return services;
    }

    /// <summary>Aplica as migrations pendentes. Usado na inicialização, só em Development.</summary>
    public static async Task AplicarMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var fabrica = services.GetRequiredService<IDbContextFactory<ChamadosDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
