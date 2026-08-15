using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FundamentoDeclaradoPeloEditalNasConvencoesDeContagem : Migration
    {

        // As três entradas de contagem, com o hash de cada definição nos dois sentidos da
        // migration. A referência viva do processo aponta o hash, então é ele que precisa
        // acompanhar a substituição.
        private static readonly (string Codigo, string HashVigente, string HashAnterior)[] Convencoes =
        [
            ("CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL",
                "fce95fc44b52a5a93a697b0309659a5af0085f9d39ceac1c3917c7b00b1c0be5",
                "63ef57b6b32c023cdcb4ba9406d84f9e63694d4a9a8ee46bd9b532bbedd08b72"),
            ("CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA",
                "49b637293aaaa71449dcb971fc548c59fc840545d2d1740ac352f858a291105b",
                "01bb4ae923690f2ae79373112069723569fc286ee3054bc1e190336256decd56"),
            ("CONTAGEM-PRAZO-AVANCA-DATA-UTIL",
                "cd4c631492d02126c88a7ca5558992b3ed8a27c80692ecabfb73609293f2a9c8",
                "73bf9e2448e2656e421243dff5f375c7fc9598eca71a5f291169efe01b777922"),
        ];

        /// <summary>
        /// Trava, num passo só e em ordem fixa, as duas tabelas que a fronteira toca.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Por que EXCLUSIVE e não SHARE.</b> O caminho de mutação do processo abre com
        /// <c>SELECT ... FOR UPDATE</c>, que toma <c>ROW SHARE</c> na tabela — modo com o qual
        /// <c>SHARE</c> é compatível. A migration passaria pelo lock, travaria no tuple que a
        /// requisição já detém, e a requisição travaria no lock da migration ao gravar: deadlock.
        /// <c>EXCLUSIVE</c> conflita com <c>ROW SHARE</c>, então a requisição espera antes de
        /// tomar o tuple, e o ciclo não se forma.
        /// </para>
        /// <para>
        /// <b>Por que num comando só.</b> Adquirir em dois passos abriria janela para outra
        /// transação pegá-los na ordem inversa. Um único <c>LOCK TABLE</c> fixa a ordem.
        /// </para>
        /// </remarks>
        internal static string SqlDoLockDaFronteira => """
            LOCK TABLE selecao.processos_seletivos, selecao.versoes_configuracao IN EXCLUSIVE MODE;
            """;

        /// <summary>Hash que esta migration passa a gravar para a convenção indicada.</summary>
        internal static string HashVigenteDe(string codigo) =>
            Array.Find(Convencoes, c => string.Equals(c.Codigo, codigo, StringComparison.Ordinal)).HashVigente;

        /// <summary>
        /// Fronteira append-only do <c>rol_de_regras</c> (ADR-0112): substituir a definição no
        /// lugar só é legítimo enquanto nenhuma versão de configuração congelada referenciar a
        /// entrada. A partir da primeira referência, a definição vira fato reproduzível.
        /// </summary>
        /// <remarks>
        /// A busca é estrutural — referência é a tripla <c>{codigo, versao, hash}</c> —, porque o
        /// snapshot serializa muitos objetos sob a chave bare <c>codigo</c> com valor declarado
        /// pelo administrador, e homônimo não é referência.
        /// </remarks>
        internal static string SqlDaGuardaDeConfiguracaoCongelada(string codigo) => $"""
            DO $adr0112$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM selecao.versoes_configuracao
                    WHERE configuracao_congelada @? '$.** ? (@.codigo == "{codigo}" && @.versao == "v1" && exists(@.hash))'
                ) THEN
                    RAISE EXCEPTION 'rol_de_regras: {codigo}/v1 referenciada por versão de configuração congelada; substituir a definição no lugar viola o append-only (ADR-0112) — evoluir exige versão sucessora';
                END IF;
            END
            $adr0112$;
            """;

        /// <summary>
        /// Reaponta a referência <b>viva</b> — a convenção que um processo já declarou e que o
        /// servidor congelou como <c>(código, versão, hash)</c> em coluna própria.
        /// </summary>
        /// <remarks>
        /// Essa população escapa da guarda acima: um rascunho que declarou a convenção não tem
        /// versão publicada. Como a substituição é no lugar e não há versão sucessora, manter o
        /// hash anterior deixaria o processo apontando para definição que o catálogo não tem.
        /// </remarks>
        internal static string SqlDoReaponteDaReferenciaViva(string codigo, string hashDestino) => $"""
            DO $viva$
            BEGIN
                UPDATE selecao.processos_seletivos
                SET algoritmo_contagem_prazo_hash = '{hashDestino}'
                WHERE algoritmo_contagem_prazo_codigo = '{codigo}'
                  AND algoritmo_contagem_prazo_versao = 'v1'
                  AND algoritmo_contagem_prazo_hash <> '{hashDestino}';
            END
            $viva$;
            """;

        private static void AplicarFronteira(MigrationBuilder migrationBuilder, bool avancando)
        {
            // Uma vez, antes de qualquer checagem: as duas tabelas ficam travadas até o fim da
            // transação da migration, e nenhuma escrita se intercala entre conferir e trocar.
            migrationBuilder.Sql(SqlDoLockDaFronteira);

            foreach ((string codigo, string vigente, string anterior) in Convencoes)
            {
                migrationBuilder.Sql(SqlDaGuardaDeConfiguracaoCongelada(codigo));
                migrationBuilder.Sql(SqlDoReaponteDaReferenciaViva(codigo, avancando ? vigente : anterior));
            }
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AplicarFronteira(migrationBuilder, avancando: true);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000020"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "fce95fc44b52a5a93a697b0309659a5af0085f9d39ceac1c3917c7b00b1c0be5" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000021"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "49b637293aaaa71449dcb971fc548c59fc840545d2d1740ac352f858a291105b" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000022"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "cd4c631492d02126c88a7ca5558992b3ed8a27c80692ecabfb73609293f2a9c8" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            AplicarFronteira(migrationBuilder, avancando: false);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000020"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada pelo edital; nenhuma citação aproximada substitui este texto.", "63ef57b6b32c023cdcb4ba9406d84f9e63694d4a9a8ee46bd9b532bbedd08b72" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000021"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada pelo edital; nenhuma citação aproximada substitui este texto.", "01bb4ae923690f2ae79373112069723569fc286ee3054bc1e190336256decd56" });

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000022"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "BASE LEGAL PENDENTE DE CONFIRMAÇÃO JURÍDICA — o dispositivo exato (lei, artigo e parágrafo) que sustenta o prazo de interposição e o efeito suspensivo do recurso administrativo ainda não foi confirmado (UNI-REQ-0095). A convenção de contagem desta entrada é escolha declarada pelo edital; nenhuma citação aproximada substitui este texto.", "73bf9e2448e2656e421243dff5f375c7fc9598eca71a5f291169efe01b777922" });
        }
    }
}
