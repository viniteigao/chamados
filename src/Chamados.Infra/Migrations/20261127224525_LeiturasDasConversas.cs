using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamados.Infra.Migrations
{
    /// <summary>
    /// Mensagens não lidas: tabela LeiturasConversas, com até quando cada usuário leu cada conversa.
    /// Num banco que já tem conversas, quem escreveu começa com a conversa lida até a própria última
    /// mensagem (quem responde leu o que veio antes, a mesma regra de quando se comenta pela aplicação).
    /// </summary>
    public partial class LeiturasDasConversas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeiturasConversas",
                columns: table => new
                {
                    ChamadoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    LidoAte = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeiturasConversas", x => new { x.ChamadoId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_LeiturasConversas_Chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "Chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Escrito à mão: a migration gerada só cria a tabela. As datas já estão gravadas como número
            // (o mesmo conversor nas duas tabelas), então MAX("Em") é a mensagem mais recente de cada autor.
            migrationBuilder.Sql(
                """
                INSERT INTO "LeiturasConversas" ("ChamadoId", "UsuarioId", "LidoAte")
                SELECT "ChamadoId", "AutorId", MAX("Em")
                FROM "ComentariosChamados"
                GROUP BY "ChamadoId", "AutorId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeiturasConversas");
        }
    }
}
