using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AmpliaTipoBancaEFaseCanonicaBiopsicossocial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_banca_codigo_canonico",
                schema: "configuracao",
                table: "tipo_banca");

            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica");

            // INSERT tolerante em vez do InsertData gerado pelo EF, mesmo molde de
            // SemeiaFasesCanonicas: o cadastro é administrável, e mesmo não havendo
            // ambiente com este código hoje, uma base pode já ter uma fase viva
            // ocupando 'AVALIACAO_BIOPSICOSSOCIAL' criada pelo CRUD antes deste
            // deploy. ON CONFLICT DO NOTHING preserva o que o operador cadastrou em
            // vez de travar a migração no índice único parcial
            // ix_fase_canonica_codigo_vivo.
            migrationBuilder.Sql(
                """
                INSERT INTO configuracao.fase_canonica
                       (id, codigo, nome, descricao, dono_tipico, origem_data,
                        produz_resultado, resultado_definitivo, coleta_inscricao,
                        coleta_solicitacao_isencao, agrupa_etapas, permite_complementacao,
                        base_legal, created_at, is_deleted)
                VALUES
                       ('f45e0000-0000-7000-8000-000000000016', 'AVALIACAO_BIOPSICOSSOCIAL', 'Avaliação biopsicossocial', 'Avaliação multiprofissional e interdisciplinar que verifica se o candidato com deficiência atende aos requisitos legais para concorrer às vagas reservadas às pessoas com deficiência.', 'CEPS', 'PROPRIA', true, false, false, false, false, false, 'Lei nº 13.146/2015, art. 2º §1º e art. 30; Lei nº 12.711/2012 c/c Lei nº 13.409/2016', TIMESTAMPTZ '2026-01-01 00:00:00+00', false)
                ON CONFLICT DO NOTHING;

                -- Fase que o operador removeu deliberadamente não volta pelo deploy — mesma
                -- lógica de SemeiaFasesCanonicas: o ON CONFLICT acima não alcança esse caso
                -- porque o índice único é parcial (WHERE is_deleted = false), e a linha
                -- removida teria id próprio, que não colide com o do seed.
                DELETE FROM configuracao.fase_canonica novo
                 WHERE novo.id = 'f45e0000-0000-7000-8000-000000000016'
                   AND EXISTS (SELECT 1 FROM configuracao.fase_canonica antigo
                                WHERE antigo.codigo = novo.codigo
                                  AND antigo.is_deleted = true
                                  AND antigo.id <> novo.id);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_banca_codigo_canonico",
                schema: "configuracao",
                table: "tipo_banca",
                sql: "codigo IN ('BANCA_ANALISE_DOCUMENTAL', 'BANCA_ENTREVISTA', 'BANCA_CORRECAO_REDACOES', 'BANCA_ANALISE_RECURSOS', 'BANCA_HETEROIDENTIFICACAO', 'BANCA_BIOPSICOSSOCIAL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "antecessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "sucessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_banca_codigo_canonico",
                schema: "configuracao",
                table: "tipo_banca");

            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica");

            // Mesma regra de SemeiaFasesCanonicas: a migração não remove o que tem dono
            // (updated_by/updated_at não nulos — o operador editou a linha do seed) nem
            // o que algum cronograma de Seleção já referencia. Rollback de base assim
            // deixa a linha para trás — preferível a quebrar processo configurado.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    referenciada boolean;
                BEGIN
                    referenciada := false;
                    IF to_regclass('selecao.fases_cronograma') IS NOT NULL THEN
                        SELECT EXISTS (
                            SELECT 1 FROM selecao.fases_cronograma c
                             WHERE c.fase_canonica_origem_id = 'f45e0000-0000-7000-8000-000000000016'
                        ) INTO referenciada;
                    END IF;

                    IF NOT referenciada THEN
                        DELETE FROM configuracao.fase_canonica
                         WHERE id = 'f45e0000-0000-7000-8000-000000000016'
                           AND updated_at IS NULL
                           AND created_by IS NULL;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_banca_codigo_canonico",
                schema: "configuracao",
                table: "tipo_banca",
                sql: "codigo IN ('BANCA_ANALISE_DOCUMENTAL', 'BANCA_ENTREVISTA', 'BANCA_CORRECAO_REDACOES', 'BANCA_ANALISE_RECURSOS')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_antecessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "antecessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_precedencia_fase_sucessora_canonica",
                schema: "configuracao",
                table: "precedencia_fase",
                sql: "sucessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fase_canonica_codigo_canonico",
                schema: "configuracao",
                table: "fase_canonica",
                sql: "codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
        }
    }
}
