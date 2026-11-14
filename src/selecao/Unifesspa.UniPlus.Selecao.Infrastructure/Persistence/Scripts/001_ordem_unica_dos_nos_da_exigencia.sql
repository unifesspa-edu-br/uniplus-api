-- A ordem dos nós irmãos da árvore de exigências é única, e a de cada raiz também. O nó é autorreferenciado
-- (no_pai_id → id): substituir uma árvore apaga os nós de baixo para cima, mas o EF insere a raiz nova antes de a antiga
-- sair do banco, na mesma transação. Um índice único comum confere cada comando e recusaria a raiz nova com a ordem da
-- antiga, mesmo com o estado final válido. A exclusão adiada confere no commit. O EXCLUDE com predicado parcial não é
-- expressável pelo modelo do EF.
ALTER TABLE selecao.nos_exigencia
    DROP CONSTRAINT IF EXISTS ex_nos_exigencia_irmaos_ordem;

ALTER TABLE selecao.nos_exigencia
    ADD CONSTRAINT ex_nos_exigencia_irmaos_ordem
    EXCLUDE USING gist (no_pai_id WITH =, ordem WITH =) WHERE (no_pai_id IS NOT NULL)
    DEFERRABLE INITIALLY DEFERRED;

ALTER TABLE selecao.nos_exigencia
    DROP CONSTRAINT IF EXISTS ex_nos_exigencia_raiz_ordem;

ALTER TABLE selecao.nos_exigencia
    ADD CONSTRAINT ex_nos_exigencia_raiz_ordem
    EXCLUDE USING gist (processo_seletivo_id WITH =, ordem WITH =) WHERE (no_pai_id IS NULL)
    DEFERRABLE INITIALLY DEFERRED;
