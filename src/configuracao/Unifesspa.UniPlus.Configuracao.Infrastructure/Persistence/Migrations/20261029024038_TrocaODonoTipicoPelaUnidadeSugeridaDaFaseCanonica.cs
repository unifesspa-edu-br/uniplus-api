using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrocaODonoTipicoPelaUnidadeSugeridaDaFaseCanonica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_dono_tipico",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "dono_tipico",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.AddColumn<string>(
                name: "unidade_sugerida_nome",
                schema: "configuracao",
                table: "fase_canonica",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unidade_sugerida_origem_id",
                schema: "configuracao",
                table: "fase_canonica",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unidade_sugerida_sigla",
                schema: "configuracao",
                table: "fase_canonica",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unidade_sugerida_nome",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "unidade_sugerida_origem_id",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.DropColumn(
                name: "unidade_sugerida_sigla",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.AddColumn<string>(
                name: "dono_tipico",
                schema: "configuracao",
                table: "fase_canonica",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_dono_tipico",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "dono_tipico IN ('CEPS', 'CRCA', 'MEC', 'CONSEPE')");
        }
    }
}
