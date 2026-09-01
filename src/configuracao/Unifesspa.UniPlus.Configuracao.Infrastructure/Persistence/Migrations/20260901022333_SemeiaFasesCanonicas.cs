using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SemeiaFasesCanonicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // INSERT tolerante em vez do InsertData gerado pelo EF: o cadastro é
            // administrável e existe desde antes desta carga, então um ambiente pode já
            // ter uma fase viva ocupando um destes códigos. O índice único parcial
            // ix_fase_canonica_codigo_vivo rejeitaria a linha do seed, e a falha travaria
            // a migração inteira — e, com ela, o deploy. Pular a linha em conflito
            // preserva o que o operador cadastrou e completa o vocabulário em toda base
            // que ainda não o tinha. Mesmo tratamento de SemeiaCatalogoTiposDocumento.
            //
            // Não há UPDATE aqui, e não pode haver: alterar linha existente é ato
            // administrativo, com autor registrado, não efeito de deploy (ADR-0062,
            // Emenda 2). Um ambiente cuja fase esteja com atributo errado se corrige
            // pela tela, não pela migração.
            //
            // created_by fica nulo: ninguém criou estas linhas: elas decorrem do ciclo
            // do certame. É a marca honesta do dado normativo, e não a fabricação de
            // autoria que a decisão original da ADR-0062 recusou.
            migrationBuilder.Sql(
                """
                INSERT INTO configuracao.fase_canonica
                       (id, codigo, nome, descricao, dono_tipico, origem_data,
                        produz_resultado, resultado_definitivo, coleta_inscricao,
                        agrupa_etapas, permite_complementacao, base_legal,
                        created_at, is_deleted)
                VALUES
                       ('f45e0000-0000-7000-8000-000000000001', 'INSCRICAO', 'Inscrição', 'Período em que o candidato se inscreve no processo seletivo.', 'CEPS', 'PROPRIA', false, false, true, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000002', 'SOLICITACAO_ISENCAO', 'Solicitação de isenção', 'Janela em que o candidato pede isenção da taxa de inscrição. Abre junto com as inscrições e termina antes delas.', 'CEPS', 'PROPRIA', true, false, false, false, false, 'Lei nº 12.799/2013', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000003', 'HOMOLOGACAO', 'Homologação das inscrições', 'Conferência das inscrições recebidas e publicação de quais foram homologadas.', 'CEPS', 'PROPRIA', true, false, false, false, true, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000004', 'ENSALAMENTO', 'Ensalamento', 'Distribuição dos candidatos pelos locais de prova.', 'CEPS', 'PROPRIA', false, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000005', 'AVALIACAO', 'Avaliação', 'Fase que agrupa as etapas pontuadas do certame.', 'CEPS', 'PROPRIA', false, false, false, true, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000006', 'CLASSIFICACAO', 'Classificação', 'Apuração das notas e ordenação dos candidatos por modalidade.', 'CEPS', 'PROPRIA', false, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000007', 'RESULTADO_PRELIMINAR', 'Resultado preliminar', 'Publicação do resultado que ainda admite recurso.', 'CEPS', 'PROPRIA', true, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000008', 'RECURSOS', 'Recursos', 'Interposição e análise dos recursos contra o resultado preliminar.', 'CEPS', 'PROPRIA', false, false, false, false, true, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000009', 'RESULTADO_FINAL', 'Resultado final', 'Publicação do resultado depois de julgados os recursos.', 'CEPS', 'PROPRIA', true, true, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000010', 'HETEROIDENTIFICACAO', 'Heteroidentificação', 'Procedimento de heteroidentificação étnico-racial dos candidatos que concorrem por cota.', 'CEPS', 'PROPRIA', true, false, false, false, false, 'Lei nº 12.711/2012; Portaria Normativa MEC nº 4/2018', TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000011', 'HABILITACAO', 'Habilitação', 'Comprovação documental dos requisitos declarados pelo candidato.', 'CRCA', 'PROPRIA', true, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000012', 'HOMOLOGACAO_RESULTADO_FINAL', 'Homologação do resultado final', 'Ato do conselho que homologa o resultado final do processo seletivo.', 'CONSEPE', 'PROPRIA', true, true, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000013', 'MATRICULA', 'Matrícula', 'Efetivação do vínculo do candidato aprovado com a instituição.', 'CRCA', 'PROPRIA', false, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000014', 'LISTA_ESPERA', 'Lista de espera', 'Fila de candidatos classificados além das vagas, convocados conforme as vagas são liberadas.', 'MEC', 'DELEGADA', false, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false),
                       ('f45e0000-0000-7000-8000-000000000015', 'CHAMADA', 'Chamada', 'Convocação dos candidatos da lista de espera para ocupar vagas remanescentes.', 'MEC', 'DELEGADA', true, false, false, false, false, NULL, TIMESTAMPTZ '2026-01-01 00:00:00+00', false)
                ON CONFLICT DO NOTHING;

                -- Fase que o operador removeu deliberadamente não volta pelo deploy. O
                -- ON CONFLICT acima não a alcança: o índice único é parcial
                -- (WHERE is_deleted = false), e a linha removida tem id próprio, que não
                -- colide com o do seed. Sem esta limpeza, o código passaria a ter duas
                -- linhas — a removida e a recém-semeada —, ressuscitando na prática o que
                -- alguém tirou de circulação.
                DELETE FROM configuracao.fase_canonica novo
                 WHERE novo.id::text LIKE 'f45e0000-0000-7000-8000-%'
                   AND EXISTS (SELECT 1 FROM configuracao.fase_canonica antigo
                                WHERE antigo.codigo = novo.codigo
                                  AND antigo.is_deleted = true
                                  AND antigo.id <> novo.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Apaga só o que o seed criou, ninguém tocou e nenhum processo usa.
            //
            // O prefixo do id exclui a linha que o operador cadastrou por conta própria; as
            // duas colunas de auditoria excluem a linha semeada que ele depois editou — ela
            // mantém o id determinístico, então o prefixo sozinho a levaria junto, apagando
            // a edição e a trilha sem o soft-delete que o cadastro usa.
            //
            // A terceira condição trata o consumo: o cronograma de um processo guarda
            // fase_canonica_origem_id e resolve a fase por ele a cada gravação — não há
            // chave estrangeira entre os módulos para recusar o apagamento, e a fase
            // ausente faria a próxima gravação falhar com FaseCanonicaNaoEncontrada num
            // cronograma que já estava correto. Diferente do que ocorre onde o consumidor
            // congela por valor, aqui a referência é viva.
            //
            // É a mesma regra do Up, na direção inversa: a migration não altera nem remove
            // o que tem dono ou o que alguém usa. Rollback de base assim deixa a linha para
            // trás — preferível a quebrar processo configurado.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    referenciadas text := '';
                BEGIN
                    -- A referencia a selecao.fases_cronograma vai por SQL dinamico porque o
                    -- Postgres resolve nomes de tabela no parse: um NOT EXISTS estatico
                    -- abortaria o rollback com "relation does not exist" num banco que so
                    -- tenha o schema de Configuracao, ou onde Selecao ja tenha sido removido.
                    IF to_regclass('selecao.fases_cronograma') IS NOT NULL THEN
                        referenciadas := ' AND NOT EXISTS (SELECT 1 FROM selecao.fases_cronograma c'
                                      || ' WHERE c.fase_canonica_origem_id = f.id)';
                    END IF;

                    EXECUTE 'DELETE FROM configuracao.fase_canonica f'
                         || ' WHERE f.id::text LIKE ''f45e0000-0000-7000-8000-%'''
                         || ' AND f.updated_at IS NULL'
                         || ' AND f.created_by IS NULL'
                         || referenciadas;
                END
                $$;
                """);
        }
    }
}
