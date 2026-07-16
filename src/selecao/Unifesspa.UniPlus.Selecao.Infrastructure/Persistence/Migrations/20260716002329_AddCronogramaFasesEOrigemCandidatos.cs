using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCronogramaFasesEOrigemCandidatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "origem_candidatos",
                schema: "selecao",
                table: "processos_seletivos",
                type: "integer",
                nullable: false,
                // Os processos já existentes antecedem a declaração explícita da
                // origem. O comportamento histórico mais restritivo era exigir a
                // configuração local de inscrição; portanto o backfill preserva essa
                // segurança com InscricaoPropria (1), nunca o sentinela Nenhuma (0).
                // Este último é inválido para criação e faria o gate ignorar o piso
                // de coleta em processos legados.
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "fases_cronograma",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    fase_canonica_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    dono_institucional = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    origem_data = table.Column<int>(type: "integer", nullable: false),
                    agrupa_etapas = table.Column<bool>(type: "boolean", nullable: false),
                    permite_complementacao = table.Column<bool>(type: "boolean", nullable: false),
                    produz_resultado = table.Column<bool>(type: "boolean", nullable: false),
                    resultado_definitivo = table.Column<bool>(type: "boolean", nullable: false),
                    coleta_inscricao = table.Column<bool>(type: "boolean", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ato_produzido_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ato_produzido_efeito_irreversivel = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fases_cronograma", x => x.id);
                    table.ForeignKey(
                        name: "fk_fases_cronograma_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bancas_requeridas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_banca_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancas_requeridas", x => x.id);
                    table.ForeignKey(
                        name: "fk_bancas_requeridas_fases_cronograma_fase_cronograma_id",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_recurso_fase",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    prazo_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    prazo_unidade = table.Column<int>(type: "integer", nullable: false),
                    ato_ancora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    suspensividade_1a_instancia_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    suspensividade_1a_instancia_unidade = table.Column<int>(type: "integer", nullable: true),
                    suspensividade_2a_instancia_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    suspensividade_2a_instancia_unidade = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_recurso_fase", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_recurso_fase_fases_cronograma_fase_cronograma_id",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bancas_requeridas_fase_cronograma_id",
                schema: "selecao",
                table: "bancas_requeridas",
                column: "fase_cronograma_id");

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_fase_canonica",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "fase_canonica_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_ordem",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_regras_recurso_fase_fase_cronograma",
                schema: "selecao",
                table: "regras_recurso_fase",
                column: "fase_cronograma_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bancas_requeridas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_recurso_fase",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "fases_cronograma",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "origem_candidatos",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
