using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaModelosFormulario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modelos_formulario",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos_formulario", x => x.id);
                    table.CheckConstraint("ck_modelos_formulario_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_modelos_formulario_codigo",
                schema: "configuracao",
                table: "modelos_formulario",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modelos_formulario_tipo_finalidade_ativo",
                schema: "configuracao",
                table: "modelos_formulario",
                columns: new[] { "tipo_processo_codigo", "finalidade", "ativo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modelos_formulario",
                schema: "configuracao");
        }
    }
}
