-- Reordenar itens e níveis, ou trocar rótulos de níveis, altera linhas existentes na mesma
-- transação. As restrições adiadas conferem a unicidade no estado final da transação.
ALTER TABLE selecao.itens_de_avaliacao_da_etapa
    DROP CONSTRAINT IF EXISTS uq_itens_de_avaliacao_da_etapa_ordem;
ALTER TABLE selecao.itens_de_avaliacao_da_etapa
    ADD CONSTRAINT uq_itens_de_avaliacao_da_etapa_ordem
    UNIQUE (etapa_processo_id, ordem) DEFERRABLE INITIALLY DEFERRED;

ALTER TABLE selecao.niveis_de_atendimento_da_etapa
    DROP CONSTRAINT IF EXISTS uq_niveis_atendimento_etapa_ordem;
ALTER TABLE selecao.niveis_de_atendimento_da_etapa
    ADD CONSTRAINT uq_niveis_atendimento_etapa_ordem
    UNIQUE (etapa_processo_id, ordem) DEFERRABLE INITIALLY DEFERRED;

ALTER TABLE selecao.niveis_de_atendimento_da_etapa
    DROP CONSTRAINT IF EXISTS uq_niveis_atendimento_etapa_rotulo;
ALTER TABLE selecao.niveis_de_atendimento_da_etapa
    ADD CONSTRAINT uq_niveis_atendimento_etapa_rotulo
    UNIQUE (etapa_processo_id, rotulo) DEFERRABLE INITIALLY DEFERRED;
