using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaFatosDeMembroDeSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000023"), true, "CAMPO_INSCRICAO:MAIOR_IDADE", "ESCALAR", "PESSOAL", "MAIOR_IDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "BOOLEANO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Maior de idade", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000024"), true, "CAMPO_INSCRICAO:SEM_RENDA", "ESCALAR", "PESSOAL", "SEM_RENDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "BOOLEANO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sem renda", "DECLARADO", "INSCRICAO", "[]", true, null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000025"), true, "CAMPO_INSCRICAO:SOB_GUARDA", "ESCALAR", "PESSOAL", "SOB_GUARDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "BOOLEANO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sob guarda", "DECLARADO", "INSCRICAO", "[]", true, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000023"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000024"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000025"));
        }
    }
}
