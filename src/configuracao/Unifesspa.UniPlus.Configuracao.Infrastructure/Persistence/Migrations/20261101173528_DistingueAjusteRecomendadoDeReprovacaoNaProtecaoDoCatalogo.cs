using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DistingueAjusteRecomendadoDeReprovacaoNaProtecaoDoCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "protecao_justificativa",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protecao_justificativa",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000018"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000019"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000022"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000023"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000024"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000025"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000026"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000027"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000028"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000030"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000031"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000033"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000036"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000037"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000038"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000039"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000040"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000041"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000042"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000043"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000044"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000045"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000046"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000047"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000048"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000049"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000050"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000051"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000052"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000053"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000054"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000055"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000056"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000057"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000058"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000059"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000060"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000001"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000002"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000003"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000004"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000005"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000006"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000007"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000008"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000009"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000010"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000011"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000012"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000013"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000014"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000015"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000016"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000017"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000018"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000019"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000020"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000021"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000022"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000023"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000024"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000025"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000026"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000027"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000028"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000029"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000030"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000031"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000032"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000033"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000034"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000035"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000036"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000037"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000038"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000039"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000040"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000041"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000042"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000043"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000044"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000045"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000046"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000047"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000048"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000049"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000050"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000051"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000052"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000053"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000054"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000055"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000056"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000057"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000058"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000059"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000060"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000061"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000062"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000063"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000064"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000065"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000066"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000067"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000068"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000069"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000070"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000071"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000072"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000073"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000074"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000075"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000076"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000077"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000078"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000079"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000080"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000081"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000082"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000083"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000084"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000085"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000086"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000087"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000088"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000089"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000090"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000091"),
                column: "protecao_justificativa",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000092"),
                column: "protecao_justificativa",
                value: null);

            // Sem produção, o dado local é renomeado e o token antigo não é mantido como sinônimo.
            migrationBuilder.Sql("UPDATE configuracao.tipo_documento SET protecao_situacao = 'AJUSTE_RECOMENDADO' WHERE protecao_situacao = 'NEGADA';");
            migrationBuilder.Sql("UPDATE configuracao.rol_de_fatos_candidato SET protecao_situacao = 'AJUSTE_RECOMENDADO' WHERE protecao_situacao = 'NEGADA';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_ajuste_com_recomendacao",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_reprovada_com_justificativa",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_protecao_situacao",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'AJUSTE_RECOMENDADO', 'REPROVADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_ajuste_com_recomendacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_reprovada_com_justificativa",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_situacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'AJUSTE_RECOMENDADO', 'REPROVADA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_ajuste_com_recomendacao",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_reprovada_com_justificativa",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_protecao_situacao",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_ajuste_com_recomendacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_reprovada_com_justificativa",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_protecao_situacao",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            // O vocabulário antigo não distingue ajuste de reprovação: ambos voltam a NEGADA, e a reprovação, que não tem
            // classificação recomendada, leva a do próprio item para satisfazer a regra da negativa.
            migrationBuilder.Sql("UPDATE configuracao.tipo_documento SET protecao_recomendada = COALESCE(protecao_recomendada, classificacao_protecao), protecao_situacao = 'NEGADA' WHERE protecao_situacao IN ('AJUSTE_RECOMENDADO', 'REPROVADA');");
            migrationBuilder.Sql("UPDATE configuracao.rol_de_fatos_candidato SET protecao_recomendada = COALESCE(protecao_recomendada, classificacao_protecao), protecao_situacao = 'NEGADA' WHERE protecao_situacao IN ('AJUSTE_RECOMENDADO', 'REPROVADA');");

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

            migrationBuilder.DropColumn(
                name: "protecao_justificativa",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "protecao_justificativa",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");
        }
    }
}
