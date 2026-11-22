-- A restrição é adiada para permitir substituir as regras na mesma transação.
ALTER TABLE selecao.regras_de_comprovacao
    DROP CONSTRAINT IF EXISTS ex_regras_comprovacao_itens_ordem;

ALTER TABLE selecao.regras_de_comprovacao
    ADD CONSTRAINT ex_regras_comprovacao_itens_ordem
    EXCLUDE USING gist (regra_que_contem_id WITH =, ordem WITH =) WHERE (regra_que_contem_id IS NOT NULL)
    DEFERRABLE INITIALLY DEFERRED;

ALTER TABLE selecao.regras_de_comprovacao
    DROP CONSTRAINT IF EXISTS ex_regras_comprovacao_sem_grupo_ordem;

ALTER TABLE selecao.regras_de_comprovacao
    ADD CONSTRAINT ex_regras_comprovacao_sem_grupo_ordem
    EXCLUDE USING gist (processo_seletivo_id WITH =, ordem WITH =) WHERE (regra_que_contem_id IS NULL)
    DEFERRABLE INITIALLY DEFERRED;
