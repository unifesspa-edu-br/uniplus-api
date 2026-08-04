using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "discentes");

            migrationBuilder.CreateTable(
                name: "sync_run",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_items = table.Column<int>(type: "integer", nullable: false),
                    processed_items = table.Column<int>(type: "integer", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_discente",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_discente_sigaa = table.Column<long>(type: "bigint", nullable: false),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cpf_ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    nivel = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    curso_id = table.Column<int>(type: "integer", nullable: false),
                    curso_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    curso_codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    curso_unidade_id = table.Column<int>(type: "integer", nullable: false),
                    curso_unidade_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    situacao_id = table.Column<int>(type: "integer", nullable: false),
                    situacao_descricao = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    situacao_vinculo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ano_ingresso = table.Column<int>(type: "integer", nullable: false),
                    periodo_ingresso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_discente", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sync_run_started_at",
                schema: "discentes",
                table: "sync_run",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_discente_id_discente_sigaa",
                schema: "discentes",
                table: "vinculo_discente",
                column: "id_discente_sigaa",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_run",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "vinculo_discente",
                schema: "discentes");
        }
    }
}
