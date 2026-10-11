using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistraNoComprovanteAsModalidadesEmQueOCandidatoConcorre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "modalidades",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "jsonb",
                nullable: true,
                comment: "As modalidades em que o candidato concorre nesta submissão, com o nome congelado e a ordem das ofertadas; nulo quando a versão não deriva a modalidade.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modalidades",
                schema: "selecao",
                table: "comprovantes_inscricao");
        }
    }
}
