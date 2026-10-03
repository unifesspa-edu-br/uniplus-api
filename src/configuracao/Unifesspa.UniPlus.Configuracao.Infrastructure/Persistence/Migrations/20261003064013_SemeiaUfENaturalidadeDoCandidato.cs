using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaUfENaturalidadeDoCandidato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000041"), true, "CAMPO_INSCRICAO:RG_UF", "ESCALAR", "PESSOAL", "RG_UF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GEO_UF", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de emissão do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000042"), true, "CAMPO_INSCRICAO:NATURALIDADE_UF", "ESCALAR", "PESSOAL", "NATURALIDADE_UF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GEO_UF", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000043"), true, "CAMPO_INSCRICAO:NATURALIDADE_MUNICIPIO", "ESCALAR", "PESSOAL", "NATURALIDADE_MUNICIPIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GEO_MUNICIPIO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Município de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000041"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000042"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000043"));
        }
    }
}
