-- No máximo um calendário vivo é o vigente. A troca grava a desmarca do anterior e a marca do novo na mesma transação, em
-- ordem que o EF não controla: um índice único comum conferiria cada gravação e recusaria a ordem inversa, mesmo com o
-- estado final válido. A exclusão adiada confere no commit. O EXCLUDE com predicado parcial não é expressável pelo modelo
-- do EF.
ALTER TABLE configuracao.calendario_dias_uteis
    DROP CONSTRAINT IF EXISTS ex_calendario_dias_uteis_vigente_unico;

ALTER TABLE configuracao.calendario_dias_uteis
    ADD CONSTRAINT ex_calendario_dias_uteis_vigente_unico
    EXCLUDE USING gist (vigente WITH =) WHERE (vigente = true AND is_deleted = false)
    DEFERRABLE INITIALLY DEFERRED;
