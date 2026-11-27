using Chamados.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chamados.Infra.Configuracoes;

/// <summary>
/// Mensagens não lidas: mapeamento da tabela LeiturasConversas (migration LeiturasDasConversas).
/// Não é parte do chamado (não é "owned"): ler não altera o chamado, e cada usuário tem a sua linha.
/// </summary>
public sealed class LeituraConversaConfiguracao : IEntityTypeConfiguration<LeituraConversa>
{
    public void Configure(EntityTypeBuilder<LeituraConversa> builder)
    {
        builder.ToTable("LeiturasConversas");

        // Chave composta: uma leitura por chamado e usuário. A chave começa pelo chamado, então o mesmo
        // índice atende à consulta das listas ("a leitura deste usuário neste chamado").
        builder.HasKey(l => new { l.ChamadoId, l.UsuarioId });
        builder.Property(l => l.UsuarioId).HasMaxLength(Autor.TamanhoMaximoId);

        // Chave estrangeira para o chamado, sem propriedade de navegação: o Chamado não sabe que esta
        // tabela existe. Apagar o chamado apaga as leituras dele.
        builder.HasOne<Chamado>()
            .WithMany()
            .HasForeignKey(l => l.ChamadoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
