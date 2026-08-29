using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaModalidadesInstitucionaisPsiq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "modalidade",
                columns: new[] { "id", "acao_quando_indeferido", "base_legal", "codigo", "composicao_origem", "composicao_vagas", "created_at", "created_by", "criterios_cumulativos", "deleted_at", "deleted_by", "descricao", "is_deleted", "natureza_legal", "regra_remanejamento", "remanejamento_args", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("70da1000-0000-7000-8000-000000000012"), null, "Res. Unifesspa 22/2014-CONSEPE, atualizada pela Res. Unifesspa 532/2021-CONSEPE (vagas por acréscimo para candidatos indígenas e quilombolas)", "AC_I", null, "SUPLEMENTAR_AO_TOTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Vaga por acréscimo — candidato indígena (PSIQ)", false, "SUPLEMENTAR", "CRUZADO", "{\"destino\":null,\"par\":\"AC_Q\",\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000013"), null, "Res. Unifesspa 22/2014-CONSEPE, atualizada pela Res. Unifesspa 532/2021-CONSEPE (vagas por acréscimo para candidatos indígenas e quilombolas)", "AC_Q", null, "SUPLEMENTAR_AO_TOTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Vaga por acréscimo — candidato quilombola (PSIQ)", false, "SUPLEMENTAR", "CRUZADO", "{\"destino\":null,\"par\":\"AC_I\",\"fallback\":null}", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000012"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000013"));
        }
    }
}
