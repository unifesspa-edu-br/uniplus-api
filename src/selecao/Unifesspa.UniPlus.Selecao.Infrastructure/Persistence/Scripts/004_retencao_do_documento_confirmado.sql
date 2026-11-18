-- O documento confirmado é a evidência do que foi publicado. Só o confirmado (status = 1) é retido: o pendente é um envio
-- que nunca se completou, e a limpeza dos vencidos continua possível. O RAISE declara CONSTRAINT/TABLE/SCHEMA para que
-- PostgresException.ConstraintName chegue preenchida à aplicação (ADR-0102).
--
-- Desconfirmar é proibido sempre. A remoção, não: a retenção vale para o documento de um processo que já existe, e o
-- documento de um rascunho excluído sai junto com ele. A existência do processo é a da linha, sem olhar a exclusão
-- lógica: o processo excluído logicamente continua existindo e seus documentos continuam retidos.
CREATE OR REPLACE FUNCTION selecao.fn_documentos_a_publicar_retencao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'Documento confirmado é a evidência do que foi publicado: não se remove nem se desconfirma.'
        USING ERRCODE = 'check_violation',
              CONSTRAINT = 'ck_documentos_a_publicar_confirmado_retido',
              TABLE = 'documentos_a_publicar',
              SCHEMA = 'selecao';
END;
$$;

CREATE OR REPLACE FUNCTION selecao.fn_documentos_a_publicar_retencao_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM selecao.processos_seletivos WHERE id = OLD.processo_seletivo_id) THEN
        RAISE EXCEPTION
            'Documento confirmado é a evidência do que foi publicado: não se remove nem se desconfirma.'
            USING ERRCODE = 'check_violation',
                  CONSTRAINT = 'ck_documentos_a_publicar_confirmado_retido',
                  TABLE = 'documentos_a_publicar',
                  SCHEMA = 'selecao';
    END IF;

    RETURN OLD;
END;
$$;

DROP TRIGGER IF EXISTS trg_documentos_a_publicar_retencao_delete ON selecao.documentos_a_publicar;
CREATE TRIGGER trg_documentos_a_publicar_retencao_delete
    BEFORE DELETE ON selecao.documentos_a_publicar
    FOR EACH ROW
    WHEN (OLD.status = 1)
    EXECUTE FUNCTION selecao.fn_documentos_a_publicar_retencao_delete();

DROP TRIGGER IF EXISTS trg_documentos_a_publicar_retencao_update ON selecao.documentos_a_publicar;
CREATE TRIGGER trg_documentos_a_publicar_retencao_update
    BEFORE UPDATE ON selecao.documentos_a_publicar
    FOR EACH ROW
    WHEN (OLD.status = 1 AND NEW.status <> 1)
    EXECUTE FUNCTION selecao.fn_documentos_a_publicar_retencao();

-- TRUNCATE não dispara gatilho de linha.
CREATE OR REPLACE FUNCTION selecao.fn_documentos_a_publicar_bloqueia_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'TRUNCATE apagaria os documentos confirmados, que são evidência do publicado.'
        USING ERRCODE = 'check_violation',
              CONSTRAINT = 'ck_documentos_a_publicar_confirmado_retido',
              TABLE = 'documentos_a_publicar',
              SCHEMA = 'selecao';
END;
$$;

DROP TRIGGER IF EXISTS trg_documentos_a_publicar_bloqueia_truncate ON selecao.documentos_a_publicar;
CREATE TRIGGER trg_documentos_a_publicar_bloqueia_truncate
    BEFORE TRUNCATE ON selecao.documentos_a_publicar
    FOR EACH STATEMENT
    EXECUTE FUNCTION selecao.fn_documentos_a_publicar_bloqueia_truncate();
