-- A posição de uma banca entre as do mesmo tipo é única na fase e na etapa: o código do tipo e a posição ordenam as bancas, e
-- nenhuma outra coluna desempata. Reordenar as bancas troca posições entre linhas que já existem, na mesma transação. Um índice
-- único comum confere cada comando e recusaria a troca, mesmo com o estado final válido. A restrição adiada confere no commit, e
-- só a restrição pode ser adiada, não o índice; o modelo do EF não a expressa. As tabelas não têm exclusão lógica, então não há
-- linha excluída a ignorar.
ALTER TABLE selecao.bancas_da_etapa
    DROP CONSTRAINT IF EXISTS uq_bancas_da_etapa_posicao;

ALTER TABLE selecao.bancas_da_etapa
    ADD CONSTRAINT uq_bancas_da_etapa_posicao
    UNIQUE (etapa_processo_id, codigo, posicao_no_tipo)
    DEFERRABLE INITIALLY DEFERRED;

ALTER TABLE selecao.bancas_requeridas
    DROP CONSTRAINT IF EXISTS uq_bancas_requeridas_posicao;

ALTER TABLE selecao.bancas_requeridas
    ADD CONSTRAINT uq_bancas_requeridas_posicao
    UNIQUE (fase_cronograma_id, codigo, posicao_no_tipo)
    DEFERRABLE INITIALLY DEFERRED;
