using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCascataRemanejamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes_cascata_remanejamento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    fallback_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_cascata_remanejamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_cascata_remanejamento_processos_seletivos_pro",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "destinos_remanejamento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_cascata_remanejamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    modalidade_destino_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinos_remanejamento", x => x.id);
                    table.CheckConstraint("ck_destinos_remanejamento_ordem_positiva", "ordem >= 1");
                    table.CheckConstraint("ck_destinos_remanejamento_origem_diferente_destino", "modalidade_origem_codigo <> modalidade_destino_codigo");
                    table.ForeignKey(
                        name: "fk_destinos_remanejamento_configuracoes_cascata_remanejamento_",
                        column: x => x.configuracao_cascata_remanejamento_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_cascata_remanejamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[] { new Guid("d0a00000-0000-7000-8000-000000000019"), "ADR-0120; Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 20-A e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 18/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", "REMANEJ-CASCATA-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fallbackCodigo\":\"AC\",\"ordens\":[\n  {\"origem\":\"LB_PPI\",\"destinos\":[\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_Q\",\"destinos\":[\"LB_PPI\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_PCD\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_EP\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_PPI\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_Q\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_PCD\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_EP\"]},\n  {\"origem\":\"LI_EP\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\"]}\n]}", "8e5cfbbf06b2a661b024ae1efc7db8f5013b434b316f6b69c3be9a3c90999626", "[\"oito origens federais, ordem fixa por origem, terminal sempre AC — matriz não recalculada, só aplicada\"]", "criterio_remanejamento", null, "v1" });

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_cascata_remanejamento_processo",
                schema: "selecao",
                table: "configuracoes_cascata_remanejamento",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_destinos_remanejamento_cascata_origem_destino",
                schema: "selecao",
                table: "destinos_remanejamento",
                columns: new[] { "configuracao_cascata_remanejamento_id", "modalidade_origem_codigo", "modalidade_destino_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_destinos_remanejamento_cascata_origem_ordem",
                schema: "selecao",
                table: "destinos_remanejamento",
                columns: new[] { "configuracao_cascata_remanejamento_id", "modalidade_origem_codigo", "ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "destinos_remanejamento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_cascata_remanejamento",
                schema: "selecao");

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000019"));
        }
    }
}
