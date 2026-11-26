using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chamados.Infra.Migrations
{
    /// <inheritdoc />
    public partial class IndicePrioridade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Chamados_Prioridade",
                table: "Chamados",
                column: "Prioridade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chamados_Prioridade",
                table: "Chamados");
        }
    }
}
