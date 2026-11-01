using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOCpfCifradoDaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cpf_ciphertext",
                schema: "selecao",
                table: "inscricoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "cpf_ciphertext",
                schema: "selecao",
                table: "inscricoes",
                type: "bytea",
                nullable: false,
                defaultValue: Array.Empty<byte>(),
                comment: "CPF cifrado em repouso (AES-GCM, ADR-0121) — envelope autenticado; nunca texto claro.");
        }
    }
}
