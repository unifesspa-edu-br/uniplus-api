using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAprovacaoDaProtecaoDoCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_aceita_em",
                schema: "configuracao",
                table: "tipo_documento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_aceita_por",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_decidida_em",
                schema: "configuracao",
                table: "tipo_documento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_decidida_por",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_recomendada",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_situacao",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDENTE");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_solicitada_em",
                schema: "configuracao",
                table: "tipo_documento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_solicitada_por",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_aceita_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_aceita_por",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_decidida_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_decidida_por",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_recomendada",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_situacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDENTE");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_solicitada_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_solicitada_por",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000018"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000019"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000022"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000023"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000024"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000025"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000026"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000027"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000028"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000030"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000031"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000033"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000036"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000037"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000038"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000039"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000040"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000041"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000042"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000043"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000044"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000045"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000046"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000047"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000048"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000049"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000050"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000051"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000052"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000053"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000054"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000055"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000056"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000057"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000058"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000059"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000060"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000001"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000002"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000003"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000004"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000005"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000006"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000007"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000008"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000009"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000010"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000011"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000012"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000013"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000014"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000015"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000016"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000017"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000018"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000019"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000020"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000021"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000022"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000023"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000024"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000025"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000026"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000027"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000028"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000029"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000030"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000031"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000032"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000033"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000034"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000035"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000036"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000037"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000038"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000039"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000040"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000041"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000042"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000043"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000044"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000045"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000046"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000047"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000048"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000049"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000050"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000051"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000052"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000053"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000054"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000055"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000056"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000057"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000058"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000059"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000060"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000061"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000062"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000063"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000064"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000065"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000066"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000067"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000068"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000069"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000070"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000071"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000072"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000073"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000074"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000075"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000076"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000077"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000078"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000079"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000080"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000081"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000082"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000083"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000084"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000085"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000086"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000087"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000088"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000089"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000090"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000091"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000092"),
                columns: new[] { "protecao_aceita_em", "protecao_aceita_por", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[] { null, null, null, null, null, "PENDENTE", null, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_negada_com_recomendacao",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "protecao_situacao <> 'NEGADA' OR protecao_recomendada IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_situacao",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'NEGADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_negada_com_recomendacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "protecao_situacao <> 'NEGADA' OR protecao_recomendada IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_situacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'NEGADA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_negada_com_recomendacao",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_situacao",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_negada_com_recomendacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_situacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_aceita_em",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_aceita_por",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_decidida_em",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_decidida_por",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_recomendada",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_situacao",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_solicitada_em",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_solicitada_por",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_aceita_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_aceita_por",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_decidida_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_decidida_por",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_recomendada",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_situacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_solicitada_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_solicitada_por",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");
        }
    }
}
