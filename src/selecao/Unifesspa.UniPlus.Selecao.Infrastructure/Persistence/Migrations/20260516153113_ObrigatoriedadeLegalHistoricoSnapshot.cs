using System;

using Microsoft.EntityFrameworkCore.Migrations;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Story #460 — cria <c>obrigatoriedades_legais</c> (forma plena com hash
    /// canônico + governance), <c>obrigatoriedade_legal_historico</c>
    /// (append-only IForensicEntity per ADR-0063), <c>edital_governance_snapshot</c>
    /// (schema apenas; preenchimento em #462) e a junction
    /// <c>obrigatoriedade_legal_areas_de_interesse</c> — primeira aplicação
    /// do template <see cref="AreaVisibilityConfiguration{T}"/> (ADR-0060)
    /// em Selecao.
    /// </summary>
    /// <remarks>
    /// O exclusion constraint GIST que impede sobreposição de janelas
    /// <c>(parent, area, valid_from..valid_to)</c> é emitido em SQL bruto via
    /// <see cref="JunctionTableMigrationHelper.AddAreaDeInteresseExclusionConstraint"/>,
    /// pois EF Core não modela <c>EXCLUDE USING GIST</c> no fluent API.
    /// A extensão <c>btree_gist</c> é provisionada explicitamente para que a
    /// migration funcione tanto em PROD/HML (idempotente) quanto em
    /// Postgres efêmero de testes (CI/Testcontainers).
    /// </remarks>
    public partial class ObrigatoriedadeLegalHistoricoSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extensão btree_gist é pré-requisito do EXCLUDE GIST emitido
            // ao final desta migration. Idempotente — em PROD/HML provisionada
            // por DBA ou por migration baseline anterior, não cria nada novo.
            // Em CI/test Postgres efêmero é a única garantia de que a extensão
            // exista no momento do CREATE constraint GIST sobre UUID.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "edital_governance_snapshot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regras_json = table.Column<string>(type: "jsonb", nullable: false),
                    snapshotted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_edital_governance_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedade_legal_historico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conteudo_json = table.Column<string>(type: "jsonb", nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    snapshot_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obrigatoriedade_legal_historico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedades_legais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_edital_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    categoria = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    predicado = table.Column<string>(type: "jsonb", nullable: false),
                    descricao_humana = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ato_normativo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    portaria_interna_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    proprietario = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obrigatoriedades_legais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedade_legal_areas_de_interesse",
                columns: table => new
                {
                    obrigatoriedade_legal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    added_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obrigatoriedade_legal_areas_de_interesse", x => new { x.obrigatoriedade_legal_id, x.area_codigo, x.valid_from });
                    table.ForeignKey(
                        name: "fk_obrigatoriedade_legal_areas_de_interesse_obrigatoriedades_l",
                        column: x => x.obrigatoriedade_legal_id,
                        principalTable: "obrigatoriedades_legais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_edital_governance_snapshot_edital_id",
                table: "edital_governance_snapshot",
                column: "edital_id");

            migrationBuilder.CreateIndex(
                name: "ix_obrigatoriedade_legal_areas_de_interesse_vigentes",
                table: "obrigatoriedade_legal_areas_de_interesse",
                columns: new[] { "obrigatoriedade_legal_id", "area_codigo" },
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_obrigatoriedade_legal_historico_regra_snapshot_at",
                table: "obrigatoriedade_legal_historico",
                columns: new[] { "regra_id", "snapshot_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_obrigatoriedades_legais_hash_ativos",
                table: "obrigatoriedades_legais",
                column: "hash",
                unique: true,
                filter: "is_deleted = false");

            // FK historico → regra (Codex P2): integridade referencial impede
            // orphan rows e bloqueia hard-delete acidental da regra mãe.
            // RESTRICT é coerente com a semântica append-only (ADR-0063): o
            // caminho normal é soft-delete (Modified state), que não dispara
            // o RESTRICT. Adicionado via AddForeignKey separado porque a
            // tabela historico é criada ANTES de obrigatoriedades_legais na
            // ordem alfabética emitida pelo EF Core.
            migrationBuilder.AddForeignKey(
                name: "fk_obrigatoriedade_legal_historico_regra_id",
                table: "obrigatoriedade_legal_historico",
                column: "regra_id",
                principalTable: "obrigatoriedades_legais",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Exclusion constraint GIST per ADR-0060: impede janelas de
            // validade sobrepostas para o mesmo (regra, área). Não é
            // expressável no fluent API do EF — emitido em SQL bruto via
            // helper canônico de Infrastructure.Core. Requer btree_gist.
            migrationBuilder.AddAreaDeInteresseExclusionConstraint(
                junctionTable: "obrigatoriedade_legal_areas_de_interesse",
                parentForeignKeyColumn: "obrigatoriedade_legal_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: as 3 tabelas guardam evidência
            // forense (historico append-only IForensicEntity per ADR-0063,
            // snapshot por publicação). Um `database update <baseline>`
            // reativaria silenciosamente o schema pré-460 e perderia
            // histórico — caminho proibido. Nova migration "Reverte..." é
            // o mecanismo canônico de revert.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
