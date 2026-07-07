using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes_classificacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_calculo_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_calculo_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_calculo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    regra_arredondamento_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    regra_arredondamento_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    regra_arredondamento_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    casas_arredondamento = table.Column<int>(type: "integer", nullable: true),
                    regra_ordem_alocacao_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_ordem_alocacao_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_ordem_alocacao_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    n_opcoes_alocacao = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_classificacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_classificacao_processos_seletivos_processo_se",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_eliminacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_classificacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    args = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_eliminacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_eliminacao_configuracoes_classificacao_configuracao_",
                        column: x => x.configuracao_classificacao_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_classificacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_classificacao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_classificacao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regras_eliminacao_configuracao_classificacao_id",
                schema: "selecao",
                table: "regras_eliminacao",
                column: "configuracao_classificacao_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regras_eliminacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_classificacao",
                schema: "selecao");
        }
    }
}
