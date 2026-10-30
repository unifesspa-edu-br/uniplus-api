using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenomeiaPapelDeProtecaoDeDadosPelaFuncao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.Sql("UPDATE autorizacao.pedidos_de_concessao SET situacao = 'aguardando_protecao_de_dados' WHERE situacao = 'aguardando_encarregada';");

            migrationBuilder.Sql("UPDATE autorizacao.pedido_decisoes SET etapa = 'protecao_de_dados' WHERE etapa = 'encarregada';");

            migrationBuilder.Sql("UPDATE autorizacao.pedido_decisoes SET dispensa = 'designacao_de_protecao_de_dados' WHERE dispensa = 'designacao_da_encarregada';");

            migrationBuilder.Sql("UPDATE autorizacao.pedido_decisoes SET dispensa = 'beneficiario_ja_tem_protecao_de_dados' WHERE dispensa = 'beneficiaria_ja_e_encarregada';");

            migrationBuilder.UpdateData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000002"),
                columns: new[] { "governanca", "nome" },
                values: new object[] { "protecao-de-dados", "Encarregado(a) pelo tratamento de dados" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'aguardando_gestor', 'aguardando_protecao_de_dados')")
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao");

            migrationBuilder.Sql("UPDATE autorizacao.pedidos_de_concessao SET situacao = 'aguardando_encarregada' WHERE situacao = 'aguardando_protecao_de_dados';");

            migrationBuilder.Sql("UPDATE autorizacao.pedido_decisoes SET etapa = 'encarregada' WHERE etapa = 'protecao_de_dados';");

            migrationBuilder.Sql("UPDATE autorizacao.pedido_decisoes SET dispensa = 'designacao_da_encarregada' WHERE dispensa = 'designacao_de_protecao_de_dados';");

            migrationBuilder.Sql("UPDATE autorizacao.pedido_decisoes SET dispensa = 'beneficiaria_ja_e_encarregada' WHERE dispensa = 'beneficiario_ja_tem_protecao_de_dados';");

            migrationBuilder.UpdateData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000002"),
                columns: new[] { "governanca", "nome" },
                values: new object[] { "encarregada-de-dados", "Encarregada de dados" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'aguardando_gestor', 'aguardando_encarregada')")
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
