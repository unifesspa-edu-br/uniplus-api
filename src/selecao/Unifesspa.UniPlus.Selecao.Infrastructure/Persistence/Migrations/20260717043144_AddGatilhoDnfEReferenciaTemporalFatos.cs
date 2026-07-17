using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGatilhoDnfEReferenciaTemporalFatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "referencia_temporal_fatos_data",
                schema: "selecao",
                table: "processos_seletivos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "referencia_temporal_fatos_fase_id",
                schema: "selecao",
                table: "processos_seletivos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referencia_temporal_fatos_tipo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "condicoes_gatilho",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula = table.Column<int>(type: "integer", nullable: false),
                    fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_gatilho", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_gatilho_documento_exigido_documento_exigido_id",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_gatilho_documento_exigido_id",
                schema: "selecao",
                table: "condicoes_gatilho",
                column: "documento_exigido_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condicoes_gatilho",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "referencia_temporal_fatos_data",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "referencia_temporal_fatos_fase_id",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "referencia_temporal_fatos_tipo",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
