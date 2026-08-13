using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlgoritmosContagemPrazo : Migration
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
                    { new Guid("d0a00000-0000-7000-8000-000000000020"), "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada pelo edital; nenhuma citação aproximada substitui este texto.", "CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "63ef57b6b32c023cdcb4ba9406d84f9e63694d4a9a8ee46bd9b532bbedd08b72", "[\"âncora fora da meia-noite: a hora da âncora não influencia o fechamento — o dia civil da âncora é excluído por inteiro e a contagem parte do primeiro dia útil seguinte (1 dia útil ancorado sexta 18h, sem feriado no intervalo, fecha no fim de segunda)\",\"âncora em dia não útil: o início desloca para o primeiro dia útil seguinte; o dia da âncora, útil ou não, nunca conta (1 dia útil ancorado domingo 18h, sem feriado no intervalo, fecha no fim de segunda)\",\"em dias úteis: N dias úteis inteiros contados após o dia excluído; a janela fecha na fronteira final do N-ésimo dia útil — dia civil fechado no início e aberto no fim, no fuso congelado\",\"em horas: a contagem começa no primeiro instante do primeiro dia útil seguinte ao dia da âncora e consome apenas horas situadas em dia útil (48h ancoradas sexta 18h, sem feriado no intervalo, começam segunda 00:00 e fecham quarta 00:00)\"]", "algoritmo_contagem_prazo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000021"), "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada pelo edital; nenhuma citação aproximada substitui este texto.", "CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "01bb4ae923690f2ae79373112069723569fc286ee3054bc1e190336256decd56", "[\"âncora fora da meia-noite: a contagem parte do instante exato da âncora — a hora do fechamento deriva da hora da âncora, sem deslocamento para fronteira de dia (48h ancoradas sexta 18h, com sábado e domingo não úteis e sem feriado, fecham terça 18h)\",\"âncora em dia não útil: o início não desloca — o relógio não avança em instante situado em dia não útil e o primeiro avanço ocorre no primeiro instante útil seguinte (24h ancoradas domingo 18h, com segunda útil, só começam a consumir segunda 00:00 e fecham terça 00:00)\",\"em horas: consome exatamente o valor declarado em horas situadas em dia útil, atravessando a madrugada de dia útil normalmente; fecha no instante em que o saldo zera\",\"em dias úteis: N dias úteis equivalem a N×24 horas situadas em dia útil consumidas desde a âncora; dia civil de transição de fuso contribui com as horas que realmente tem, nunca um bloco presumido de 24 (1 dia útil ancorado sexta 18h, sem feriado no intervalo, fecha segunda 18h)\"]", "algoritmo_contagem_prazo", null, "v1" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fronteira append-only do rol_de_regras (ADR-0112): uma entrada
            // referenciada por configuração congelada é fato imutável — a
            // reversão só é legítima enquanto nenhuma VersaoConfiguracao citar
            // os códigos semeados. O token é casado entre aspas (valor JSON
            // delimitado), para que um código que apenas contenha um destes
            // como prefixo não bloqueie a reversão por engano.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada::text LIKE '%"CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL"%'
                           OR configuracao_congelada::text LIKE '%"CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA"%'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: entrada de algoritmo de contagem referenciada por versão de configuração congelada; remover viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000020"));

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000021"));
        }
    }
}
