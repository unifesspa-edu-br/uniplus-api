-- Raiz da cadeia de retificação de um ato: sobe até quem não emenda ninguém.
--
-- A aciclicidade não é dada pelo que já existe. A chave estrangeira de ato_retificado_id é verificada ao fim do
-- comando, não a cada linha: um único INSERT com duas linhas (A retificando B, B retificando A) passa. O CHECK de
-- não-autorreferência só barra o ciclo de tamanho um, e o índice único da linearidade não vê ciclo nenhum. O ciclo,
-- uma vez gravado, seria irreparável (UPDATE e DELETE estão bloqueados) e faria qualquer travessia girar para sempre.
-- A função detecta o ciclo pela marcação dos visitados: um teto de profundidade confundiria uma cadeia longa e
-- legítima com corrupção.
CREATE OR REPLACE FUNCTION publicacoes.fn_raiz_da_cadeia(p_ato_id uuid)
RETURNS uuid
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    atual uuid := p_ato_id;
    pai uuid;
    visitados uuid[] := ARRAY[]::uuid[];
BEGIN
    LOOP
        IF atual = ANY(visitados) THEN
            RAISE EXCEPTION
                'a cadeia de retificação que passa por % é cíclica.', p_ato_id
                USING ERRCODE = 'restrict_violation';
        END IF;

        visitados := visitados || atual;

        SELECT ato_retificado_id INTO pai
        FROM publicacoes.ato_normativo WHERE id = atual;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'ato % não existe.', atual
                USING ERRCODE = 'restrict_violation';
        END IF;

        EXIT WHEN pai IS NULL;

        atual := pai;
    END LOOP;

    RETURN atual;
END;
$$;

-- A cadeia de todo ato registrado termina numa raiz, verificado no fim da transação, quando as linhas do comando
-- inteiro já existem. É este trigger, e não a chave estrangeira, que barra o INSERT multi-linha cíclico.
CREATE OR REPLACE FUNCTION publicacoes.fn_ato_normativo_cadeia_aciclica()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM publicacoes.fn_raiz_da_cadeia(NEW.id);
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_ato_normativo_cadeia_aciclica ON publicacoes.ato_normativo;
CREATE CONSTRAINT TRIGGER trg_ato_normativo_cadeia_aciclica
    AFTER INSERT ON publicacoes.ato_normativo
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION publicacoes.fn_ato_normativo_cadeia_aciclica();
