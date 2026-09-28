using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiRegraComPcdPuroPorComAcPcd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000025"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "010df079f624fb7a3306eed54285fa5ad1f9e3be8f43195527c67b99b1534398" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000026"),
                columns: new[] { "base_legal", "codigo", "esquema_args", "hash", "invariantes" },
                values: new object[] { "Res. Unifesspa 532/2021-CONSEPE, art. 1º (reserva de vaga para pessoa com deficiência); Portaria MEC 18/2012 art. 12", "DISTRIB-VAGAS-COM-AC-PCD", "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro fixo sem as cotas federais — AC_PCD como reserva de pessoa com deficiência fora do regime federal\",\"modalidades_admitidas\":[\"AC\",\"AC_PCD\"]}", "2bef4de324234a2d63164008d2e6dec6b4bb8901d816ee04f6824cbcc0027f80", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame sem as cotas da Lei 12.711\",\"AC_PCD retira de AC: o par fecha no VO_base\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000025"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "Res. Unifesspa 22/2014-CONSEPE, atualizada pela Res. Unifesspa 532/2021-CONSEPE (vagas por acréscimo para candidatos indígenas e quilombolas)", "4e7143abcfc92e95cf320ff395f7d2ce0205d72dc5a101fc083d4f09a352b567" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000026"),
                columns: new[] { "base_legal", "codigo", "esquema_args", "hash", "invariantes" },
                values: new object[] { "Res. Unifesspa 64/2015-CONSEPE (reserva de vaga para pessoa com deficiência); Portaria MEC 18/2012 art. 12", "DISTRIB-VAGAS-COM-PCD-PURO", "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro fixo sem as cotas federais — PCD_PURO como reserva de qualquer processo fora do regime federal\",\"modalidades_admitidas\":[\"AC\",\"PCD_PURO\"]}", "18dbc3ea7b3ade62c4edcff134124d5c1dd4535367c8f33678176cd7749510ce", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame sem as cotas da Lei 12.711\",\"PCD_PURO retira de AC: o par fecha no VO_base\"]" });
        }
    }
}
