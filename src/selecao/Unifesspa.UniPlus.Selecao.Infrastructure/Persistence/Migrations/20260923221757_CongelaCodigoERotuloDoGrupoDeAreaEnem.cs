using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CongelaCodigoERotuloDoGrupoDeAreaEnem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A coluna antiga guardava o rótulo acentuado do grupo; o congelamento passa a
            // ter código e rótulo. Não há produção: a coluna é descartada, e as
            // distribuições já gravadas ficam sem grupo até serem redefinidas.
            migrationBuilder.DropColumn(
                name: "grupo_area_enem",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.AddColumn<string>(
                name: "grupo_area_enem_codigo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                comment: "Código do grupo de área do ENEM do curso da oferta, sem abreviação e sem acento, congelado por valor do cadastro de cursos na definição da distribuição; casa a oferta com a linha de Pesos por Área. Nulo quando o curso não declara grupo.");

            migrationBuilder.AddColumn<string>(
                name: "grupo_area_enem_rotulo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                comment: "Rótulo do grupo de área do ENEM do curso da oferta, congelado por valor junto do código na definição da distribuição. Nulo quando o curso não declara grupo.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grupo_area_enem_rotulo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.DropColumn(
                name: "grupo_area_enem_codigo",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas");

            migrationBuilder.AddColumn<string>(
                name: "grupo_area_enem",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
