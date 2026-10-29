using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaNomeDeQuemAgeNaAutorizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "solicitante_nome",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decisor_nome",
                schema: "autorizacao",
                table: "pedido_decisoes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "leitor_nome",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "executor_nome",
                schema: "autorizacao",
                table: "instalacoes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "autorizacao",
                table: "instalacao_gestores",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nome",
                schema: "autorizacao",
                table: "instalacao_gestores",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "beneficiario_email",
                schema: "autorizacao",
                table: "concessoes",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "beneficiario_nome",
                schema: "autorizacao",
                table: "concessoes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "concedida_por_nome",
                schema: "autorizacao",
                table: "concessoes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revogada_por_nome",
                schema: "autorizacao",
                table: "concessoes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "solicitante_nome",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropColumn(
                name: "decisor_nome",
                schema: "autorizacao",
                table: "pedido_decisoes");

            migrationBuilder.DropColumn(
                name: "leitor_nome",
                schema: "autorizacao",
                table: "pedido_anexo_leituras");

            migrationBuilder.DropColumn(
                name: "executor_nome",
                schema: "autorizacao",
                table: "instalacoes");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "autorizacao",
                table: "instalacao_gestores");

            migrationBuilder.DropColumn(
                name: "nome",
                schema: "autorizacao",
                table: "instalacao_gestores");

            migrationBuilder.DropColumn(
                name: "beneficiario_email",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropColumn(
                name: "beneficiario_nome",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropColumn(
                name: "concedida_por_nome",
                schema: "autorizacao",
                table: "concessoes");

            migrationBuilder.DropColumn(
                name: "revogada_por_nome",
                schema: "autorizacao",
                table: "concessoes");
        }
    }
}
