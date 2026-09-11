using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeBaseLegalBonusMultiplicativo : Migration
    {
        /// <summary>Hash da definição que esta migration passa a valer.</summary>
        private const string HashDaDefinicaoVigente =
            "76a46eef115f27a52873a21eeee988a7d6978993ae535c026fa2c2d250d3acd1";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ExigirQueNenhumaConfiguracaoCongeladaReferencie(migrationBuilder);
            AtualizarHashDosRascunhosMutaveis(migrationBuilder);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000008"),
                columns: new[] { "base_legal", "hash" },
                values: new object[] { "Bônus multiplicativo aplicado à nota final após os pesos das áreas do ENEM, sem teto por padrão", HashDaDefinicaoVigente });
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
        /// reescrever a definição sob um processo que a cita.
        /// </remarks>
        private static void ExigirQueNenhumaConfiguracaoCongeladaReferencie(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "BONUS-MULTIPLICATIVO" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: BONUS-MULTIPLICATIVO/v1 referenciada por versão de configuração congelada; corrigir a base legal no lugar viola o append-only (ADR-0112) — evoluir exige versão sucessora';
                    END IF;
                END
                $adr0112$;
                """);
        }

        /// <summary>
        /// Um rascunho ainda não publicado congela a referência <c>(codigo, versao, hash)</c> em
        /// <c>configuracoes_bonus_regional.regra_hash</c> assim que o bônus é definido — antes de
        /// qualquer <c>VersaoConfiguracao</c> existir. A guarda acima não vê esse caso porque só
        /// olha configuração congelada; sem este passo, publicar o rascunho depois desta migration
        /// serializaria um hash que não bate mais com <c>rol_de_regras</c>.
        /// </summary>
        private static void AtualizarHashDosRascunhosMutaveis(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE selecao.configuracoes_bonus_regional
                SET regra_hash = '{HashDaDefinicaoVigente}'
                WHERE regra_codigo = 'BONUS-MULTIPLICATIVO' AND regra_versao = 'v1';
                """);
        }

        /// <summary>
        /// Sem <c>Down</c>: o texto substituído era informal e não deve voltar a existir no
        /// código sob nenhuma circunstância, nem para reversão local.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
        }
    }
}
