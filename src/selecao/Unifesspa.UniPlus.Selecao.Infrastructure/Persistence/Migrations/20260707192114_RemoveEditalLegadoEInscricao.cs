using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEditalLegadoEInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "inscricoes",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "editais",
                schema: "selecao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidatos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    nome_social = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nome_civil = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
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
                    bonus_regional_habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    maximo_opcoes_curso = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<int>(type: "integer", nullable: false),
                    tipo_edital_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bonus_regional_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    fator_divisao = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ano_edital = table.Column<int>(type: "integer", nullable: false),
                    numero_edital = table.Column<int>(type: "integer", nullable: false),
                    periodo_inscricao_fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    periodo_inscricao_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inscricoes",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_curso_primeira_opcao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    codigo_curso_segunda_opcao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    lista_espera = table.Column<bool>(type: "boolean", nullable: false),
                    modalidade = table.Column<int>(type: "integer", nullable: false),
                    numero_inscricao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscricoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cotas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    modalidade = table.Column<int>(type: "integer", nullable: false),
                    percentual_vagas = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eliminatoria = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nota_minima = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    peso = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tipo_etapa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
        }
    }
}
