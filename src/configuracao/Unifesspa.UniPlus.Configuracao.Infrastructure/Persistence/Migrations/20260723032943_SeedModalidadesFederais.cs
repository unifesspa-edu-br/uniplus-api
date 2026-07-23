using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedModalidadesFederais : Migration
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
                    { new Guid("70da1000-0000-7000-8000-000000000001"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "AC", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Ampla concorrência", false, "AMPLA", null, "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000002"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_PPI", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — baixa renda, preto/pardo/indígena", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000003"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_Q", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — baixa renda, quilombola", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000004"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_PCD", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — baixa renda, pessoa com deficiência", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000005"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_EP", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — baixa renda, egresso de escola pública", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000006"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_PPI", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — independente de renda, preto/pardo/indígena", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000007"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_Q", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — independente de renda, quilombola", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000008"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_PCD", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — independente de renda, pessoa com deficiência", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000009"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_EP", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Cota — independente de renda, egresso de escola pública", false, "COTA_RESERVADA", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000010"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "AC_PCD", "AC", "RETIRA_DE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "[]", null, null, "Ampla Concorrência – Pessoa com Deficiência (V)", false, "OUTRA_MODALIDADE", "DESTINO_UNICO", "{\"destino\":\"AC\",\"par\":null,\"fallback\":null}", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"));
        }
    }
}
