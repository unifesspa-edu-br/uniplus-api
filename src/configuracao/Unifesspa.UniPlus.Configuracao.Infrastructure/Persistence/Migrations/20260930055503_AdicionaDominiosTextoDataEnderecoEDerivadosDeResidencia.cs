using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDominiosTextoDataEnderecoEDerivadosDeResidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddColumn<string>(
                name: "formato",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000001"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000002"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000003"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000004"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000005"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000006"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000007"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000008"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000009"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000010"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000011"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000012"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000013"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000014"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000015"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000016"),
                column: "formato",
                value: null);

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000017"),
                column: "formato",
                value: null);

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000018"), true, "CAMPO_INSCRICAO:ENDERECO_RESIDENCIAL", "ESCALAR", "PESSOAL", "ENDERECO_RESIDENCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "ENDERECO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Endereço residencial", "DECLARADO", "INSCRICAO", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000019"), true, "CAMPO_INSCRICAO:DATA_NASCIMENTO", "ESCALAR", "PESSOAL", "DATA_NASCIMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "DATA", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Data de nascimento", "DECLARADO", "INSCRICAO", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000020"), true, "ATRIBUTO_CANDIDATO:UF_RESIDENCIA", "ESCALAR", "PESSOAL", "UF_RESIDENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "CATEGORICO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "GEO_UF", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de residência", "DERIVADO", "INSCRICAO", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000021"), true, "ATRIBUTO_CANDIDATO:MUNICIPIO_RESIDENCIA", "ESCALAR", "PESSOAL", "MUNICIPIO_RESIDENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "CATEGORICO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "GEO_MUNICIPIO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Município de residência", "DERIVADO", "INSCRICAO", true, null, null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') OR classificacao_protecao IN ('PESSOAL', 'SENSIVEL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "dominio IN ('CATEGORICO', 'BOOLEANO', 'NUMERICO', 'TEXTO', 'DATA', 'ENDERECO')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'CATEGORICO' AND fonte_valores IS NOT NULL AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE', 'MUNICIPIOS_BONUS', 'GEO_UF', 'GEO_MUNICIPIO')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_formato_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'TEXTO' AND formato IS NOT NULL AND formato IN ('LIVRE', 'CPF', 'EMAIL', 'TELEFONE', 'CEP', 'NOME_PESSOA')) OR (dominio <> 'TEXTO' AND formato IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_formato_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000018"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000019"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000020"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000021"));

            migrationBuilder.DropColumn(
                name: "formato",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "dominio IN ('CATEGORICO', 'BOOLEANO', 'NUMERICO')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_fonte_valores_coerente",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(dominio = 'CATEGORICO' AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE', 'MUNICIPIOS_BONUS')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");
        }
    }
}
