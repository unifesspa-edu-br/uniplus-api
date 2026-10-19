using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaASegundaOpcaoDeCursoNaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "oferta_curso_segunda_opcao_id",
                schema: "selecao",
                table: "inscricoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modalidades_segunda_opcao",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "jsonb",
                nullable: true,
                comment: "As modalidades em que o candidato concorre na 2ª opção, sobre a distribuição de vagas da oferta dela; nulo quando não há 2ª opção ou a versão não deriva a modalidade.");

            migrationBuilder.AddColumn<Guid>(
                name: "oferta_curso_segunda_opcao_id",
                schema: "selecao",
                table: "comprovantes_inscricao",
                type: "uuid",
                nullable: true,
                comment: "A oferta de curso da 2ª opção; nulo quando a inscrição não a declarou nesta submissão.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "oferta_curso_segunda_opcao_id",
                schema: "selecao",
                table: "inscricoes");

            migrationBuilder.DropColumn(
                name: "modalidades_segunda_opcao",
                schema: "selecao",
                table: "comprovantes_inscricao");

            migrationBuilder.DropColumn(
                name: "oferta_curso_segunda_opcao_id",
                schema: "selecao",
                table: "comprovantes_inscricao");
        }
    }
}
