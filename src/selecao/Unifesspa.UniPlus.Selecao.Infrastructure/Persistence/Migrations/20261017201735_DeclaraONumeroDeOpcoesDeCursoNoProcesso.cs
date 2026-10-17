using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraONumeroDeOpcoesDeCursoNoProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O hash da regra de ordem de alocação muda porque o parâmetro n_opcoes saiu do esquema:
            // o número de opções agora é dimensão do processo. Substituição sob a mesma linha do
            // rol_de_regras (ADR-0112, Emenda 1): legítima só enquanto nenhuma versão de configuração
            // congelada citar a regra com o hash antigo. A busca é pela tripla {codigo, versao, hash}
            // da referência de regra, nunca por texto.
            //
            // A classificação de QUALQUER processo que cite o hash antigo é apagada — em rascunho,
            // publicado ou em retificação —, porque a referência deixaria de existir no catálogo.
            // Sem produção, a Emenda 1 autoriza descartar o dado; os processos de HML são recadastrados
            // e o operador define a classificação de novo. Sem backfill: o número vive numa coluna nova,
            // a ser declarada pelo operador.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA" && @.versao == "v1" && @.hash == "3c3b381dbe84380b1954fdd25512cd34e3881bddccf8e733530ca0593aa8e12f")'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA referenciada por versão de configuração congelada; substituir viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.Sql("""
                DELETE FROM selecao.configuracoes_classificacao
                WHERE regra_ordem_alocacao_codigo = 'ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA'
                  AND regra_ordem_alocacao_versao = 'v1'
                  AND regra_ordem_alocacao_hash = '3c3b381dbe84380b1954fdd25512cd34e3881bddccf8e733530ca0593aa8e12f';
                """);

            migrationBuilder.DropColumn(
                name: "n_opcoes_alocacao",
                schema: "selecao",
                table: "configuracoes_classificacao");

            migrationBuilder.AddColumn<int>(
                name: "numero_de_opcoes_de_curso",
                schema: "selecao",
                table: "processos_seletivos",
                type: "integer",
                nullable: true,
                comment: "Quantos cursos o candidato escolhe na inscrição (1 ou 2). Nulo = ainda não declarado.");

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000013"),
                columns: new[] { "esquema_args", "hash" },
                values: new object[] { "{}", "2b70a91223c2d03b6e89ce77ae88699dbfa3ed6c9c3e84fae88b8dc3b4c9444b" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_processos_seletivos_numero_de_opcoes_de_curso",
                schema: "selecao",
                table: "processos_seletivos",
                sql: "numero_de_opcoes_de_curso IS NULL OR numero_de_opcoes_de_curso IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A volta desfaz a substituição só enquanto nenhuma versão congelada citar a regra com o
            // hash novo (ADR-0112); a classificação que o cita é descartada, como na ida.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? (@.codigo == "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA" && @.versao == "v1" && @.hash == "2b70a91223c2d03b6e89ce77ae88699dbfa3ed6c9c3e84fae88b8dc3b4c9444b")'
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA referenciada por versão de configuração congelada; desfazer a substituição viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;

                DELETE FROM selecao.configuracoes_classificacao
                WHERE regra_ordem_alocacao_codigo = 'ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA'
                  AND regra_ordem_alocacao_versao = 'v1'
                  AND regra_ordem_alocacao_hash = '2b70a91223c2d03b6e89ce77ae88699dbfa3ed6c9c3e84fae88b8dc3b4c9444b';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_processos_seletivos_numero_de_opcoes_de_curso",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "numero_de_opcoes_de_curso",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.AddColumn<int>(
                name: "n_opcoes_alocacao",
                schema: "selecao",
                table: "configuracoes_classificacao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000013"),
                columns: new[] { "esquema_args", "hash" },
                values: new object[] { "{\"n_opcoes\":\"integer\"}", "3c3b381dbe84380b1954fdd25512cd34e3881bddccf8e733530ca0593aa8e12f" });
        }
    }
}
