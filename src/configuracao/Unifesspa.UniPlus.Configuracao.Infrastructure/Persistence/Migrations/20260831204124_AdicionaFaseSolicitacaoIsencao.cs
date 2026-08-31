using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFaseSolicitacaoIsencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "antecessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "sucessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "antecessora_codigo IN ('INSCRICAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "sucessora_codigo IN ('INSCRICAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "codigo IN ('INSCRICAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
        }
    }
}
