-- A lista de anexos fecha com o ato: só se insere anexo na mesma transação que inseriu o ato. Sem isto, um INSERT em
-- transação posterior acrescentaria um arquivo a um ato já publicado, mudando o que o registro prova.
--
-- Comparar o xmin do ato com pg_current_xact_id() não serve: o EF cria savepoints quando a transação é externa, e a
-- linha inserida dentro de um savepoint tem como xmin o xid da subtransação, não o da transação de nível superior.
-- Em vez disso, o ato é visível a esta sessão; se a transação que o inseriu ainda está em andamento, só pode ser a
-- própria (a de outra sessão não é visível), subtransações incluídas.
--
-- O xmin tem 32 bits e o xid8 que pg_xact_status exige tem 64: falta a época. Uma transação em andamento é recente,
-- então a época é a da transação corrente. Se o xmin reconstruído com ela cai no futuro (o contador de 32 bits deu a
-- volta), pg_xact_status lança 22023 em vez de responder; esse xid só pode ser de uma transação antiga e encerrada,
-- logo não é a corrente, e a exceção é tratada como "não está em andamento" para o gatilho recusar com o SQLSTATE
-- próprio.
CREATE OR REPLACE FUNCTION publicacoes.fn_xid_em_andamento(p_xmin xid)
RETURNS boolean
LANGUAGE plpgsql
AS $$
DECLARE
    epoca bigint := (pg_current_xact_id()::text::bigint >> 32) << 32;
BEGIN
    RETURN pg_xact_status((epoca | p_xmin::text::bigint)::text::xid8) = 'in progress';
EXCEPTION
    WHEN invalid_parameter_value THEN
        RETURN false;
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_anexo_do_ato_na_transacao_do_ato()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM publicacoes.ato_normativo
        WHERE id = NEW.ato_id AND publicacoes.fn_xid_em_andamento(xmin)) THEN
        RAISE EXCEPTION
            'anexo_do_ato: o anexo só entra na transação que registrou o ato % (ADR-0063); o ato publicado não ganha anexo depois.', NEW.ato_id
            USING ERRCODE = 'restrict_violation';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_anexo_do_ato_na_transacao_do_ato ON publicacoes.anexo_do_ato;
CREATE TRIGGER trg_anexo_do_ato_na_transacao_do_ato
    BEFORE INSERT ON publicacoes.anexo_do_ato
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_anexo_do_ato_na_transacao_do_ato();
