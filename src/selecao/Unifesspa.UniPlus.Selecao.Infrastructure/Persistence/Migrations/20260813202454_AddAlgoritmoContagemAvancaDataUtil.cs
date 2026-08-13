using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlgoritmoContagemAvancaDataUtil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[] { new Guid("d0a00000-0000-7000-8000-000000000022"), "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada pelo edital; nenhuma citação aproximada substitui este texto.", "CONTAGEM-PRAZO-AVANCA-DATA-UTIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "73bf9e2448e2656e421243dff5f375c7fc9598eca71a5f291169efe01b777922", "[\"âncora fora da meia-noite: mantém a hora da âncora — a contagem parte do instante exato, sem deslocamento para fronteira de dia (1 dia útil ancorado sexta 18h, com sábado e domingo não úteis e sem feriado, fecha segunda 18h)\",\"âncora em dia não útil: em dias úteis, desloca para o próximo dia útil na mesma hora (âncora domingo 18h conta como segunda 18h, e 1 dia útil fecha terça 18h); em horas não há deslocamento, apenas a não contagem dos instantes de dia não útil\",\"em dias úteis: fecha na mesma hora da âncora, N datas úteis adiante, pulando cada data não útil; se a hora da âncora não existir na data de fechamento por transição de fuso, fecha no primeiro instante válido seguinte\",\"em horas: consome horas situadas em dia útil desde a âncora, sem deslocar o início — nesta unidade a convenção coincide com CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA, e a diferença entre as duas está só na unidade dias úteis\"]", "algoritmo_contagem_prazo", null, "v1" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fronteira append-only do rol_de_regras (ADR-0112): a reversão só é
            // legítima enquanto nenhuma configuração congelada referenciar a
            // entrada que este Down remove.
            //
            // A busca é estrutural — referência de regra é a tripla
            // {codigo, versao, hash} —, porque o snapshot serializa muitos
            // outros objetos sob a chave bare `codigo` com valor declarado pelo
            // administrador, e homônimo não é referência. O predicado nomeia só
            // a entrada desta migration: uma configuração que referencie outra
            // convenção de contagem, ou uma versão que este Down não remove, não
            // pode bloquear a reversão.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "CONTAGEM-PRAZO-AVANCA-DATA-UTIL" && @.versao == "v1" && exists(@.hash))'
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
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000022"));
        }
    }
}
