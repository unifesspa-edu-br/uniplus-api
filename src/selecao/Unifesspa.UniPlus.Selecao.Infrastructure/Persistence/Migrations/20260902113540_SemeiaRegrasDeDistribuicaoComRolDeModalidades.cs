using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaRegrasDeDistribuicaoComRolDeModalidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[,]
                {
                    { new Guid("d0a00000-0000-7000-8000-000000000023"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIB-VAGAS-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\",\"AC_PCD\"]}", "0951eef80fb6fd6af566751547a7566a152dfcc18d4053c5060df10c1d73a88b", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"INV-3a: LB_EP≥0 e LI_EP≥0\",\"INV-3b: AC≥0\",\"INV-3c: VR_final+RETIRADAS+AC=VO_base\",\"modalidade fora de modalidades_admitidas é recusada\"]", "regra_distribuicao_vagas", null, "v2" },
                    { new Guid("d0a00000-0000-7000-8000-000000000024"), "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)", "DISTRIB-VAGAS-INSTITUCIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro institucional não nomeado por regra própria\",\"modalidades_admitidas\":null}", "faa74ff68dcf4d38e22690a873bf84b2525fe59696b329af7460860d6c3ca409", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"a soma das quantidades declaradas é o total publicado, e fecha no VO_base\",\"modalidades_admitidas nulo: rol aberto, para o certame institucional que ainda não tem regra própria\"]", "regra_distribuicao_vagas", null, "v2" },
                    { new Guid("d0a00000-0000-7000-8000-000000000025"), "Res. Unifesspa 22/2014-CONSEPE, atualizada pela Res. Unifesspa 532/2021-CONSEPE (vagas por acréscimo para candidatos indígenas e quilombolas)", "DISTRIB-VAGAS-PSIQ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"Processo Seletivo Indígena e Quilombola\",\"modalidades_admitidas\":[\"AC_I\",\"AC_Q\"]}", "4e7143abcfc92e95cf320ff395f7d2ce0205d72dc5a101fc083d4f09a352b567", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame exclusivo: não há ampla concorrência\",\"rol composto só de vagas por acréscimo — sem outro conjunto ao qual se somem, a soma delas é o total publicado\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000026"), "Res. Unifesspa 64/2015-CONSEPE (reserva de vaga para pessoa com deficiência); Portaria MEC 18/2012 art. 12", "DISTRIB-VAGAS-EDU-CAMPO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"PSE Educação do Campo\",\"modalidades_admitidas\":[\"AC\",\"PCD_PURO\"]}", "bf890f415c8e5e58a3c64ca45bf32f958c8fa4d1730f2c0155fb7c6fe6581c42", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame sem as cotas da Lei 12.711\",\"PCD_PURO retira de AC: o par fecha no VO_base\"]", "regra_distribuicao_vagas", null, "v1" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000023"));

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000024"));

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000025"));

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000026"));
        }
    }
}
