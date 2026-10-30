using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSituacaoAguardandoDocumentoAoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'aguardando_gestor', 'aguardando_encarregada')")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'cancelado') OR beneficiario_sub IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('aguardando_beneficiario', 'aguardando_gestor', 'aguardando_encarregada')")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_decisao_com_beneficiario",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                sql: "situacao IN ('aguardando_beneficiario', 'cancelado') OR beneficiario_sub IS NOT NULL");
        }
    }
}
