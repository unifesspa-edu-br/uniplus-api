using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiRegraRecursoMultiInstancia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000018"),
                columns: new[] { "base_legal", "codigo", "esquema_args", "hash", "invariantes" },
                values: new object[] { "Lei 9.784/1999 art. 56 (cabimento do recurso administrativo) e art. 61 (efeito suspensivo por decisão fundamentada); prazo configurável por edital", "RECURSO-PRAZO-ANCORADO-EM-ATO", "{\"prazo_valor\":\"numeric (> 0)\",\"prazo_unidade\":\"HORAS|DIAS|DIAS_UTEIS (sem default — dado do edital)\",\"ato_ancora_codigo\":\"código do tipo de ato — o prazo conta do INSTANTE DE PUBLICAÇÃO do ato, nunca de data fixa; a âncora nunca é um ato que congela configuração\",\"suspensividade_primeira_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência na fase não bloqueia atos irreversíveis\",\"suspensividade_segunda_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência em instância superior não bloqueia (via judicial, prazo indeterminado)\"}", "94f2a02a12cccae0ebe98dabc9dc66b5aacac25053e91b768fdf0d47492e8240", "[\"o Uni+ gere apenas a 1ª instância — o julgamento em instância superior (administrativa ou judicial) corre FORA do sistema; a sua existência e o seu desfecho são REGISTRADOS como ato publicado\",\"a suspensividade é configurável por fase e por grau: null = a pendência não bloqueia atos irreversíveis\",\"a janela de suspensividade fecha no julgamento OU no fim do prazo, o que vier primeiro — recurso nunca julgado não trava o certame para sempre\",\"interposição só é aceita com a janela da fase de recurso aberta\",\"não cabe recurso contra resultado definitivo\",\"prazo ancorado no instante de publicação do ato âncora: se o ato atrasa, o prazo desliza junto, sem retificação\",\"a âncora nunca é um tipo de ato que congela configuração\",\"DIAS_UTEIS é recusado na INTERPOSIÇÃO enquanto não houver calendário — nunca aproximado em silêncio\",\"append-only: julgamento e retificação são NOVO fato, não sobrescrevem o passado\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000018"),
                columns: new[] { "base_legal", "codigo", "esquema_args", "hash", "invariantes" },
                values: new object[] { "Lei 9.784/1999 (processo administrativo federal); edital/processo (prazo configurável por edital); regimento CONSEPE (2ª instância)", "RECURSO-MULTI-INSTANCIA", "{\"prazo_valor\":\"numeric\",\"prazo_unidade\":\"HORAS|DIAS|DIAS_UTEIS (sem default — dado do edital)\",\"contra_ato\":\"codigo da fase produtora recorrível (NUNCA resultado definitivo)\",\"marco_inicial\":\"a partir de quando conta (ex.: publicacao_resultado)\",\"instancias\":\"lista ordenada [{instancia,dono,prazo_valor,prazo_unidade}] — 1ª CEPS, 2ª CONSEPE\"}", "660cf3fffe22069f5a7f302a98b1e44b96d2e992680f02304935a36548f95490", "[\"interposição GATED pela janela aberta (sem recurso a qualquer momento)\",\"sem recurso ao resultado definitivo (≠ recurso de habilitação CRCA)\",\"2ª instância (CONSEPE) só após indeferimento da 1ª; CONSEPE emite parecer externo, CEPS anexa e aplica (vinculante)\",\"processo PARALISA enquanto 2ª instância pendente; avanço só após expirar o prazo\",\"append-only: parecer/retificação = novo fato, não sobrescreve o passado\"]" });
        }
    }
}
