using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraAUnidadeResponsavelPorFase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unidade_responsavel_nome",
                schema: "selecao",
                table: "fases_cronograma",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unidade_responsavel_origem_id",
                schema: "selecao",
                table: "fases_cronograma",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unidade_responsavel_sigla",
                schema: "selecao",
                table: "fases_cronograma",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unidade_responsavel_nome",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.DropColumn(
                name: "unidade_responsavel_origem_id",
                schema: "selecao",
                table: "fases_cronograma");

            migrationBuilder.DropColumn(
                name: "unidade_responsavel_sigla",
                schema: "selecao",
                table: "fases_cronograma");
        }
    }
}
