using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamados.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ResponsavelAtendente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponsavelAnterior",
                table: "EdicoesChamados",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelNovo",
                table: "EdicoesChamados",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelId",
                table: "Chamados",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_ResponsavelId",
                table: "Chamados",
                column: "ResponsavelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chamados_ResponsavelId",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "ResponsavelAnterior",
                table: "EdicoesChamados");

            migrationBuilder.DropColumn(
                name: "ResponsavelNovo",
                table: "EdicoesChamados");

            migrationBuilder.DropColumn(
                name: "ResponsavelId",
                table: "Chamados");
        }
    }
}
