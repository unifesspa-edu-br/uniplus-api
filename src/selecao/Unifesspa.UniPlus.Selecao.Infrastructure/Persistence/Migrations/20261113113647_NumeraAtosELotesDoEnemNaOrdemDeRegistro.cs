using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NumeraAtosELotesDoEnemNaOrdemDeRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lotes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropIndex(
                name: "ix_atos_do_processo_processo_recentes",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AddColumn<long>(
                name: "ordem_de_registro",
                schema: "selecao",
                table: "atos_do_processo",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.CreateIndex(
                name: "ix_lotes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "importado_em", "ordem_de_registro" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_lotes_de_notas_do_enem_ordem_de_registro",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_processo_recentes",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "created_at", "ordem_de_registro" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_ordem_de_registro",
                schema: "selecao",
                table: "atos_do_processo",
                column: "ordem_de_registro",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lotes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropIndex(
                name: "ux_lotes_de_notas_do_enem_ordem_de_registro",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropIndex(
                name: "ix_atos_do_processo_processo_recentes",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.DropIndex(
                name: "ux_atos_do_processo_ordem_de_registro",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "selecao",
                table: "lotes_de_notas_do_enem");

            migrationBuilder.DropColumn(
                name: "ordem_de_registro",
                schema: "selecao",
                table: "atos_do_processo");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "importado_em", "id" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_processo_recentes",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "created_at", "id" },
                descending: new[] { false, true, true });
        }
    }
}
