using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamados.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AutoriaDosChamados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PorId",
                table: "HistoricoChamados",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PorNome",
                table: "HistoricoChamados",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbertoPorId",
                table: "Chamados",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbertoPorNome",
                table: "Chamados",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_AbertoPorId",
                table: "Chamados",
                column: "AbertoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chamados_AbertoPorId",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "PorId",
                table: "HistoricoChamados");

            migrationBuilder.DropColumn(
                name: "PorNome",
                table: "HistoricoChamados");

            migrationBuilder.DropColumn(
                name: "AbertoPorId",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "AbertoPorNome",
                table: "Chamados");
        }
    }
}
