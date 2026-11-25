-- O documento a publicar e o modelo de documento confirmados preservam a evidência do que foi publicado.
-- Pendentes podem ser removidos. Um confirmado pode ser removido quando não existe linha de processo,
-- como na exclusão de um rascunho; a exclusão lógica mantém a linha e a retenção. Desconfirmar e
-- mudar o processo de um confirmado e truncar são sempre proibidos, pois permitiriam apagar a evidência.
CREATE OR REPLACE FUNCTION selecao.fn_retencao_do_documento_confirmado()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF NOT EXISTS (SELECT 1 FROM selecao.processos_seletivos WHERE id = OLD.processo_seletivo_id) THEN
            RETURN OLD;
        END IF;
    END IF;

    RAISE EXCEPTION
        'Documento confirmado é evidência do que foi publicado: não se remove, não se desconfirma nem muda de processo.'
        USING ERRCODE = 'check_violation',
              CONSTRAINT = TG_ARGV[0],
              TABLE = TG_TABLE_NAME,
              SCHEMA = TG_TABLE_SCHEMA;
END;
$$;

DROP TRIGGER IF EXISTS trg_documentos_a_publicar_retencao_delete ON selecao.documentos_a_publicar;
CREATE TRIGGER trg_documentos_a_publicar_retencao_delete
    BEFORE DELETE ON selecao.documentos_a_publicar
    FOR EACH ROW
    WHEN (OLD.situacao = 'CONFIRMADO')
    EXECUTE FUNCTION selecao.fn_retencao_do_documento_confirmado('ck_documentos_a_publicar_confirmado_retido');

DROP TRIGGER IF EXISTS trg_documentos_a_publicar_retencao_update ON selecao.documentos_a_publicar;
CREATE TRIGGER trg_documentos_a_publicar_retencao_update
    BEFORE UPDATE ON selecao.documentos_a_publicar
    FOR EACH ROW
    WHEN (OLD.situacao = 'CONFIRMADO' AND (NEW.situacao IS DISTINCT FROM 'CONFIRMADO' OR NEW.processo_seletivo_id IS DISTINCT FROM OLD.processo_seletivo_id))
    EXECUTE FUNCTION selecao.fn_retencao_do_documento_confirmado('ck_documentos_a_publicar_confirmado_retido');

DROP TRIGGER IF EXISTS trg_documentos_a_publicar_bloqueia_truncate ON selecao.documentos_a_publicar;
CREATE TRIGGER trg_documentos_a_publicar_bloqueia_truncate
    BEFORE TRUNCATE ON selecao.documentos_a_publicar
    FOR EACH STATEMENT
    EXECUTE FUNCTION selecao.fn_retencao_do_documento_confirmado('ck_documentos_a_publicar_confirmado_retido');

DROP TRIGGER IF EXISTS trg_modelos_de_documento_retencao_delete ON selecao.modelos_de_documento;
CREATE TRIGGER trg_modelos_de_documento_retencao_delete
    BEFORE DELETE ON selecao.modelos_de_documento
    FOR EACH ROW
    WHEN (OLD.situacao = 'CONFIRMADO')
    EXECUTE FUNCTION selecao.fn_retencao_do_documento_confirmado('ck_modelos_de_documento_confirmado_retido');

DROP TRIGGER IF EXISTS trg_modelos_de_documento_retencao_update ON selecao.modelos_de_documento;
CREATE TRIGGER trg_modelos_de_documento_retencao_update
    BEFORE UPDATE ON selecao.modelos_de_documento
    FOR EACH ROW
    WHEN (OLD.situacao = 'CONFIRMADO' AND (NEW.situacao IS DISTINCT FROM 'CONFIRMADO' OR NEW.processo_seletivo_id IS DISTINCT FROM OLD.processo_seletivo_id))
    EXECUTE FUNCTION selecao.fn_retencao_do_documento_confirmado('ck_modelos_de_documento_confirmado_retido');

DROP TRIGGER IF EXISTS trg_modelos_de_documento_bloqueia_truncate ON selecao.modelos_de_documento;
CREATE TRIGGER trg_modelos_de_documento_bloqueia_truncate
    BEFORE TRUNCATE ON selecao.modelos_de_documento
    FOR EACH STATEMENT
    EXECUTE FUNCTION selecao.fn_retencao_do_documento_confirmado('ck_modelos_de_documento_confirmado_retido');
