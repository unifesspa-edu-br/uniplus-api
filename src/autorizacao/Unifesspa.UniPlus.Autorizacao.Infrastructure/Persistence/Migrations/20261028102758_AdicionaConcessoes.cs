using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConcessoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "concessoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    beneficiario_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    beneficiario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escopo_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origem_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concedida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concedida_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    concedida_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    valida_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revogada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_revogacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concessoes", x => x.id);
                    table.CheckConstraint("ck_concessoes_escopo_unidade", "(escopo_tipo = 'unidade') = (unidade_id IS NOT NULL)");
                    table.CheckConstraint("ck_concessoes_revogacao_completa", "(revogada_em IS NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NOT NULL AND revogada_por_sub IS NOT NULL AND motivo_revogacao IS NOT NULL)");
                    table.CheckConstraint("ck_concessoes_validade_posterior", "valida_ate IS NULL OR valida_ate > concedida_em");
                    table.ForeignKey(
                        name: "fk_concessoes_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:concessoes:administrar", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:concessoes:administrar", new Guid("019a0000-0000-7000-8000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_concessoes_perfil_ativas",
                schema: "autorizacao",
                table: "concessoes",
                column: "perfil_id",
                filter: "revogada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_concessoes_ativa_unica",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "beneficiario_emissor", "beneficiario_sub", "perfil_id", "escopo_tipo", "unidade_id" },
                unique: true,
                filter: "revogada_em IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_concessoes_origem",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "origem_tipo", "origem_id", "beneficiario_emissor", "beneficiario_sub" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "concessoes",
                schema: "autorizacao");

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:concessoes:administrar", new Guid("019a0000-0000-7000-8000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:concessoes:administrar", new Guid("019a0000-0000-7000-8000-000000000002") });
        }
    }
}
