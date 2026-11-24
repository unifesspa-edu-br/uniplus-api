-- Append-only imposto pelo banco (ADR-0063, ADR-0107). O ato publicado, o vínculo com a entidade, a exclusividade de atos do objeto e o
-- anexo são prova legal: nenhum agregado expõe mutador, e o trigger fecha a brecha do UPDATE/DELETE cru.
CREATE OR REPLACE FUNCTION publicacoes.fn_atos_normativos_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'atos_normativos é append-only (ADR-0063): operação % é bloqueada; o passado documental não se muta.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_vinculos_do_ato_com_entidade_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'vinculos_do_ato_com_entidade é append-only (ADR-0063): operação % é bloqueada; o vínculo é parte do ato publicado.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_exclusividades_de_atos_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'exclusividades_de_atos é append-only (ADR-0107): operação % é bloqueada; a exclusividade de atos do objeto não se transfere.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_anexos_do_ato_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'anexos_do_ato é append-only (ADR-0063): operação % é bloqueada; o anexo é parte do ato publicado.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

-- TRUNCATE não é DELETE: não dispara trigger de linha e esvaziaria as tabelas sem que o append-only percebesse.
CREATE OR REPLACE FUNCTION publicacoes.fn_bloqueia_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'TRUNCATE é bloqueado em %.%: o registro é append-only (ADR-0063).', TG_TABLE_SCHEMA, TG_TABLE_NAME
        USING ERRCODE = 'restrict_violation';
END;
$$;

DROP TRIGGER IF EXISTS trg_atos_normativos_somente_insercao ON publicacoes.atos_normativos;
CREATE TRIGGER trg_atos_normativos_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.atos_normativos
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_atos_normativos_somente_insercao();

DROP TRIGGER IF EXISTS trg_vinculos_do_ato_com_entidade_somente_insercao ON publicacoes.vinculos_do_ato_com_entidade;
CREATE TRIGGER trg_vinculos_do_ato_com_entidade_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.vinculos_do_ato_com_entidade
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_vinculos_do_ato_com_entidade_somente_insercao();

DROP TRIGGER IF EXISTS trg_exclusividades_de_atos_somente_insercao ON publicacoes.exclusividades_de_atos;
CREATE TRIGGER trg_exclusividades_de_atos_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.exclusividades_de_atos
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_exclusividades_de_atos_somente_insercao();

DROP TRIGGER IF EXISTS trg_anexos_do_ato_somente_insercao ON publicacoes.anexos_do_ato;
CREATE TRIGGER trg_anexos_do_ato_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.anexos_do_ato
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_anexos_do_ato_somente_insercao();

DROP TRIGGER IF EXISTS trg_atos_normativos_bloqueia_truncate ON publicacoes.atos_normativos;
CREATE TRIGGER trg_atos_normativos_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.atos_normativos
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();

DROP TRIGGER IF EXISTS trg_vinculos_do_ato_com_entidade_bloqueia_truncate ON publicacoes.vinculos_do_ato_com_entidade;
CREATE TRIGGER trg_vinculos_do_ato_com_entidade_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.vinculos_do_ato_com_entidade
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();

DROP TRIGGER IF EXISTS trg_exclusividades_de_atos_bloqueia_truncate ON publicacoes.exclusividades_de_atos;
CREATE TRIGGER trg_exclusividades_de_atos_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.exclusividades_de_atos
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();

DROP TRIGGER IF EXISTS trg_anexos_do_ato_bloqueia_truncate ON publicacoes.anexos_do_ato;
CREATE TRIGGER trg_anexos_do_ato_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.anexos_do_ato
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();
