using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOPerfilDoEncarregadoDeDados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfis",
                columns: new[] { "id", "ativo", "created_at", "descricao", "governanca", "nome", "updated_at" },
                values: new object[] { new Guid("019a0000-0000-7000-8000-000000000003"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Acompanha o tratamento de dados pessoais na gestão de acessos: consulta pedidos, concessões, revisões, versões de perfil, janela de migração e catálogo, registra manifestação técnica na proteção do catálogo e solicita providências sobre concessões, sem aprovar, recusar nem revogar.", "encarregado-de-dados", "Encarregado de dados", null });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:concessoes:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:pedidos:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:perfis:consultar-versoes", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:providencias:solicitar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:revisoes:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "configuracao:protecao-do-catalogo:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "configuracao:protecao-do-catalogo:manifestar", new Guid("019a0000-0000-7000-8000-000000000003") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:concessoes:consultar", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:pedidos:consultar", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:perfis:consultar-versoes", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:providencias:solicitar", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "autorizacao:revisoes:consultar", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "configuracao:protecao-do-catalogo:consultar", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                keyColumns: new[] { "codigo", "perfil_id" },
                keyValues: new object[] { "configuracao:protecao-do-catalogo:manifestar", new Guid("019a0000-0000-7000-8000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "autorizacao",
                table: "perfis",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000003"));
        }
    }
}
