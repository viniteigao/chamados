using Chamados.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chamados.Infra;

/// <summary>
/// A sessão com o banco: sabe quais classes viram tabelas (DbSet), como mapeá-las
/// (OnModelCreating) e rastreia o que mudou até o SaveChanges.
/// Vida curta: no Blazor Server, uma instância por operação, criada pela fábrica.
/// Módulo 8: herda de IdentityDbContext, que acrescenta as tabelas AspNetUsers, AspNetRoles,
/// AspNetUserRoles, AspNetUserClaims e outras do ASP.NET Core Identity.
/// </summary>
public sealed class ChamadosDbContext(DbContextOptions<ChamadosDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Chamado> Chamados => Set<Chamado>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Primeiro o mapeamento do Identity: sem esta linha, as tabelas de usuários ficam sem chaves.
        base.OnModelCreating(builder);

        // Cada entidade nossa tem sua classe de configuração (Fluent API) na pasta Configuracoes.
        builder.ApplyConfigurationsFromAssembly(typeof(ChamadosDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // O SQLite não tem tipo de data com fuso: o EF guardaria DateTimeOffset como texto e
        // não conseguiria ordenar nem comparar no SQL. Como número (binário), ORDER BY funciona.
        // A documentação do provedor sugere outra saída, trocar por DateTime em UTC; aqui o domínio
        // fica como está e o conversor nativo resolve. Custo: frações abaixo do milissegundo se perdem.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
