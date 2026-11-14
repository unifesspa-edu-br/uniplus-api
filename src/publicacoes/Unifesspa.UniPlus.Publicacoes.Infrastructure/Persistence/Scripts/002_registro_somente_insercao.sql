-- Append-only imposto pelo banco (ADR-0063, ADR-0107). O ato publicado, o vínculo com a entidade, a vaga do objeto e o
-- anexo são prova legal: nenhum agregado expõe mutador, e o trigger fecha a brecha do UPDATE/DELETE cru.
CREATE OR REPLACE FUNCTION publicacoes.fn_ato_normativo_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'ato_normativo é append-only (ADR-0063): operação % é bloqueada; o passado documental não se muta.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_vinculo_ato_entidade_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'vinculo_ato_entidade é append-only (ADR-0063): operação % é bloqueada; o vínculo é parte do ato publicado.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_linhagem_unica_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'linhagem_unica_por_objeto é append-only (ADR-0107): operação % é bloqueada; a vaga do objeto não se transfere.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

CREATE OR REPLACE FUNCTION publicacoes.fn_anexo_do_ato_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'anexo_do_ato é append-only (ADR-0063): operação % é bloqueada; o anexo é parte do ato publicado.', TG_OP
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

DROP TRIGGER IF EXISTS trg_ato_normativo_somente_insercao ON publicacoes.ato_normativo;
CREATE TRIGGER trg_ato_normativo_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.ato_normativo
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_ato_normativo_somente_insercao();

DROP TRIGGER IF EXISTS trg_vinculo_ato_entidade_somente_insercao ON publicacoes.vinculo_ato_entidade;
CREATE TRIGGER trg_vinculo_ato_entidade_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.vinculo_ato_entidade
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_vinculo_ato_entidade_somente_insercao();

DROP TRIGGER IF EXISTS trg_linhagem_unica_somente_insercao ON publicacoes.linhagem_unica_por_objeto;
CREATE TRIGGER trg_linhagem_unica_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.linhagem_unica_por_objeto
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_linhagem_unica_somente_insercao();

DROP TRIGGER IF EXISTS trg_anexo_do_ato_somente_insercao ON publicacoes.anexo_do_ato;
CREATE TRIGGER trg_anexo_do_ato_somente_insercao
    BEFORE UPDATE OR DELETE ON publicacoes.anexo_do_ato
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_anexo_do_ato_somente_insercao();

DROP TRIGGER IF EXISTS trg_ato_normativo_bloqueia_truncate ON publicacoes.ato_normativo;
CREATE TRIGGER trg_ato_normativo_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.ato_normativo
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();

DROP TRIGGER IF EXISTS trg_vinculo_ato_entidade_bloqueia_truncate ON publicacoes.vinculo_ato_entidade;
CREATE TRIGGER trg_vinculo_ato_entidade_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.vinculo_ato_entidade
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();

DROP TRIGGER IF EXISTS trg_linhagem_unica_bloqueia_truncate ON publicacoes.linhagem_unica_por_objeto;
CREATE TRIGGER trg_linhagem_unica_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.linhagem_unica_por_objeto
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();

DROP TRIGGER IF EXISTS trg_anexo_do_ato_bloqueia_truncate ON publicacoes.anexo_do_ato;
CREATE TRIGGER trg_anexo_do_ato_bloqueia_truncate
    BEFORE TRUNCATE ON publicacoes.anexo_do_ato
    FOR EACH STATEMENT
    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();
