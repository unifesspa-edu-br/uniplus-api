-- O que a exclusividade de atos afirma sobre o ato tem de ser verdade (ADR-0107). Sem isto, uma linha forjada (tipo que
-- não é o do ato, ato inicial que não é o da sua cadeia, objeto a que o ato não se vincula, ou ato de tipo que nem é
-- único por objeto) reservaria a exclusividade de um objeto sem que ato publicado algum o justificasse.
CREATE OR REPLACE FUNCTION publicacoes.fn_exclusividades_de_atos_coerente_com_ato()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_unico boolean;
    v_tipo_codigo varchar(60);
BEGIN
    SELECT unico_por_objeto, tipo_codigo INTO v_unico, v_tipo_codigo
    FROM publicacoes.atos_normativos WHERE id = NEW.ato_id;

    IF NOT v_unico THEN
        RAISE EXCEPTION
            'o ato % não é de tipo único por objeto e não reserva exclusividade de atos alguma.', NEW.ato_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF v_tipo_codigo <> NEW.tipo_codigo THEN
        RAISE EXCEPTION
            'o tipo da exclusividade de atos (%) diverge do tipo do ato % (%).', NEW.tipo_codigo, NEW.ato_id, v_tipo_codigo
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF NEW.ato_inicial_id <> publicacoes.fn_ato_inicial_da_cadeia(NEW.ato_id) THEN
        RAISE EXCEPTION
            'o ato inicial declarado (%) não é o ato inicial da cadeia de retificação do ato %.', NEW.ato_inicial_id, NEW.ato_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM publicacoes.vinculos_do_ato_com_entidade v
        WHERE v.ato_id = NEW.ato_id
          AND v.entidade_tipo = NEW.entidade_tipo
          AND v.entidade_id = NEW.entidade_id
    ) THEN
        RAISE EXCEPTION
            'o ato % não está vinculado à entidade %/%, cuja exclusividade de atos a linha reserva.', NEW.ato_id, NEW.entidade_tipo, NEW.entidade_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    RETURN NEW;
END;
$$;

-- A correspondência reversa, que faz o índice único valer alguma coisa: todo vínculo de um ato único por objeto tem de
-- ter a exclusividade de atos daquele objeto reservada em nome da sua cadeia de retificação. Sem isto, gravar o vínculo
-- e omitir a exclusividade deixaria o objeto livre para uma segunda cadeia, e o índice, que só vê as exclusividades que
-- existem, nada acusaria.
CREATE OR REPLACE FUNCTION publicacoes.fn_vinculo_exige_exclusividade_de_atos()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_unico boolean;
    v_tipo_codigo varchar(60);
    v_ato_inicial uuid;
BEGIN
    SELECT unico_por_objeto, tipo_codigo INTO v_unico, v_tipo_codigo
    FROM publicacoes.atos_normativos WHERE id = NEW.ato_id;

    IF NOT v_unico THEN
        RETURN NEW;
    END IF;

    v_ato_inicial := publicacoes.fn_ato_inicial_da_cadeia(NEW.ato_id);

    IF NOT EXISTS (
        SELECT 1 FROM publicacoes.exclusividades_de_atos e
        WHERE e.entidade_tipo = NEW.entidade_tipo
          AND e.entidade_id = NEW.entidade_id
          AND e.tipo_codigo = v_tipo_codigo
          AND e.ato_inicial_id = v_ato_inicial
    ) THEN
        RAISE EXCEPTION
            'o ato % é de tipo único por objeto e vincula-se a %/% sem que a exclusividade de atos do objeto esteja reservada para a cadeia iniciada pelo ato %.',
            NEW.ato_id, NEW.entidade_tipo, NEW.entidade_id, v_ato_inicial
            USING ERRCODE = 'restrict_violation';
    END IF;

    RETURN NEW;
END;
$$;

-- DEFERRABLE INITIALLY DEFERRED nos dois: o ato, o vínculo e a exclusividade de atos entram na mesma transação, e a
-- ordem em que o EF emite os INSERTs não é contratual. No fim da transação, ambas as pontas já existem.
DROP TRIGGER IF EXISTS trg_exclusividades_de_atos_coerente_com_ato ON publicacoes.exclusividades_de_atos;
CREATE CONSTRAINT TRIGGER trg_exclusividades_de_atos_coerente_com_ato
    AFTER INSERT ON publicacoes.exclusividades_de_atos
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_exclusividades_de_atos_coerente_com_ato();

DROP TRIGGER IF EXISTS trg_vinculo_exige_exclusividade_de_atos ON publicacoes.vinculos_do_ato_com_entidade;
CREATE CONSTRAINT TRIGGER trg_vinculo_exige_exclusividade_de_atos
    AFTER INSERT ON publicacoes.vinculos_do_ato_com_entidade
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_vinculo_exige_exclusividade_de_atos();
