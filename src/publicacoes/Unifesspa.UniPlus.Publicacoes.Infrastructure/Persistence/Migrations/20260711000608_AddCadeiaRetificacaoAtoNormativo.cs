using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCadeiaRetificacaoAtoNormativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ato_retificado_id",
                schema: "publicacoes",
                table: "ato_normativo",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_retificacao",
                schema: "publicacoes",
                table: "ato_normativo",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "unico_por_objeto",
                schema: "publicacoes",
                table: "ato_normativo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ux_ato_normativo_retificado",
                schema: "publicacoes",
                table: "ato_normativo",
                column: "ato_retificado_id",
                unique: true,
                filter: "ato_retificado_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ato_normativo_nao_autorretifica",
                schema: "publicacoes",
                table: "ato_normativo",
                sql: "ato_retificado_id IS NULL OR ato_retificado_id <> id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ato_normativo_retificacao_completa",
                schema: "publicacoes",
                table: "ato_normativo",
                sql: "(ato_retificado_id IS NULL AND motivo_retificacao IS NULL) OR (ato_retificado_id IS NOT NULL AND motivo_retificacao IS NOT NULL AND btrim(motivo_retificacao) <> '')");

            migrationBuilder.AddForeignKey(
                name: "fk_ato_normativo_ato_retificado",
                schema: "publicacoes",
                table: "ato_normativo",
                column: "ato_retificado_id",
                principalSchema: "publicacoes",
                principalTable: "ato_normativo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ato_normativo_ato_retificado",
                schema: "publicacoes",
                table: "ato_normativo");

            migrationBuilder.DropIndex(
                name: "ux_ato_normativo_retificado",
                schema: "publicacoes",
                table: "ato_normativo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ato_normativo_nao_autorretifica",
                schema: "publicacoes",
                table: "ato_normativo");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ato_normativo_retificacao_completa",
                schema: "publicacoes",
                table: "ato_normativo");

            migrationBuilder.DropColumn(
                name: "ato_retificado_id",
                schema: "publicacoes",
                table: "ato_normativo");

            migrationBuilder.DropColumn(
                name: "motivo_retificacao",
                schema: "publicacoes",
                table: "ato_normativo");

            migrationBuilder.DropColumn(
                name: "unico_por_objeto",
                schema: "publicacoes",
                table: "ato_normativo");
        }
    }
}
