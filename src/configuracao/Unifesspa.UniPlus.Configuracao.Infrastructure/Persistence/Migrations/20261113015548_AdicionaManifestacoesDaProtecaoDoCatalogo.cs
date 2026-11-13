using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaManifestacoesDaProtecaoDoCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "protecao_ciclo",
                schema: "configuracao",
                table: "tipo_documento",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "protecao_manifestacao_desfavoravel",
                schema: "configuracao",
                table: "tipo_documento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_manifestada_em",
                schema: "configuracao",
                table: "tipo_documento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "protecao_ciclo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "protecao_manifestacao_desfavoravel",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "protecao_manifestada_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "diante_de_manifestacao_desfavoravel",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "protecao_do_catalogo_manifestacoes",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo = table.Column<int>(type: "integer", nullable: false),
                    parecer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    substitui_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    autor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    cadastro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protecao_do_catalogo_manifestacoes", x => x.id);
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_cadastro", "cadastro IN ('TIPO_DOCUMENTO', 'FATO')");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_ciclo_positivo", "ciclo >= 1");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_parecer", "parecer IN ('ADEQUADA', 'RECOMENDA_AJUSTE', 'INADEQUADA')");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_substitui_outra", "substitui_id IS NULL OR substitui_id <> id");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_texto_no_desfavoravel", "parecer = 'ADEQUADA' OR texto IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_protecao_do_catalogo_manifestacoes_substitui",
                        column: x => x.substitui_id,
                        principalSchema: "configuracao",
                        principalTable: "protecao_do_catalogo_manifestacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000018"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000019"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000022"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000023"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000024"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000025"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000026"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000027"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000028"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000030"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000031"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000033"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000036"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000037"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000038"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000039"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000040"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000041"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000042"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000043"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000044"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000045"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000046"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000047"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000048"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000049"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000050"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000051"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000052"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000053"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000054"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000055"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000056"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000057"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000058"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000059"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000060"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000001"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000002"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000003"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000004"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000005"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000006"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000007"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000008"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000009"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000010"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000011"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000012"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000013"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000014"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000015"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000016"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000017"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000018"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000019"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000020"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000021"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000022"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000023"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000024"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000025"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000026"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000027"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000028"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000029"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000030"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000031"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000032"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000033"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000034"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000035"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000036"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000037"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000038"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000039"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000040"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000041"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000042"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000043"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000044"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000045"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000046"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000047"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000048"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000049"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000050"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000051"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000052"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000053"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000054"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000055"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000056"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000057"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000058"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000059"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000060"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000061"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000062"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000063"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000064"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000065"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000066"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000067"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000068"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000069"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000070"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000071"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000072"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000073"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000074"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000075"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000076"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000077"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000078"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000079"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000080"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000081"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000082"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000083"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000084"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000085"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000086"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000087"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000088"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000089"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000090"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000091"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000092"),
                columns: new[] { "protecao_ciclo", "protecao_manifestacao_desfavoravel", "protecao_manifestada_em" },
                values: new object[] { 1, false, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_ciclo_positivo",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "protecao_ciclo >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_desfavoravel_manifestada",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_ciclo_positivo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "protecao_ciclo >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_desfavoravel_manifestada",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_atos_item_ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                columns: new[] { "cadastro", "item_id", "ciclo", "instante" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_protecao_do_catalogo_atos_ciclo_positivo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                sql: "ciclo >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_protecao_do_catalogo_atos_diante_so_na_decisao",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                sql: "ato IN ('APROVACAO', 'AJUSTE_RECOMENDADO', 'REPROVACAO') OR NOT diante_de_manifestacao_desfavoravel");

            migrationBuilder.AddCheckConstraint(
                name: "ck_protecao_do_catalogo_atos_justificativa_diante_desfavoravel",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                sql: "ato NOT IN ('APROVACAO', 'AJUSTE_RECOMENDADO') OR NOT diante_de_manifestacao_desfavoravel OR justificativa IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_manifestacoes_item_ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                columns: new[] { "cadastro", "item_id", "ciclo", "instante" });

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_inicial_do_autor",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                columns: new[] { "cadastro", "item_id", "ciclo", "autor_emissor", "autor_sub" },
                unique: true,
                filter: "substitui_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_substitui",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                column: "substitui_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "protecao_do_catalogo_manifestacoes",
                schema: "configuracao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_ciclo_positivo",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_desfavoravel_manifestada",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_ciclo_positivo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_desfavoravel_manifestada",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropIndex(
                name: "ix_protecao_do_catalogo_atos_item_ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_protecao_do_catalogo_atos_ciclo_positivo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_protecao_do_catalogo_atos_diante_so_na_decisao",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_protecao_do_catalogo_atos_justificativa_diante_desfavoravel",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");

            migrationBuilder.DropColumn(
                name: "protecao_ciclo",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_manifestacao_desfavoravel",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_manifestada_em",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_ciclo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_manifestacao_desfavoravel",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "protecao_manifestada_em",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropColumn(
                name: "ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");

            migrationBuilder.DropColumn(
                name: "diante_de_manifestacao_desfavoravel",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos");
        }
    }
}
