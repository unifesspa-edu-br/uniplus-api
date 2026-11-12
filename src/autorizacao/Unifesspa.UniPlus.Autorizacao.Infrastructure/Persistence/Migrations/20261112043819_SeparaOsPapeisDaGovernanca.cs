using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparaOsPapeisDaGovernanca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_revisao_itens_decisao_completa",
                schema: "autorizacao",
                table: "revisao_itens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_migracoes_da_concessao_dispensa",
                schema: "autorizacao",
                table: "migracoes_da_concessao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_decisoes_da_migracao_dispensa",
                schema: "autorizacao",
                table: "decisoes_da_migracao");

            migrationBuilder.DropColumn(
                name: "decide",
                schema: "autorizacao",
                table: "revisao_itens");

            migrationBuilder.DropColumn(
                name: "dispensa",
                schema: "autorizacao",
                table: "migracoes_da_concessao");

            migrationBuilder.DropColumn(
                name: "dispensa",
                schema: "autorizacao",
                table: "decisoes_da_migracao");

            migrationBuilder.AddColumn<string>(
                name: "decidido_como",
                schema: "autorizacao",
                table: "revisao_itens",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:termos:publicar", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "configuracao:protecao-do-catalogo:aprovar", new Guid("019a0000-0000-7000-8000-000000000002") }
                });

            migrationBuilder.UpdateData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000001"),
                column: "descricao",
                value: "Opera as concessões de acesso, mantém os perfis de acesso e publica o termo de responsabilidade.");

            migrationBuilder.UpdateData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000002"),
                columns: new[] { "descricao", "governanca", "nome" },
                values: new object[] { "Decide, em nome da Unifesspa, a segunda etapa dos pedidos com dado pessoal, as versões protegidas de perfil, a janela de migração e a proteção do catálogo, e conduz a revisão periódica dos acessos.", "representante-do-controlador", "Representante do controlador" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_revisao_itens_decisao_completa",
                schema: "autorizacao",
                table: "revisao_itens",
                sql: "(resultado IS NULL AND decidido_como IS NULL AND decidido_por_emissor IS NULL AND decidido_por_sub IS NULL AND decidido_em IS NULL) OR (resultado IS NOT NULL AND decidido_como IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_sub IS NOT NULL AND decidido_em IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_revisao_itens_decisao_completa",
                schema: "autorizacao",
                table: "revisao_itens");

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:termos:publicar", new Guid("019a0000-0000-7000-8000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "configuracao:protecao-do-catalogo:aprovar", new Guid("019a0000-0000-7000-8000-000000000002") });

            migrationBuilder.DropColumn(
                name: "decidido_como",
                schema: "autorizacao",
                table: "revisao_itens");

            migrationBuilder.AddColumn<string>(
                name: "decide",
                schema: "autorizacao",
                table: "revisao_itens",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "dispensa",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dispensa",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000001"),
                column: "descricao",
                value: "Opera as concessões de acesso e mantém os perfis de acesso.");

            migrationBuilder.UpdateData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000002"),
                columns: new[] { "descricao", "governanca", "nome" },
                values: new object[] { "Aprova a concessão de acesso a dado pessoal ou sensível.", "protecao-de-dados", "Encarregado(a) pelo tratamento de dados" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_revisao_itens_decisao_completa",
                schema: "autorizacao",
                table: "revisao_itens",
                sql: "(resultado IS NULL AND decidido_por_emissor IS NULL AND decidido_por_sub IS NULL AND decidido_em IS NULL) OR (resultado IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_sub IS NOT NULL AND decidido_em IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_migracoes_da_concessao_dispensa",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                sql: "dispensa IS NULL OR dispensa = 'beneficiario_ja_tem_protecao_de_dados'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_decisoes_da_migracao_dispensa",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                sql: "dispensa IS NULL OR dispensa = 'beneficiario_ja_tem_protecao_de_dados'");
        }
    }
}
