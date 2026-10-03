using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaConjuntoBasicoDoCandidato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000027"), true, "CAMPO_INSCRICAO:NOME", "ESCALAR", "PESSOAL", "NOME", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000028"), true, "CAMPO_INSCRICAO:DESEJA_NOME_SOCIAL", "ESCALAR", "PESSOAL", "DESEJA_NOME_SOCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "BOOLEANO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja usar nome social", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000029"), true, "CAMPO_INSCRICAO:NOME_SOCIAL", "ESCALAR", "PUBLICO", "NOME_SOCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome social", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000030"), true, "CAMPO_INSCRICAO:CPF", "ESCALAR", "PESSOAL", "CPF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "CPF", "CUMPRIMENTO_OBRIGACAO_LEGAL", "CPF", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000031"), true, "CAMPO_INSCRICAO:RG_NUMERO", "ESCALAR", "PESSOAL", "RG_NUMERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000032"), true, "CAMPO_INSCRICAO:RG_ORGAO_EMISSOR", "ESCALAR", "PESSOAL", "RG_ORGAO_EMISSOR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Órgão emissor do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000033"), true, "CAMPO_INSCRICAO:RG_DATA_EMISSAO", "ESCALAR", "PESSOAL", "RG_DATA_EMISSAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "DATA", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Data de emissão do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000034"), true, "CAMPO_INSCRICAO:DOCUMENTO_ESTRANGEIRO_TIPO", "ESCALAR", "PESSOAL", "DOCUMENTO_ESTRANGEIRO_TIPO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Documento de identificação do estrangeiro", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000035"), true, "CAMPO_INSCRICAO:DOCUMENTO_ESTRANGEIRO_NUMERO", "ESCALAR", "PESSOAL", "DOCUMENTO_ESTRANGEIRO_NUMERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do documento do estrangeiro", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000036"), true, "CAMPO_INSCRICAO:NOME_MAE", "ESCALAR", "PESSOAL", "NOME_MAE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome da mãe", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000037"), true, "CAMPO_INSCRICAO:NOME_PAI", "ESCALAR", "PESSOAL", "NOME_PAI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome do pai", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000038"), true, "CAMPO_INSCRICAO:ESTADO_CIVIL", "ESCALAR", "PESSOAL", "ESTADO_CIVIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Estado civil", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000039"), true, "CAMPO_INSCRICAO:EMAIL", "ESCALAR", "PESSOAL", "EMAIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Comunicação com o candidato sobre o processo seletivo.", null, "EMAIL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "E-mail", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000040"), true, "CAMPO_INSCRICAO:TELEFONE", "ESCALAR", "PESSOAL", "TELEFONE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "TEXTO", "CANDIDATO", "Comunicação com o candidato sobre o processo seletivo.", null, "TELEFONE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Telefone", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000025"), true, "PASSAPORTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Passaporte.", new Guid("fa700000-0000-7000-8000-000000000034"), 0, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000026"), true, "RNM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Registro Nacional Migratório.", new Guid("fa700000-0000-7000-8000-000000000034"), 1, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000027"), true, "SOLTEIRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solteiro(a).", new Guid("fa700000-0000-7000-8000-000000000038"), 0, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000028"), true, "CASADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Casado(a).", new Guid("fa700000-0000-7000-8000-000000000038"), 1, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000029"), true, "UNIAO_ESTAVEL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Em união estável.", new Guid("fa700000-0000-7000-8000-000000000038"), 2, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000030"), true, "SEPARADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Separado(a) judicialmente.", new Guid("fa700000-0000-7000-8000-000000000038"), 3, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000031"), true, "DIVORCIADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Divorciado(a).", new Guid("fa700000-0000-7000-8000-000000000038"), 4, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000032"), true, "VIUVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Viúvo(a).", new Guid("fa700000-0000-7000-8000-000000000038"), 5, null }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "(sistema AND codigo = 'NOME_SOCIAL' AND dominio = 'TEXTO' AND classificacao_protecao = 'PUBLICO') OR dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') OR classificacao_protecao IN ('PESSOAL', 'SENSIVEL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato");

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000025"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000026"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000027"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000028"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000029"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000030"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000031"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                keyColumn: "id",
                keyValue: new Guid("fa70d000-0000-7000-8000-000000000032"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000027"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000028"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000029"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000030"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000031"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000032"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000033"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000035"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000036"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000037"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000039"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000040"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000034"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000038"));

            migrationBuilder.AddCheckConstraint(
                name: "ck_rol_de_fatos_candidato_classificacao_minima_do_dominio",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                sql: "dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') OR classificacao_protecao IN ('PESSOAL', 'SENSIVEL')");
        }
    }
}
