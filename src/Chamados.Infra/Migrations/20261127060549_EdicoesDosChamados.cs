using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamados.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EdicoesDosChamados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdicoesChamados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Em = table.Column<long>(type: "INTEGER", nullable: false),
                    TituloAnterior = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    TituloNovo = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    PrioridadeAnterior = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PrioridadeNova = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    DescricaoAlterada = table.Column<bool>(type: "INTEGER", nullable: false),
                    PorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    PorNome = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChamadoId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdicoesChamados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdicoesChamados_Chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "Chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdicoesChamados_ChamadoId",
                table: "EdicoesChamados",
                column: "ChamadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdicoesChamados");
        }
    }
}
