using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenomeiaAlocacaoParaPrimeiraOpcaoPrioritaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Substituição sob a mesma linha do rol_de_regras (ADR-0112, Emenda 1): legítima só
            // enquanto nenhuma versão de configuração congelada citar ALOCACAO-OPCOES-RN04. A busca
            // é pela tripla {codigo, versao, hash} da referência de regra, nunca por texto.
            //
            // A classificação em rascunho que cita a regra antiga é descartada: a referência deixaria
            // de existir no catálogo. Sem produção, a Emenda 1 autoriza descartar o dado de rascunho;
            // o operador define a classificação de novo.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ALOCACAO-OPCOES-RN04" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: ALOCACAO-OPCOES-RN04 referenciada por versão de configuração congelada; substituir viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;

                DELETE FROM selecao.configuracoes_classificacao
                WHERE regra_ordem_alocacao_codigo = 'ALOCACAO-OPCOES-RN04' AND regra_ordem_alocacao_versao = 'v1';
                """);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000013"),
                columns: new[] { "base_legal", "codigo", "hash", "invariantes" },
                values: new object[] { "UNI-REQ-0045 — processamento da 1ª opção antes da 2ª", "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA", "3c3b381dbe84380b1954fdd25512cd34e3881bddccf8e733530ca0593aa8e12f", "[\"processam-se todas as 1ªs opções de cada curso\",\"a vaga de modalidade não preenchida é remanejada dentro do curso, entre candidatos de 1ª opção\",\"só a vaga que sobra depois do remanejamento vai à 2ª opção, em ordem de nota, sem deslocar aprovado de 1ª opção\",\"quem não é classificado em nenhuma opção vai para a lista de espera\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A volta desfaz a substituição só enquanto nenhuma versão congelada citar
            // ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA; a classificação em rascunho que a cita é descartada,
            // como na ida, porque a referência deixaria de existir no catálogo.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA" && @.versao == "v1" && exists(@.hash))'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA referenciada por versão de configuração congelada; desfazer a substituição viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;

                DELETE FROM selecao.configuracoes_classificacao
                WHERE regra_ordem_alocacao_codigo = 'ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA' AND regra_ordem_alocacao_versao = 'v1';
                """);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000013"),
                columns: new[] { "base_legal", "codigo", "hash", "invariantes" },
                values: new object[] { "RN04 (processamento de 1ª/2ª opção)", "ALOCACAO-OPCOES-RN04", "2bb69f0e34483e635aa0903f8d3ba19a4255e8f542c5f7090ac75cecf200c988", "[\"1ª opção → 2ª opção → remanejamento → lista de espera\"]" });
        }
    }
}
