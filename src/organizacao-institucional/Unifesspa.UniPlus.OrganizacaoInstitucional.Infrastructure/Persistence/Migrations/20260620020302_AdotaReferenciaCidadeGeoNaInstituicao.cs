using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdotaReferenciaCidadeGeoNaInstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "municipio_sede",
                table: "instituicao");

            migrationBuilder.AddColumn<string>(
                name: "cidade_codigo_ibge",
                table: "instituicao",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cidade_display_atualizado_em",
                table: "instituicao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade_nome",
                table: "instituicao",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade_origem",
                table: "instituicao",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade_uf",
                table: "instituicao",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_cidade_codigo_ibge",
                table: "instituicao",
                column: "cidade_codigo_ibge");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_instituicao_cidade_codigo_ibge",
                table: "instituicao");

            migrationBuilder.DropColumn(
                name: "cidade_codigo_ibge",
                table: "instituicao");

            migrationBuilder.DropColumn(
                name: "cidade_display_atualizado_em",
                table: "instituicao");

            migrationBuilder.DropColumn(
                name: "cidade_nome",
                table: "instituicao");

            migrationBuilder.DropColumn(
                name: "cidade_origem",
                table: "instituicao");

            migrationBuilder.DropColumn(
                name: "cidade_uf",
                table: "instituicao");

            migrationBuilder.AddColumn<string>(
                name: "municipio_sede",
                table: "instituicao",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
