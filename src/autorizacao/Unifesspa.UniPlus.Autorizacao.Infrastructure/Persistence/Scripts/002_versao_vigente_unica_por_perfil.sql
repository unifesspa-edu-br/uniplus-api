-- A troca de vigência grava o encerramento da anterior e a entrada da nova na mesma transação, em ordem de chave que o
-- EF não controla. A exclusão adiada confere uma só vigente por perfil no commit.
ALTER TABLE autorizacao.versoes_do_perfil
    DROP CONSTRAINT IF EXISTS ex_versoes_do_perfil_vigente_unica;

ALTER TABLE autorizacao.versoes_do_perfil
    ADD CONSTRAINT ex_versoes_do_perfil_vigente_unica
    EXCLUDE USING btree (perfil_id WITH =) WHERE (situacao = 'vigente')
    DEFERRABLE INITIALLY DEFERRED;
