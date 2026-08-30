using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaCategoriasDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // INSERT tolerante em vez do InsertData gerado pelo EF: o cadastro
            // administrativo existe desde a migration anterior, então um ambiente pode
            // já ter uma categoria viva ocupando um destes códigos. O índice único
            // parcial rejeitaria a linha do seed e a falha travaria a migração inteira
            // — e, com ela, o deploy. Pular a linha em conflito preserva o que o
            // operador cadastrou e mantém o catálogo completo em toda base que ainda
            // não o tinha.
            migrationBuilder.Sql(
                """
                INSERT INTO configuracao.categoria_documento
                       (id, codigo, nome, ordem, created_at, is_deleted)
                VALUES
                       ('ca7e0000-0000-7000-8000-000000000001', 'IDENTIFICACAO', 'Identificação', 1, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000002', 'ESCOLARIDADE', 'Escolaridade', 2, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000003', 'TITULACAO_EXPERIENCIA', 'Titulação e experiência', 3, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000004', 'RENDA', 'Renda', 4, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000005', 'RESIDENCIA', 'Residência', 5, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000006', 'RACA_ETNIA', 'Raça/etnia', 6, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000007', 'SAUDE', 'Saúde', 7, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000008', 'DOCUMENTO_PROCESSUAL', 'Documento processual', 8, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000009', 'PRODUCAO_AVALIATIVA', 'Produção avaliativa', 9, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('ca7e0000-0000-7000-8000-000000000010', 'OUTROS', 'Outros', 10, TIMESTAMPTZ '2026-01-01 00:00:00+00', false)
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "categoria_documento",
                keyColumn: "id",
                keyValue: new Guid("ca7e0000-0000-7000-8000-000000000010"));
        }
    }
}
