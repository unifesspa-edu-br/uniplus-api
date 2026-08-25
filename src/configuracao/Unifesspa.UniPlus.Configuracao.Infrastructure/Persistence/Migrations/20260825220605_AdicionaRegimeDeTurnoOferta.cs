using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaRegimeDeTurnoOferta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_turno",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropColumn(
                name: "turno",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.AddColumn<string>(
                name: "regime_de_turno",
                schema: "configuracao",
                table: "oferta_curso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false);

            migrationBuilder.AddColumn<string[]>(
                name: "turnos",
                schema: "configuracao",
                table: "oferta_curso",
                type: "character varying(30)[]",
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_regime_de_turno",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "regime_de_turno IN ('REGULAR', 'INTEGRAL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_turnos_dominio",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "turnos::text[] <@ ARRAY['MATUTINO', 'VESPERTINO', 'NOTURNO']::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_turnos_regime",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "coalesce(array_ndims(turnos), 0) = 1 AND coalesce(array_lower(turnos, 1), 1) = 1 AND ((regime_de_turno = 'REGULAR' AND cardinality(turnos) = 1) OR (regime_de_turno = 'INTEGRAL' AND cardinality(turnos) = 2 AND turnos[1] <> turnos[2]))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_regime_de_turno",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_turnos_dominio",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_turnos_regime",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropColumn(
                name: "regime_de_turno",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropColumn(
                name: "turnos",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.AddColumn<string>(
                name: "turno",
                schema: "configuracao",
                table: "oferta_curso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_turno",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "turno IS NULL OR turno IN ('MATUTINO', 'VESPERTINO', 'NOTURNO', 'INTEGRAL')");
        }
    }
}
