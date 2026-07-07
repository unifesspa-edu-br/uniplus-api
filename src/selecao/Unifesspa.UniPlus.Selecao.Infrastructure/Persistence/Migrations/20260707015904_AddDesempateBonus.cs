using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesempateBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes_bonus_regional",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    fator = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    teto = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    municipio_convenio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_bonus_regional", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_bonus_regional_processos_seletivos_processo_s",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "criterios_desempate",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    args = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criterios_desempate", x => x.id);
                    table.ForeignKey(
                        name: "fk_criterios_desempate_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_bonus_regional_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_criterios_desempate_processo_ordem",
                schema: "selecao",
                table: "criterios_desempate",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes_bonus_regional",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "criterios_desempate",
                schema: "selecao");
        }
    }
}
