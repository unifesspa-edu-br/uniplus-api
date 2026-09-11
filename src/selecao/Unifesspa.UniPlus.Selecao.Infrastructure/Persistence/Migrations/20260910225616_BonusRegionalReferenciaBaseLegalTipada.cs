using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BonusRegionalReferenciaBaseLegalTipada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "base_legal",
                schema: "selecao",
                table: "configuracoes_bonus_regional");

            migrationBuilder.DropColumn(
                name: "municipio_convenio",
                schema: "selecao",
                table: "configuracoes_bonus_regional");

            migrationBuilder.AddColumn<Guid>(
                name: "base_legal_bonus_regional_id",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "identificacao",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tipo_instrumento",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "configuracoes_bonus_regional_municipio",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_bonus_regional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_bonus_regional_municipio", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_bonus_regional_municipio_configuracoes_bonus_",
                        column: x => x.configuracao_bonus_regional_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_bonus_regional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_bonus_regional_municipio_configuracao_bonus_r",
                schema: "selecao",
                table: "configuracoes_bonus_regional_municipio",
                column: "configuracao_bonus_regional_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes_bonus_regional_municipio",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "base_legal_bonus_regional_id",
                schema: "selecao",
                table: "configuracoes_bonus_regional");

            migrationBuilder.DropColumn(
                name: "descricao",
                schema: "selecao",
                table: "configuracoes_bonus_regional");

            migrationBuilder.DropColumn(
                name: "identificacao",
                schema: "selecao",
                table: "configuracoes_bonus_regional");

            migrationBuilder.DropColumn(
                name: "tipo_instrumento",
                schema: "selecao",
                table: "configuracoes_bonus_regional");

            migrationBuilder.AddColumn<string>(
                name: "base_legal",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "municipio_convenio",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
