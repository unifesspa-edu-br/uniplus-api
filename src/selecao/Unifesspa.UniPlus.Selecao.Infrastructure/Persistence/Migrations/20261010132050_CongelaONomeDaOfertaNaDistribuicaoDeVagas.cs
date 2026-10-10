using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaONomeDaOfertaNaDistribuicaoDeVagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "oferta_curso_grau",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                comment: "Grau do curso da oferta, congelado por valor do cadastro na definição da distribuição.");

            migrationBuilder.AddColumn<string>(
                name: "oferta_curso_nome",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "Nome do curso da oferta, congelado por valor do cadastro na definição da distribuição.");

            migrationBuilder.AddColumn<string[]>(
                name: "oferta_turnos",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "text[]",
                nullable: true,
                comment: "Turnos que a oferta ocupa (tokens), congelados por valor do cadastro na definição da distribuição.");

            migrationBuilder.AddColumn<string>(
                name: "oferta_unidade_ofertante_nome",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                comment: "Nome da unidade ofertante, congelado por valor do cadastro na definição da distribuição.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "oferta_curso_grau",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "oferta_curso_nome",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "oferta_turnos",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "oferta_unidade_ofertante_nome",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");
        }
    }
}
