using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaONomeEARegraDeDeliberacaoDasBancas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nome",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_criterio",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_desempate",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_encerramento",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_integrantes",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_modo",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_quorum",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_sem_unanimidade",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_suplentes",
                schema: "selecao",
                table: "bancas_requeridas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_criterio",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_desempate",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_encerramento",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_integrantes",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_modo",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_quorum",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_sem_unanimidade",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "regra_deliberacao_suplentes",
                schema: "selecao",
                table: "bancas_da_etapa",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nome",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_criterio",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_desempate",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_encerramento",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_integrantes",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_modo",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_quorum",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_sem_unanimidade",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_suplentes",
                schema: "selecao",
                table: "bancas_requeridas");

            migrationBuilder.DropColumn(
                name: "nome",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_criterio",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_desempate",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_encerramento",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_integrantes",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_modo",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_quorum",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_sem_unanimidade",
                schema: "selecao",
                table: "bancas_da_etapa");

            migrationBuilder.DropColumn(
                name: "regra_deliberacao_suplentes",
                schema: "selecao",
                table: "bancas_da_etapa");
        }
    }
}
