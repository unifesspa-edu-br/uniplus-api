using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentoExigidoBaseLegal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_exigidos_base_legal",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_exigidos_base_legal", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_base_legal_documento_exigido_documento_",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_base_legal_documento_exigido_id",
                schema: "selecao",
                table: "documentos_exigidos_base_legal",
                column: "documento_exigido_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos_exigidos_base_legal",
                schema: "selecao");
        }
    }
}
