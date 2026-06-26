using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "selecao");

            migrationBuilder.CreateTable(
                name: "candidatos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    nome_civil = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    nome_social = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidatos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "editais",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_edital = table.Column<int>(type: "integer", nullable: false),
                    ano_edital = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo_edital_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    periodo_inscricao_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    periodo_inscricao_fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fator_divisao = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    bonus_regional_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    maximo_opcoes_curso = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    bonus_regional_habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_headers_json = table.Column<string>(type: "jsonb", nullable: true),
                    response_body_cipher = table.Column<byte[]>(type: "bytea", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inscricoes",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    codigo_curso_primeira_opcao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    codigo_curso_segunda_opcao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lista_espera = table.Column<bool>(type: "boolean", nullable: false),
                    numero_inscricao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscricoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedades_legais",
                schema: "selecao",
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
                name: "processos_seletivos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_curso = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome_curso = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    campus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    total_vagas = table.Column<int>(type: "integer", nullable: false),
                    turno = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processos_seletivos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cotas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade = table.Column<int>(type: "integer", nullable: false),
                    percentual_vagas = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotas", x => x.id);
                    table.ForeignKey(
                        name: "fk_cotas_editais_edital_id",
                        column: x => x.edital_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "edital_governance_snapshot",
                schema: "selecao",
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
                    table.ForeignKey(
                        name: "fk_edital_governance_snapshot_edital_id",
                        column: x => x.edital_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "etapas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_etapa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    peso = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    nota_minima = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    eliminatoria = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas", x => x.id);
                    table.ForeignKey(
                        name: "fk_etapas_editais_edital_id",
                        column: x => x.edital_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedade_legal_historico",
                schema: "selecao",
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
                    table.ForeignKey(
                        name: "fk_obrigatoriedade_legal_historico_regra_id",
                        column: x => x.regra_id,
                        principalSchema: "selecao",
                        principalTable: "obrigatoriedades_legais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidatos_cpf",
                schema: "selecao",
                table: "candidatos",
                column: "cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cotas_edital_id",
                schema: "selecao",
                table: "cotas",
                column: "edital_id");

            migrationBuilder.CreateIndex(
                name: "ix_edital_governance_snapshot_edital_id",
                schema: "selecao",
                table: "edital_governance_snapshot",
                column: "edital_id");

            migrationBuilder.CreateIndex(
                name: "ix_etapas_edital_id",
                schema: "selecao",
                table: "etapas",
                column: "edital_id");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                schema: "selecao",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                schema: "selecao",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_candidato_id_edital_id",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "candidato_id", "edital_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_numero_inscricao",
                schema: "selecao",
                table: "inscricoes",
                column: "numero_inscricao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_obrigatoriedade_legal_historico_regra_snapshot_at",
                schema: "selecao",
                table: "obrigatoriedade_legal_historico",
                columns: new[] { "regra_id", "snapshot_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_obrigatoriedades_legais_hash_ativos",
                schema: "selecao",
                table: "obrigatoriedades_legais",
                column: "hash",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_obrigatoriedades_legais_regra_codigo_ativos",
                schema: "selecao",
                table: "obrigatoriedades_legais",
                column: "regra_codigo",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidatos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "cotas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "edital_governance_snapshot",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "etapas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "idempotency_cache",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "inscricoes",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "obrigatoriedade_legal_historico",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "processos_seletivos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "editais",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "obrigatoriedades_legais",
                schema: "selecao");
        }
    }
}
