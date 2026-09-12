using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BancasRecursosEJanelaDaEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "emite_parecer_individual",
                schema: "selecao",
                table: "etapas_processo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fim",
                schema: "selecao",
                table: "etapas_processo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "inicio",
                schema: "selecao",
                table: "etapas_processo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bancas_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_banca_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancas_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_bancas_da_etapa_etapas_processo_etapa_processo_id",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recursos_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ancora = table.Column<int>(type: "integer", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    regra_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    prazo_valor = table.Column<decimal>(type: "numeric", nullable: false),
                    prazo_unidade = table.Column<int>(type: "integer", nullable: false),
                    susp_1a_valor = table.Column<decimal>(type: "numeric", nullable: true),
                    susp_1a_unidade = table.Column<int>(type: "integer", nullable: true),
                    susp_2a_valor = table.Column<decimal>(type: "numeric", nullable: true),
                    susp_2a_unidade = table.Column<int>(type: "integer", nullable: true),
                    produto_ancora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recursos_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_recursos_da_etapa_etapas_processo_etapa_processo_id",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_bancas_da_etapa_codigo",
                schema: "selecao",
                table: "bancas_da_etapa",
                columns: new[] { "etapa_processo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recursos_da_etapa_etapa_processo_id",
                schema: "selecao",
                table: "recursos_da_etapa",
                column: "etapa_processo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bancas_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "recursos_da_etapa",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "emite_parecer_individual",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "fim",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "inicio",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
