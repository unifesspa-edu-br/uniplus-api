using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracaoDerivacaoFato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes_derivacao_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_derivacao_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_derivacao_fato_processos_seletivos_processo_s",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_derivacao_configuradas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_derivacao_fato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    contribui = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_derivacao_configuradas", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_derivacao_configuradas_configuracoes_derivacao_fato_",
                        column: x => x.configuracao_derivacao_fato_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_derivacao_fato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condicoes_regra_derivacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_derivacao_configurada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula = table.Column<int>(type: "integer", nullable: false),
                    fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_regra_derivacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_regra_derivacao_regra_derivacao_configurada_regra",
                        column: x => x.regra_derivacao_configurada_id,
                        principalSchema: "selecao",
                        principalTable: "regras_derivacao_configuradas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_regra_derivacao_regra_derivacao_configurada_id",
                schema: "selecao",
                table: "condicoes_regra_derivacao",
                column: "regra_derivacao_configurada_id");

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_derivacao_fato_processo_fato",
                schema: "selecao",
                table: "configuracoes_derivacao_fato",
                columns: new[] { "processo_seletivo_id", "codigo_fato" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_regras_derivacao_configuradas_config_ordem",
                schema: "selecao",
                table: "regras_derivacao_configuradas",
                columns: new[] { "configuracao_derivacao_fato_id", "ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condicoes_regra_derivacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_derivacao_configuradas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_derivacao_fato",
                schema: "selecao");
        }
    }
}
