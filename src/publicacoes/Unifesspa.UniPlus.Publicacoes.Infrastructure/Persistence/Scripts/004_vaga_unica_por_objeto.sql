-- O que a vaga afirma sobre o ato tem de ser verdade (ADR-0107). Sem isto, uma linha forjada (tipo que não é o do ato,
-- raiz que não é a da sua cadeia, objeto a que o ato não se vincula, ou ato de tipo que nem é único por objeto)
-- ocuparia a vaga de um objeto sem que ato publicado algum o justificasse.
CREATE OR REPLACE FUNCTION publicacoes.fn_linhagem_unica_coerente_com_ato()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_unico boolean;
    v_tipo_codigo varchar(60);
BEGIN
    SELECT unico_por_objeto, tipo_codigo INTO v_unico, v_tipo_codigo
    FROM publicacoes.ato_normativo WHERE id = NEW.ato_id;

    IF NOT v_unico THEN
        RAISE EXCEPTION
            'o ato % não é de tipo único por objeto e não reserva vaga alguma.', NEW.ato_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF v_tipo_codigo <> NEW.tipo_codigo THEN
        RAISE EXCEPTION
            'o tipo da vaga (%) diverge do tipo do ato % (%).', NEW.tipo_codigo, NEW.ato_id, v_tipo_codigo
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF NEW.raiz_id <> publicacoes.fn_raiz_da_cadeia(NEW.ato_id) THEN
        RAISE EXCEPTION
            'a raiz declarada (%) não é a raiz da cadeia de retificação do ato %.', NEW.raiz_id, NEW.ato_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM publicacoes.vinculo_ato_entidade v
        WHERE v.ato_id = NEW.ato_id
          AND v.entidade_tipo = NEW.entidade_tipo
          AND v.entidade_id = NEW.entidade_id
    ) THEN
        RAISE EXCEPTION
            'o ato % não está vinculado à entidade %/%, cuja vaga a linha reserva.', NEW.ato_id, NEW.entidade_tipo, NEW.entidade_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    RETURN NEW;
END;
$$;

-- A correspondência reversa, que faz o índice único valer alguma coisa: todo vínculo de um ato único por objeto tem de
-- ter a vaga daquele objeto reservada em nome da sua linhagem. Sem isto, gravar o vínculo e omitir a vaga deixaria o
-- objeto livre para uma segunda linhagem, e o índice, que só vê as vagas que existem, nada acusaria.
CREATE OR REPLACE FUNCTION publicacoes.fn_vinculo_exige_vaga_da_linhagem()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_unico boolean;
    v_tipo_codigo varchar(60);
    v_raiz uuid;
BEGIN
    SELECT unico_por_objeto, tipo_codigo INTO v_unico, v_tipo_codigo
    FROM publicacoes.ato_normativo WHERE id = NEW.ato_id;

    IF NOT v_unico THEN
        RETURN NEW;
    END IF;

    v_raiz := publicacoes.fn_raiz_da_cadeia(NEW.ato_id);

    IF NOT EXISTS (
        SELECT 1 FROM publicacoes.linhagem_unica_por_objeto l
        WHERE l.entidade_tipo = NEW.entidade_tipo
          AND l.entidade_id = NEW.entidade_id
          AND l.tipo_codigo = v_tipo_codigo
          AND l.raiz_id = v_raiz
    ) THEN
        RAISE EXCEPTION
            'o ato % é de tipo único por objeto e vincula-se a %/% sem que a vaga do objeto esteja reservada para a linhagem %.',
            NEW.ato_id, NEW.entidade_tipo, NEW.entidade_id, v_raiz
            USING ERRCODE = 'restrict_violation';
    END IF;

    RETURN NEW;
END;
$$;

-- DEFERRABLE INITIALLY DEFERRED nos dois: o ato, o vínculo e a vaga entram na mesma transação, e a ordem em que o EF
-- emite os INSERTs não é contratual. No fim da transação, ambas as pontas já existem.
DROP TRIGGER IF EXISTS trg_linhagem_unica_coerente_com_ato ON publicacoes.linhagem_unica_por_objeto;
CREATE CONSTRAINT TRIGGER trg_linhagem_unica_coerente_com_ato
    AFTER INSERT ON publicacoes.linhagem_unica_por_objeto
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_linhagem_unica_coerente_com_ato();

DROP TRIGGER IF EXISTS trg_vinculo_exige_vaga_da_linhagem ON publicacoes.vinculo_ato_entidade;
CREATE CONSTRAINT TRIGGER trg_vinculo_exige_vaga_da_linhagem
    AFTER INSERT ON publicacoes.vinculo_ato_entidade
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_vinculo_exige_vaga_da_linhagem();
