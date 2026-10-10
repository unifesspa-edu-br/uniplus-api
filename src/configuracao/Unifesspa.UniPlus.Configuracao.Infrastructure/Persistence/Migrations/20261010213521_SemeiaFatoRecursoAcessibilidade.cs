using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaFatoRecursoAcessibilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by" },
                values: new object[] { new Guid("fa700000-0000-7000-8000-000000000053"), true, "CAMPO_FORMULARIO:RECURSO_ACESSIBILIDADE", "MULTIVALORADO", "SENSIVEL", "RECURSO_ACESSIBILIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, Array.Empty<string>(), null, "CATEGORICO", "CANDIDATO", "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.", "PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Recurso de acessibilidade", "DECLARADO", "INSCRICAO", "[]", true, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                keyColumn: "id",
                keyValue: new Guid("fa700000-0000-7000-8000-000000000053"));
        }
    }
}
