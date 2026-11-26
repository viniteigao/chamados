using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamados.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chamados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false, collation: "NOCASE"),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Prioridade = table.Column<int>(type: "INTEGER", nullable: false),
                    Versao = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Responsavel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Solucao = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CriadoEm = table.Column<long>(type: "INTEGER", nullable: false),
                    EncerradoEm = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chamados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoChamados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    De = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Para = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Em = table.Column<long>(type: "INTEGER", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ChamadoId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoChamados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricoChamados_Chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "Chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_Status",
                table: "Chamados",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoChamados_ChamadoId",
                table: "HistoricoChamados",
                column: "ChamadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricoChamados");

            migrationBuilder.DropTable(
                name: "Chamados");
        }
    }
}
