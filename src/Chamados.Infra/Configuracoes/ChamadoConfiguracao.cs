using Chamados.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chamados.Infra.Configuracoes;

/// <summary>
/// Mapeamento do Chamado com a Fluent API. Fica fora do domínio: o Chamado não sabe
/// que existe banco, e nenhum atributo do EF entra no projeto Chamados.Domain.
/// </summary>
public sealed class ChamadoConfiguracao : IEntityTypeConfiguration<Chamado>
{
    public void Configure(EntityTypeBuilder<Chamado> builder)
    {
        builder.ToTable("Chamados");
        builder.HasKey(c => c.Id);

        // O Id nasce no construtor do Chamado (Guid.NewGuid()), não no banco.
        builder.Property(c => c.Id).ValueGeneratedNever();

        // As mesmas constantes do domínio viram o tamanho máximo no modelo. No SQLite a coluna é TEXT
        // sem limite (quem garante o tamanho é a validação); em SQL Server viraria nvarchar(120).
        builder.Property(c => c.Titulo)
            .HasMaxLength(Chamado.TamanhoMaximoTitulo)
            .IsRequired()
            .UseCollation("NOCASE");   // ordena e compara sem diferenciar maiúsculas (só letras ASCII)

        builder.Property(c => c.Descricao).HasMaxLength(Chamado.TamanhoMaximoDescricao);

        // Propriedades sem set (só get) não viram colunas por convenção: é preciso pedir.
        // O EF grava e lê pelo campo gerado pelo compilador.
        builder.Property(c => c.CriadoEm);
        builder.Property(c => c.Responsavel).HasMaxLength(120);
        builder.Property(c => c.Solucao).HasMaxLength(Chamado.TamanhoMaximoDescricao);

        // Status como texto ("EmAtendimento"): legível em qualquer ferramenta de banco.
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        // Prioridade continua número: o filtro "prioridade mínima" usa >= e a ordenação usa a ordem do enum.
        builder.Property(c => c.Prioridade);

        // Concorrência otimista: o UPDATE leva "WHERE Versao = <versão lida>".
        // Se outra pessoa gravou antes, nenhuma linha muda e o EF lança DbUpdateConcurrencyException.
        builder.Property(c => c.Versao).IsConcurrencyToken();

        // Índices para os filtros mais usados: status (lista e fila) e prioridade mínima.
        // O de prioridade chegou depois, na migration IndicePrioridade: o banco evolui por migrations.
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.Prioridade);

        // Propriedades calculadas não viram colunas.
        builder.Ignore(c => c.EstaEncerrado);
        builder.Ignore(c => c.TempoAteEncerramento);

        // O histórico é parte do chamado (entidade "owned"): tabela própria, chave estrangeira
        // ChamadoId, carregado e gravado sempre junto com o chamado.
        builder.OwnsMany(c => c.Historico, historico =>
        {
            historico.ToTable("HistoricoChamados");
            historico.WithOwner().HasForeignKey("ChamadoId");
            historico.Property<int>("Id");
            historico.HasKey("Id");
            historico.Property(r => r.De).HasConversion<string>().HasMaxLength(20);
            historico.Property(r => r.Para).HasConversion<string>().HasMaxLength(20);
            historico.Property(r => r.Observacao).HasMaxLength(Chamado.TamanhoMaximoDescricao);
        });

        // O EF escreve na lista privada _historico; a propriedade pública é só leitura.
        builder.Navigation(c => c.Historico).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
