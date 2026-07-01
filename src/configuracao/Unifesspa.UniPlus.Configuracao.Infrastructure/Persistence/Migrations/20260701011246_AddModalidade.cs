using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModalidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modalidade",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    natureza_legal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_vagas = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_origem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    regra_remanejamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    remanejamento_args = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    criterios_cumulativos = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    acao_quando_indeferido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("pk_modalidade", x => x.id);
                    table.CheckConstraint("ck_modalidade_acao_quando_indeferido", "acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ('RECLASSIFICAR_AC', 'RECLASSIFICAR_REGRA_EDITAL')");
                    table.CheckConstraint("ck_modalidade_codigo_formato", "codigo ~ '^[A-Z0-9_]+$'");
                    table.CheckConstraint("ck_modalidade_composicao_vagas", "composicao_vagas IN ('RESIDUAL_DO_VO', 'DENTRO_DO_VR', 'RETIRA_DE', 'SUPLEMENTAR_AO_TOTAL')");
                    table.CheckConstraint("ck_modalidade_natureza_legal", "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'SUPLEMENTAR', 'OUTRA_MODALIDADE')");
                    table.CheckConstraint("ck_modalidade_regra_remanejamento", "regra_remanejamento IS NULL OR regra_remanejamento IN ('SEGUE_CASCATA', 'DESTINO_UNICO', 'CRUZADO')");
                    table.CheckConstraint("ck_modalidade_retira_de_origem", "(composicao_vagas = 'RETIRA_DE') = (composicao_origem IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_modalidade_codigo_vivo",
                schema: "configuracao",
                table: "modalidade",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modalidade",
                schema: "configuracao");
        }
    }
}
