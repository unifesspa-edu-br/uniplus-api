using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoMotivosDecisaoIsencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "motivos_decisao_isencao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fundamento = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resultado_permitido = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_decisao_isencao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_motivos_decisao_isencao_fundamento_ativo",
                schema: "selecao",
                table: "motivos_decisao_isencao",
                columns: new[] { "fundamento", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ux_motivos_decisao_isencao_codigo",
                schema: "selecao",
                table: "motivos_decisao_isencao",
                column: "codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "motivos_decisao_isencao",
                schema: "selecao");
        }
    }
}
