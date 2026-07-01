using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaseCanonicaETipoBanca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fase_canonica",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dono_tipico = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    agrupa_etapas = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    permite_complementacao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fase_canonica", x => x.id);
                    table.CheckConstraint("ck_fase_canonica_agrupa_etapas", "agrupa_etapas = false OR codigo = 'AVALIACAO'");
                    table.CheckConstraint("ck_fase_canonica_codigo_canonico", "codigo IN ('INSCRICAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_fase_canonica_codigo_formato", "codigo ~ '^[A-Z_]+$'");
                    table.CheckConstraint("ck_fase_canonica_complementacao", "permite_complementacao = false OR codigo IN ('HOMOLOGACAO', 'RECURSOS')");
                    table.CheckConstraint("ck_fase_canonica_dono_tipico", "dono_tipico IN ('CEPS', 'CRCA', 'MEC', 'CONSEPE')");
                });

            migrationBuilder.CreateTable(
                name: "tipo_banca",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fase_tipica = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_banca", x => x.id);
                    table.CheckConstraint("ck_tipo_banca_codigo_canonico", "codigo IN ('BANCA_ANALISE_DOCUMENTAL', 'BANCA_ENTREVISTA', 'BANCA_CORRECAO_REDACOES', 'BANCA_ANALISE_RECURSOS')");
                    table.CheckConstraint("ck_tipo_banca_codigo_formato", "codigo ~ '^[A-Z_]+$'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_fase_canonica_codigo_vivo",
                schema: "configuracao",
                table: "fase_canonica",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_banca_codigo_vivo",
                schema: "configuracao",
                table: "tipo_banca",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fase_canonica",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipo_banca",
                schema: "configuracao");
        }
    }
}
