-- Enforcement de banco do append-only (ADR-0063). A entidade forense não expõe mutadores e nenhum handler chama
-- Update/Remove: o trigger fecha a última brecha, um UPDATE/DELETE cru fora do agregado. A configuração congelada é a
-- prova do que valia quando o certame foi publicado (congelamento dos parâmetros na publicação, UNI-REQ-0019);
-- reescrevê-la é reescrever o passado.
CREATE OR REPLACE FUNCTION selecao.fn_versoes_configuracao_somente_insercao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'versoes_configuracao é append-only (ADR-0063): operação % é bloqueada; a configuração congelada não se muta.', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$;

DROP TRIGGER IF EXISTS trg_versoes_configuracao_somente_insercao ON selecao.versoes_configuracao;
CREATE TRIGGER trg_versoes_configuracao_somente_insercao
    BEFORE UPDATE OR DELETE ON selecao.versoes_configuracao
    FOR EACH ROW
    EXECUTE FUNCTION selecao.fn_versoes_configuracao_somente_insercao();

-- TRUNCATE não é DELETE: não dispara trigger de linha e esvaziaria a tabela sem que o append-only percebesse. O trigger
-- de statement fecha essa porta.
CREATE OR REPLACE FUNCTION selecao.fn_versoes_configuracao_bloqueia_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'TRUNCATE é bloqueado em %.%: o registro é append-only (ADR-0063).', TG_TABLE_SCHEMA, TG_TABLE_NAME
        USING ERRCODE = 'restrict_violation';
END;
$$;

DROP TRIGGER IF EXISTS trg_versoes_configuracao_bloqueia_truncate ON selecao.versoes_configuracao;
CREATE TRIGGER trg_versoes_configuracao_bloqueia_truncate
    BEFORE TRUNCATE ON selecao.versoes_configuracao
    FOR EACH STATEMENT
    EXECUTE FUNCTION selecao.fn_versoes_configuracao_bloqueia_truncate();

-- Sucessão da cadeia de versões (ADR-0104). Invariantes que um CHECK não alcança, porque dependem de OUTRA linha da
-- tabela: a numeração é contígua, o ato criador da versão N retifica o ato criador da versão N-1 (a trava que impede uma
-- SEGUNDA cadeia no mesmo certame) e a vigência não regride. O domínio já recusa as três antes
-- (VersaoConfiguracao.Suceder); o trigger cobre o INSERT cru fora do agregado. A corrida entre duas publicações
-- concorrentes NÃO é papel dele, porque as duas leriam o mesmo topo e passariam: quem a barra é
-- ux_versoes_configuracao_processo_numero, que recusa a segunda com erro de duplicidade.
-- O RAISE declara CONSTRAINT/TABLE/SCHEMA para que PostgresException.ConstraintName chegue populado ao handler, que o
-- traduz em DomainError nomeado (ADR-0102); sem isso, a violação afloraria como 500 opaco.
CREATE OR REPLACE FUNCTION selecao.fn_versoes_configuracao_sucessao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    anterior RECORD;
BEGIN
    SELECT numero_versao, ato_criador_id, vigente_a_partir_de
      INTO anterior
      FROM selecao.versoes_configuracao
     WHERE processo_seletivo_id = NEW.processo_seletivo_id
     ORDER BY numero_versao DESC
     LIMIT 1;

    IF NOT FOUND THEN
        IF NEW.numero_versao <> 1 THEN
            RAISE EXCEPTION
                'A primeira versão da configuração do processo % deve ser a versão 1 (recebida: %).',
                NEW.processo_seletivo_id, NEW.numero_versao
                USING ERRCODE = 'check_violation',
                      CONSTRAINT = 'ck_versoes_configuracao_numeracao_contigua',
                      TABLE = 'versoes_configuracao',
                      SCHEMA = 'selecao';
        END IF;

        RETURN NEW;
    END IF;

    -- Número já ocupado: a numeração é contígua, logo todo número até o
    -- topo existe. Deixa passar para o índice único, que devolve o erro
    -- de DUPLICIDADE — conflacioná-lo com "buraco" aqui daria ao cliente
    -- o diagnóstico errado.
    IF NEW.numero_versao <= anterior.numero_versao THEN
        RETURN NEW;
    END IF;

    IF NEW.numero_versao > anterior.numero_versao + 1 THEN
        RAISE EXCEPTION
            'A numeração das versões é contígua: o processo % espera a versão % (recebida: %).',
            NEW.processo_seletivo_id, anterior.numero_versao + 1, NEW.numero_versao
            USING ERRCODE = 'check_violation',
                  CONSTRAINT = 'ck_versoes_configuracao_numeracao_contigua',
                  TABLE = 'versoes_configuracao',
                  SCHEMA = 'selecao';
    END IF;

    IF NEW.ato_criador_retifica_id IS DISTINCT FROM anterior.ato_criador_id THEN
        RAISE EXCEPTION
            'O ato criador da versão % deve retificar o ato criador da versão % (esperado: %; recebido: %).',
            NEW.numero_versao, anterior.numero_versao, anterior.ato_criador_id, NEW.ato_criador_retifica_id
            USING ERRCODE = 'check_violation',
                  CONSTRAINT = 'ck_versoes_configuracao_cadeia',
                  TABLE = 'versoes_configuracao',
                  SCHEMA = 'selecao';
    END IF;

    -- A vigência não regride. É ela que ORDENA as versões: uma sucessora
    -- com vigência anterior à da versão que sucede faria o seletor
    -- continuar elegendo a versão VELHA depois de a nova existir. O
    -- relógio do sistema pode andar para trás (ajuste NTP em degrau) — o
    -- domínio empata a sucessora no instante da anterior nesse caso, e o
    -- desempate por número resolve; o que o banco recusa é o retrocesso.
    IF NEW.vigente_a_partir_de < anterior.vigente_a_partir_de THEN
        RAISE EXCEPTION
            'A vigência da versão % (%) não pode preceder a da versão % (%).',
            NEW.numero_versao, NEW.vigente_a_partir_de,
            anterior.numero_versao, anterior.vigente_a_partir_de
            USING ERRCODE = 'check_violation',
                  CONSTRAINT = 'ck_versoes_configuracao_vigencia_monotonica',
                  TABLE = 'versoes_configuracao',
                  SCHEMA = 'selecao';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_versoes_configuracao_sucessao ON selecao.versoes_configuracao;
CREATE TRIGGER trg_versoes_configuracao_sucessao
    BEFORE INSERT ON selecao.versoes_configuracao
    FOR EACH ROW
    EXECUTE FUNCTION selecao.fn_versoes_configuracao_sucessao();
