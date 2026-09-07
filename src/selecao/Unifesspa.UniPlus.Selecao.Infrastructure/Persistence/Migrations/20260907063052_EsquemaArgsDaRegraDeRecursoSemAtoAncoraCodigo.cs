using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaArgsDaRegraDeRecursoSemAtoAncoraCodigo : Migration
    {
        /// <summary>Hash da definição que esta migration passa a valer.</summary>
        private const string HashDaDefinicaoVigente =
            "42cbba27f7e36789437ecb469c74476c99a37baaf66404769774eeff619b9e2d";

        /// <summary>Hash da definição anterior, para o qual a reversão devolve as referências vivas.</summary>
        private const string HashDaDefinicaoAnterior =
            "92e78394a057b6eadbdcb69c7b08793ff8801790856874d99355074483b2709c";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ExigirQueNenhumaConfiguracaoCongeladaReferencie(migrationBuilder);
            ReapontarHashDasRegrasDeRecursoVivas(migrationBuilder, HashDaDefinicaoVigente);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000018"),
                columns: new[] { "esquema_args", "hash", "invariantes" },
                values: new object[] { "{\"prazo_valor\":\"numeric (> 0) — inteiro quando a unidade é DIAS_UTEIS\",\"prazo_unidade\":\"DIAS_UTEIS|HORAS (sem default — dado do edital; DIAS não é declarável na interposição)\",\"suspensividade_primeira_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência na fase não bloqueia atos irreversíveis\",\"suspensividade_segunda_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência em instância superior não bloqueia (via judicial, prazo indeterminado)\"}", "42cbba27f7e36789437ecb469c74476c99a37baaf66404769774eeff619b9e2d", "[\"o Uni+ gere apenas a 1ª instância — o julgamento em instância superior (administrativa ou judicial) corre FORA do sistema; a sua existência e o seu desfecho são REGISTRADOS como ato publicado\",\"a suspensividade é configurável por fase e por grau: null = a pendência não bloqueia atos irreversíveis\",\"a janela de suspensividade fecha no julgamento OU no fim do prazo, o que vier primeiro — recurso nunca julgado não trava o certame para sempre\",\"interposição só é aceita com a janela da fase de recurso aberta\",\"não cabe recurso contra resultado definitivo\",\"prazo ancorado no instante de publicação do ato âncora: se o ato atrasa, o prazo desliza junto, sem retificação\",\"a âncora é um produto PRELIMINAR publicado pela própria fase, referenciado pela identidade da publicação e não pelo código do tipo de ato — o mesmo tipo publicado por outra fase do cronograma não é âncora desta\",\"a âncora nunca é um tipo de ato que congela configuração\",\"o prazo de INTERPOSIÇÃO corre exclusivamente em dia útil e admite apenas DIAS_UTEIS em valor inteiro ou HORAS — DIAS corridos é recusado, e fração de dia útil também, cada um com causa própria; nunca aproximado em silêncio\",\"contagem sobre dia útil depende de duas declarações do processo, o calendário de dias úteis vigente da localidade regente e a convenção de contagem — vale para todo prazo de INTERPOSIÇÃO, nas duas unidades, e para a suspensividade em DIAS_UTEIS\",\"a suspensividade em HORAS ou em DIAS corridos não depende de calendário nem de convenção de contagem — conta todos os dias, sem distinguir úteis de não úteis\",\"append-only: julgamento e retificação são NOVO fato, não sobrescrevem o passado\"]" });
        }

        /// <summary>
        /// Fronteira append-only do <c>rol_de_regras</c> (ADR-0112): substituir a definição
        /// no lugar só é legítimo enquanto nenhuma versão de configuração congelada
        /// referenciar a entrada. A partir da primeira referência, a definição vira fato
        /// reproduzível, e evoluir passa a exigir versão sucessora.
        /// </summary>
        /// <remarks>
        /// A busca é estrutural — referência de regra é a tripla <c>{codigo, versao, hash}</c>
        /// —, porque o snapshot serializa muitos outros objetos sob a chave bare
        /// <c>codigo</c> com valor declarado pelo administrador, e homônimo não é
        /// referência. A guarda é o que torna a escolha segura em vez de presumida, e faz o
        /// deploy abortar em vez de reescrever a definição sob um edital que a cita.
        /// </remarks>
        private static void ExigirQueNenhumaConfiguracaoCongeladaReferencie(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "RECURSO-PRAZO-ANCORADO-EM-ATO" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: RECURSO-PRAZO-ANCORADO-EM-ATO/v1 referenciada por versão de configuração congelada; substituir a definição no lugar viola o append-only (ADR-0112) — evoluir exige versão sucessora';
                    END IF;
                END
                $adr0112$;
                """);
        }

        /// <summary>
        /// As regras de recurso <b>vivas</b> — as de rascunho, que ainda não foram congeladas
        /// em versão nenhuma e por isso escapam da guarda acima — guardam o hash da definição
        /// anterior em coluna própria. Como a substituição é no lugar e não há versão
        /// sucessora, o hash antigo passaria a não descrever definição nenhuma do catálogo: a
        /// referência acompanha a substituição.
        /// </summary>
        /// <remarks>
        /// Ao contrário da substituição que inverteu a política de unidade do prazo, esta não
        /// tem guarda de dado a redeclarar: nenhum valor declarado por quem configurou deixa
        /// de ser admissível — o que sai do <c>esquema_args</c> é a descrição de um argumento
        /// que a entidade não tem mais, não um argumento que alguém preencheu.
        /// </remarks>
        private static void ReapontarHashDasRegrasDeRecursoVivas(
            MigrationBuilder migrationBuilder,
            string hashDestino) =>
            migrationBuilder.Sql($"""
                UPDATE selecao.regras_recurso_fase
                SET regra_hash = '{hashDestino}'
                WHERE regra_codigo = 'RECURSO-PRAZO-ANCORADO-EM-ATO'
                  AND regra_versao = 'v1'
                  AND regra_hash <> '{hashDestino}';
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A reversão reescreve a mesma definição e responde à mesma fronteira: se alguma
            // versão passou a referenciar a entrada depois do Up, voltar a definição antiga
            // quebraria a reprodutibilidade daquela versão tanto quanto avançá-la.
            ExigirQueNenhumaConfiguracaoCongeladaReferencie(migrationBuilder);
            ReapontarHashDasRegrasDeRecursoVivas(migrationBuilder, HashDaDefinicaoAnterior);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000018"),
                columns: new[] { "esquema_args", "hash", "invariantes" },
                values: new object[] { "{\"prazo_valor\":\"numeric (> 0) — inteiro quando a unidade é DIAS_UTEIS\",\"prazo_unidade\":\"DIAS_UTEIS|HORAS (sem default — dado do edital; DIAS não é declarável na interposição)\",\"ato_ancora_codigo\":\"código do tipo de ato — o prazo conta do INSTANTE DE PUBLICAÇÃO do ato, nunca de data fixa; a âncora nunca é um ato que congela configuração\",\"suspensividade_primeira_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência na fase não bloqueia atos irreversíveis\",\"suspensividade_segunda_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência em instância superior não bloqueia (via judicial, prazo indeterminado)\"}", "92e78394a057b6eadbdcb69c7b08793ff8801790856874d99355074483b2709c", "[\"o Uni+ gere apenas a 1ª instância — o julgamento em instância superior (administrativa ou judicial) corre FORA do sistema; a sua existência e o seu desfecho são REGISTRADOS como ato publicado\",\"a suspensividade é configurável por fase e por grau: null = a pendência não bloqueia atos irreversíveis\",\"a janela de suspensividade fecha no julgamento OU no fim do prazo, o que vier primeiro — recurso nunca julgado não trava o certame para sempre\",\"interposição só é aceita com a janela da fase de recurso aberta\",\"não cabe recurso contra resultado definitivo\",\"prazo ancorado no instante de publicação do ato âncora: se o ato atrasa, o prazo desliza junto, sem retificação\",\"a âncora nunca é um tipo de ato que congela configuração\",\"o prazo de INTERPOSIÇÃO corre exclusivamente em dia útil e admite apenas DIAS_UTEIS em valor inteiro ou HORAS — DIAS corridos é recusado, e fração de dia útil também, cada um com causa própria; nunca aproximado em silêncio\",\"contagem sobre dia útil depende de duas declarações do processo, o calendário de dias úteis vigente da localidade regente e a convenção de contagem — vale para todo prazo de INTERPOSIÇÃO, nas duas unidades, e para a suspensividade em DIAS_UTEIS\",\"a suspensividade em HORAS ou em DIAS corridos não depende de calendário nem de convenção de contagem — conta todos os dias, sem distinguir úteis de não úteis\",\"append-only: julgamento e retificação são NOVO fato, não sobrescrevem o passado\"]" });
        }
    }
}
