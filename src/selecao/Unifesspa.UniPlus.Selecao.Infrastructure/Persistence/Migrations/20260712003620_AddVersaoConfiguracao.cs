using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersaoConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "versoes_configuracao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_versao = table.Column<int>(type: "integer", nullable: false),
                    vigente_a_partir_de = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    algoritmo_hash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    configuracao_congelada_canonica = table.Column<byte[]>(type: "bytea", nullable: false),
                    configuracao_congelada = table.Column<string>(type: "jsonb", nullable: false),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ato_criador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_criador_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ato_criador_retifica_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ator_usuario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_configuracao", x => x.id);
                    table.CheckConstraint("ck_versoes_configuracao_ato_criador_hash", "ato_criador_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versoes_configuracao_ato_criador_nao_zero", "ato_criador_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_versoes_configuracao_contrato_abertura", "(numero_versao = 1 AND ato_criador_retifica_id IS NULL) OR (numero_versao > 1 AND ato_criador_retifica_id IS NOT NULL)");
                    table.CheckConstraint("ck_versoes_configuracao_hash_configuracao", "hash_configuracao ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versoes_configuracao_nao_autorretifica", "ato_criador_retifica_id IS NULL OR ato_criador_retifica_id <> ato_criador_id");
                    table.CheckConstraint("ck_versoes_configuracao_numero_positivo", "numero_versao > 0");
                    table.ForeignKey(
                        name: "fk_versoes_configuracao_processo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_versoes_configuracao_processo_vigencia",
                schema: "selecao",
                table: "versoes_configuracao",
                columns: new[] { "processo_seletivo_id", "vigente_a_partir_de", "numero_versao" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_versoes_configuracao_ato_criador",
                schema: "selecao",
                table: "versoes_configuracao",
                column: "ato_criador_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_versoes_configuracao_processo_numero",
                schema: "selecao",
                table: "versoes_configuracao",
                columns: new[] { "processo_seletivo_id", "numero_versao" },
                unique: true);

            // Enforcement de banco do append-only (ADR-0063). A entidade forense
            // não expõe mutadores e nenhum handler chama Update/Remove — o trigger
            // fecha a última brecha: um UPDATE/DELETE cru fora do agregado. A
            // configuração congelada é a prova do que valia quando o certame foi
            // publicado (RN08); reescrevê-la é reescrever o passado.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_versoes_configuracao_somente_insercao
                    BEFORE UPDATE OR DELETE ON selecao.versoes_configuracao
                    FOR EACH ROW
                    EXECUTE FUNCTION selecao.fn_versoes_configuracao_somente_insercao();
                """);

            // TRUNCATE não é DELETE: não dispara trigger de linha, e esvaziaria a
            // tabela sem que o append-only percebesse. O trigger de statement fecha
            // essa porta.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_versoes_configuracao_bloqueia_truncate
                    BEFORE TRUNCATE ON selecao.versoes_configuracao
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION selecao.fn_versoes_configuracao_bloqueia_truncate();
                """);

            // Sucessão da cadeia de versões (ADR-0104). Duas invariantes que um
            // CHECK não alcança, porque dependem de OUTRA linha da tabela:
            //
            //   1. a numeração é contígua — uma versão N sem a N−1 é recusada;
            //   2. o ato criador da versão N retifica o ato criador da versão N−1 —
            //      é a trava que impede uma SEGUNDA cadeia de versões no mesmo
            //      certame (duas configurações vigentes, cada uma se dizendo a atual).
            //
            // O domínio já recusa ambas antes (VersaoConfiguracao.Suceder); o trigger
            // cobre o INSERT cru fora do agregado. A corrida check-then-act entre duas
            // publicações concorrentes NÃO é papel dele — as duas leriam o mesmo topo e
            // passariam: quem a barra é ux_versoes_configuracao_processo_numero, que
            // recusa a segunda com erro de duplicidade.
            //
            // RAISE declara CONSTRAINT/TABLE/SCHEMA para que PostgresException.ConstraintName
            // chegue populado ao handler, que o traduz em DomainError nomeado (ADR-0102) —
            // sem isso, a violação afloraria como 500 opaco.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_versoes_configuracao_sucessao
                    BEFORE INSERT ON selecao.versoes_configuracao
                    FOR EACH ROW
                    EXECUTE FUNCTION selecao.fn_versoes_configuracao_sucessao();
                """);

            // Transporta os congelamentos já existentes para o novo modelo ANTES de
            // dropar a tabela antiga. A configuração congelada é prova do que valia
            // no certame (RN08): perdê-la deixaria um processo publicado sem versão —
            // o seletor devolveria "vigente ausente", e a retificação, estado
            // inconsistente. Cada snapshot vira uma versão, preservando o seu próprio
            // id (é ele que o ProcessoPublicadoEvent já publicou no Kafka e que os
            // consumidores guardaram como referência forense durável).
            //
            // O que se deriva da cadeia de editais:
            //   - numero_versao: a ordem da publicação dentro do processo. A cadeia
            //     legada é linear e ux_editais_processo_data_publicacao garantia
            //     unicidade da data, então ordenar por ela é determinístico;
            //   - ato_criador_id/hash: o Edital que emitiu o snapshot, por valor;
            //   - ato_criador_retifica_id: o Edital que aquele Edital retifica — nulo
            //     na abertura, que é exatamente o contrato simétrico da nova tabela.
            //
            // O INSERT passa pelo trigger de sucessão: dados legados incoerentes
            // (buraco, cadeia quebrada, vigência regressiva) FALHAM a migration em vez
            // de entrar como evidência corrompida. O ORDER BY garante que a versão N
            // seja inserida depois da N-1, como o trigger exige.
            migrationBuilder.Sql("""
                INSERT INTO selecao.versoes_configuracao (
                    id, processo_seletivo_id, numero_versao, vigente_a_partir_de,
                    schema_version, algoritmo_hash,
                    configuracao_congelada_canonica, configuracao_congelada, hash_configuracao,
                    ato_criador_id, ato_criador_hash, ato_criador_retifica_id, ator_usuario_sub)
                SELECT
                    s.id,
                    e.processo_seletivo_id,
                    ROW_NUMBER() OVER (
                        PARTITION BY e.processo_seletivo_id
                        ORDER BY e.data_publicacao, e.id)::int,
                    s.data_publicacao,
                    s.schema_version,
                    s.algoritmo_hash,
                    s.configuracao_congelada_canonica,
                    s.configuracao_congelada,
                    s.hash_configuracao,
                    s.edital_id,
                    s.hash_edital,
                    e.edital_retificado_id,
                    s.ator_usuario_sub
                FROM selecao.snapshot_publicacao s
                JOIN selecao.editais e ON e.id = s.edital_id
                ORDER BY e.processo_seletivo_id, e.data_publicacao, e.id;
                """);

            migrationBuilder.DropTable(
                name: "snapshot_publicacao",
                schema: "selecao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_versoes_configuracao_sucessao ON selecao.versoes_configuracao;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_versoes_configuracao_bloqueia_truncate ON selecao.versoes_configuracao;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_versoes_configuracao_somente_insercao ON selecao.versoes_configuracao;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS selecao.fn_versoes_configuracao_sucessao();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS selecao.fn_versoes_configuracao_bloqueia_truncate();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS selecao.fn_versoes_configuracao_somente_insercao();");

            migrationBuilder.CreateTable(
                name: "snapshot_publicacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    algoritmo_hash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ator_usuario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    configuracao_congelada = table.Column<string>(type: "jsonb", nullable: false),
                    configuracao_congelada_canonica = table.Column<byte[]>(type: "bytea", nullable: false),
                    data_publicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    hash_edital = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_snapshot_publicacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_snapshot_publicacao_edital_id",
                        column: x => x.edital_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_snapshot_publicacao_edital_id",
                schema: "selecao",
                table: "snapshot_publicacao",
                column: "edital_id",
                unique: true);

            // Devolve os congelamentos ao modelo antigo antes de dropar o novo — o
            // reverso do backfill do Up. Perde-se apenas o que o modelo antigo não
            // sabe representar (o número da versão, e a vigência separada da data
            // documental), porque lá as duas grandezas eram a mesma coluna.
            migrationBuilder.Sql("""
                INSERT INTO selecao.snapshot_publicacao (
                    id, edital_id, schema_version, algoritmo_hash,
                    configuracao_congelada_canonica, configuracao_congelada,
                    hash_configuracao, hash_edital, ator_usuario_sub, data_publicacao)
                SELECT
                    v.id, v.ato_criador_id, v.schema_version, v.algoritmo_hash,
                    v.configuracao_congelada_canonica, v.configuracao_congelada,
                    v.hash_configuracao, v.ato_criador_hash, v.ator_usuario_sub, v.vigente_a_partir_de
                FROM selecao.versoes_configuracao v;
                """);

            migrationBuilder.DropTable(
                name: "versoes_configuracao",
                schema: "selecao");
        }
    }
}
