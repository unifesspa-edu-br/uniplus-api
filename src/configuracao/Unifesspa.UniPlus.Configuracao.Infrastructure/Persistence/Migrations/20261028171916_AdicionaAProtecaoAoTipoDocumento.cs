using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// O tipo que o operador cadastrou antes da migration recebe, pelo default da coluna, o padrão
    /// provisório (pessoal, com a finalidade e a hipótese da semente), para que a leitura do
    /// catálogo não falhe numa linha sem proteção; a classificação definitiva vem do cadastro.
    /// </remarks>
    public partial class AdicionaAProtecaoAoTipoDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "classificacao_protecao",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PESSOAL");

            migrationBuilder.AddColumn<string>(
                name: "finalidade_tratamento",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.");

            migrationBuilder.AddColumn<string>(
                name: "hipotese_legal",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "CUMPRIMENTO_OBRIGACAO_LEGAL");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000001"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000002"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000003"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000004"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000005"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000006"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000007"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "IDENTIFICADOR", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000008"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000009"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000010"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000011"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000012"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000013"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000014"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000015"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000016"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000017"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000018"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000019"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000020"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000021"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000022"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000023"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000024"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000025"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000026"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000027"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000028"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000029"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000030"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000031"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000032"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000033"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000034"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000035"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000036"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000037"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000038"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000039"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000040"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000041"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000042"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000043"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000044"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000045"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000046"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000047"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000048"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "SENSIVEL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000049"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000050"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000051"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000052"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000053"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000054"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000055"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000056"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000057"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000058"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000059"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000060"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000061"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000062"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000063"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000064"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000065"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000066"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000067"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000068"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000069"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: new Guid("d0c00000-0000-7000-8000-000000000070"),
                columns: new[] { "classificacao_protecao", "finalidade_tratamento", "hipotese_legal" },
                values: new object[] { "PESSOAL", "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "classificacao_protecao",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "finalidade_tratamento",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.DropColumn(
                name: "hipotese_legal",
                schema: "configuracao",
                table: "tipo_documento");
        }
    }
}
