using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeBaseLegalCascataLei12711 : Migration
    {
        /// <summary>Hash da definição que esta migration passa a valer.</summary>
        private const string HashDaDefinicaoVigente =
            "70f96f9fa5b2c5282adba8028070fdf0a82bef99408693d17d913c51d0c5078a";

        /// <summary>Hash da definição anterior, para o qual a reversão devolve as referências vivas.</summary>
        private const string HashDaDefinicaoAnterior =
            "8e5cfbbf06b2a661b024ae1efc7db8f5013b434b316f6b69c3be9a3c90999626";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ExigirQueNenhumaConfiguracaoCongeladaReferencie(migrationBuilder);
            ReapontarHashDasCascatasVivas(migrationBuilder, HashDaDefinicaoVigente);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000019"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 2º e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 21/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", HashDaDefinicaoVigente });
        }

        /// <summary>
        /// Fronteira append-only do <c>rol_de_regras</c> (ADR-0112): corrigir a base legal no
        /// lugar só é legítimo enquanto nenhuma versão de configuração congelada referenciar a
        /// entrada. A partir da primeira referência, a definição vira fato reproduzível, e
        /// evoluir passa a exigir versão sucessora.
        /// </summary>
        /// <remarks>
        /// A busca é estrutural — referência de regra é a tripla <c>{codigo, versao, hash}</c>
        /// —, porque o snapshot serializa muitos outros objetos sob a chave bare <c>codigo</c>
        /// com valor declarado pelo administrador, e homônimo não é referência. A guarda é o
        /// que torna a escolha segura em vez de presumida, e faz o deploy abortar em vez de
        /// reescrever a definição sob um edital que a cita.
        /// </remarks>
        private static void ExigirQueNenhumaConfiguracaoCongeladaReferencie(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "REMANEJ-CASCATA-LEI-12711" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: REMANEJ-CASCATA-LEI-12711/v1 referenciada por versão de configuração congelada; corrigir a base legal no lugar viola o append-only (ADR-0112) — evoluir exige versão sucessora';
                    END IF;
                END
                $adr0112$;
                """);
        }

        /// <summary>
        /// As configurações de cascata <b>vivas</b> — as de rascunho, que ainda não foram
        /// congeladas em versão nenhuma e por isso escapam da guarda acima — guardam o hash da
        /// definição referenciada em coluna própria (<c>ConfiguracaoCascataRemanejamento.Regra</c>).
        /// Como a correção é no lugar e não há versão sucessora, o hash antigo passaria a não
        /// descrever definição nenhuma do catálogo: a referência acompanha a correção.
        /// </summary>
        private static void ReapontarHashDasCascatasVivas(
            MigrationBuilder migrationBuilder,
            string hashDestino) =>
            migrationBuilder.Sql($"""
                UPDATE selecao.configuracoes_cascata_remanejamento
                SET regra_hash = '{hashDestino}'
                WHERE regra_codigo = 'REMANEJ-CASCATA-LEI-12711'
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
            ReapontarHashDasCascatasVivas(migrationBuilder, HashDaDefinicaoAnterior);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000019"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "ADR-0120; Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 20-A e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 18/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", HashDaDefinicaoAnterior });
        }
    }
}
