using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraAFormaDeAvaliacaoDaEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "forma_de_avaliacao",
                schema: "selecao",
                table: "etapas_processo",
                type: "integer",
                nullable: true,
                comment: "Como a nota da etapa é obtida (1 objetiva, 2 humana); ausente em rascunho, na etapa sem nota e na do ENEM.");

            migrationBuilder.AddColumn<string>(
                name: "regra_consolidacao_codigo",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regra_consolidacao_hash",
                schema: "selecao",
                table: "etapas_processo",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regra_consolidacao_versao",
                schema: "selecao",
                table: "etapas_processo",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[,]
                {
                    { new Guid("d0a00000-0000-7000-8000-000000000030"), "Decisão institucional da Unifesspa", "CONSOLIDACAO-AVALIACAO-UNICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "0fdac8b1bc6a5f9f7e4d2f257d695e5028210d6c8cb16876157755e36c6b05b0", "[\"um só avaliador lança a nota de cada candidato na etapa\",\"a nota lançada é a nota da etapa, sem combinação com outra\"]", "regra_consolidacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000031"), "UNI-REQ-0097 (dupla correção) e edital do processo seletivo", "CONSOLIDACAO-CORRECAO-INDEPENDENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "376e2e3ace2ba3302fb7d2a957e390e4eeeceb2bdc0102d65db688dd744a67c3", "[\"mais de um avaliador corrige cada candidato na etapa, cada um sem conhecer a nota do outro\",\"a nota da etapa é a combinação das correções independentes, na forma que o edital determina\"]", "regra_consolidacao", null, "v1" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_etapas_processo_forma_de_avaliacao_valida",
                schema: "selecao",
                table: "etapas_processo",
                sql: "forma_de_avaliacao IS NULL OR forma_de_avaliacao IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fronteira append-only do rol_de_regras (ADR-0112): uma entrada referenciada por
            // configuração congelada é fato imutável, e a reversão só é legítima enquanto nenhuma
            // VersaoConfiguracao citar as regras de consolidação que este Down remove. A guarda
            // também cobre o rascunho vivo em etapas_processo: a referência é cópia por valor sem
            // FK (ADR-0061), e a publicação serializa o rascunho sem reconsultar o catálogo.
            migrationBuilder.Sql("""
                DO $adr0112$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM selecao.versoes_configuracao
                        WHERE configuracao_congelada @? '$.** ? ((@.codigo == "CONSOLIDACAO-AVALIACAO-UNICA" || @.codigo == "CONSOLIDACAO-CORRECAO-INDEPENDENTE") && @.versao == "v1" && exists(@.hash))'
                    ) OR EXISTS (
                        SELECT 1
                        FROM selecao.etapas_processo
                        WHERE regra_consolidacao_codigo IN ('CONSOLIDACAO-AVALIACAO-UNICA', 'CONSOLIDACAO-CORRECAO-INDEPENDENTE')
                    ) THEN
                        RAISE EXCEPTION 'rol_de_regras: regra de consolidação referenciada por versão de configuração congelada ou por etapa em rascunho; remover viola o append-only (ADR-0112)';
                    END IF;
                END
                $adr0112$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_etapas_processo_forma_de_avaliacao_valida",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000030"));

            migrationBuilder.DeleteData(
                schema: "selecao",
                table: "rol_de_regras",
                keyColumn: "id",
                keyValue: new Guid("d0a00000-0000-7000-8000-000000000031"));

            migrationBuilder.DropColumn(
                name: "forma_de_avaliacao",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "regra_consolidacao_codigo",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "regra_consolidacao_hash",
                schema: "selecao",
                table: "etapas_processo");

            migrationBuilder.DropColumn(
                name: "regra_consolidacao_versao",
                schema: "selecao",
                table: "etapas_processo");
        }
    }
}
