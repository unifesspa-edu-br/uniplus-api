using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaModalidadePcdPuro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "modalidade",
                columns: new[] { "id", "acao_quando_indeferido", "base_legal", "codigo", "composicao_origem", "composicao_vagas", "created_at", "created_by", "criterios_cumulativos", "deleted_at", "deleted_by", "descricao", "is_deleted", "natureza_legal", "regra_remanejamento", "remanejamento_args", "updated_at", "updated_by" },
                values: new object[] { new Guid("70da1000-0000-7000-8000-000000000011"), null, "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)", "PCD_PURO", "AC", "RETIRA_DE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Pessoa com Deficiência — reserva sem as cotas da Lei 12.711", false, "OUTRA_MODALIDADE", "DESTINO_UNICO", "{\"destino\":\"AC\",\"par\":null,\"fallback\":null}", null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000011"));
        }
    }
}
