using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaALimpezaDasChavesDoArmazenamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chaves_a_limpar",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "A chave do objeto; leva só o id do arquivo."),
                    limpar_apos = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "A partir de quando a chave pode ser apagada."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chaves_a_limpar", x => x.id);
                },
                comment: "Chaves do armazenamento a apagar depois de uma hora: a URL de envio recria o objeto em claro até vencer.");

            migrationBuilder.CreateIndex(
                name: "ix_chaves_a_limpar_limpar_apos",
                schema: "selecao",
                table: "chaves_a_limpar",
                column: "limpar_apos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chaves_a_limpar",
                schema: "selecao");
        }
    }
}
