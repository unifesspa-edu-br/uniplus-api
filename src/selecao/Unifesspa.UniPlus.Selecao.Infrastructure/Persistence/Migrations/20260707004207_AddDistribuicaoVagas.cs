using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDistribuicaoVagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracoes_distribuicao_vagas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oferta_curso_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vo_base = table.Column<int>(type: "integer", nullable: false),
                    pr = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    regra_distribuicao_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_distribuicao_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_distribuicao_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    referencia_demografica_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referencia_demografica_censo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    referencia_demografica_ppi_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    referencia_demografica_quilombola_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    referencia_demografica_pcd_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    referencia_demografica_base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_distribuicao_vagas", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_distribuicao_vagas_processos_seletivos_proces",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modalidades_selecionadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_distribuicao_vagas_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    natureza_legal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_vagas = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    regra_remanejamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    remanejamento_destino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    remanejamento_par = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    remanejamento_fallback = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    criterios_cumulativos = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    acao_quando_indeferido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modalidades_selecionadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_modalidades_selecionadas_configuracoes_distribuicao_vagas_c",
                        column: x => x.configuracao_distribuicao_vagas_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_distribuicao_vagas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_distribuicao_vagas_processo_oferta",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                columns: new[] { "processo_seletivo_id", "oferta_curso_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modalidades_selecionadas_configuracao_distribuicao_vagas_id",
                schema: "selecao",
                table: "modalidades_selecionadas",
                column: "configuracao_distribuicao_vagas_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modalidades_selecionadas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_distribuicao_vagas",
                schema: "selecao");
        }
    }
}
