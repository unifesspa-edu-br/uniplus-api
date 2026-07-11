using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVinculoAtoEntidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "linhagem_unica_por_objeto",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    raiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linhagem_unica_por_objeto", x => x.id);
                    table.CheckConstraint("ck_linhagem_unica_entidade_id_nao_zero", "entidade_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_linhagem_unica_entidade_tipo_formato", "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_linhagem_unica_raiz_nao_zero", "raiz_id <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_linhagem_unica_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_linhagem_unica_raiz",
                        column: x => x.raiz_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_ato_entidade",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_ato_entidade", x => x.id);
                    table.CheckConstraint("ck_vinculo_ato_entidade_id_nao_zero", "entidade_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_vinculo_ato_entidade_tipo_formato", "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_vinculo_ato_entidade_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ato_normativo_data_publicacao",
                schema: "publicacoes",
                table: "ato_normativo",
                columns: new[] { "data_publicacao", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_linhagem_unica_por_objeto_ato_id",
                schema: "publicacoes",
                table: "linhagem_unica_por_objeto",
                column: "ato_id");

            migrationBuilder.CreateIndex(
                name: "ix_linhagem_unica_por_objeto_raiz_id",
                schema: "publicacoes",
                table: "linhagem_unica_por_objeto",
                column: "raiz_id");

            migrationBuilder.CreateIndex(
                name: "ux_linhagem_unica_por_objeto",
                schema: "publicacoes",
                table: "linhagem_unica_por_objeto",
                columns: new[] { "entidade_tipo", "entidade_id", "tipo_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_ato_entidade_objeto",
                schema: "publicacoes",
                table: "vinculo_ato_entidade",
                columns: new[] { "entidade_tipo", "entidade_id", "ato_id" });

            migrationBuilder.CreateIndex(
                name: "ux_vinculo_ato_entidade_trio",
                schema: "publicacoes",
                table: "vinculo_ato_entidade",
                columns: new[] { "ato_id", "entidade_tipo", "entidade_id" },
                unique: true);

            // Append-only imposto pelo banco (ADR-0063), como no ato que estas duas
            // tabelas acompanham. Mutar um vínculo reescreveria de que objeto o ato
            // tratava; mutar uma vaga transferiria o objeto de uma linhagem para outra
            // sem que ato algum o dissesse. As duas coisas corrompem a prova.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_vinculo_ato_entidade_somente_insercao
                    BEFORE UPDATE OR DELETE ON publicacoes.vinculo_ato_entidade
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_vinculo_ato_entidade_somente_insercao();
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_linhagem_unica_somente_insercao
                    BEFORE UPDATE OR DELETE ON publicacoes.linhagem_unica_por_objeto
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_linhagem_unica_somente_insercao();
                """);

            // Raiz da cadeia de retificação de um ato: sobe até quem não emenda ninguém.
            //
            // A aciclicidade NÃO é dada de graça pelo que já existe. A chave estrangeira
            // de ato_retificado_id é imediata, mas é verificada ao fim do COMANDO, não a
            // cada linha: um único INSERT com duas linhas — A retificando B, B retificando
            // A — passa, porque quando a verificação corre ambos já existem. O CHECK de
            // não-autorreferência só barra o ciclo de tamanho um, e o índice único da
            // linearidade não vê ciclo nenhum (A→B e B→A são alvos distintos). O ciclo,
            // uma vez gravado, seria irreparável (UPDATE e DELETE estão bloqueados) e
            // faria qualquer travessia da cadeia girar para sempre.
            //
            // Daí a marcação dos visitados: a função detecta o ciclo em vez de contar
            // passos — um teto de profundidade confundiria uma cadeia longa e legítima
            // com corrupção, e ainda assim só falharia depois de percorrê-la inteira.
            migrationBuilder.Sql("""
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
                """);

            // A cadeia de todo ato registrado termina numa raiz — verificado no fim da
            // transação, quando as linhas do comando inteiro já existem. É este trigger,
            // e não a chave estrangeira, que barra o INSERT multi-linha cíclico descrito
            // acima. Custo por ato: subir a própria cadeia, que a linearidade mantém curta.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION publicacoes.fn_ato_normativo_cadeia_aciclica()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM publicacoes.fn_raiz_da_cadeia(NEW.id);
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_ato_normativo_cadeia_aciclica
                    AFTER INSERT ON publicacoes.ato_normativo
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_ato_normativo_cadeia_aciclica();
                """);

            // O que a vaga afirma sobre o ato tem de ser verdade (ADR-0107). Sem isto,
            // uma linha forjada — tipo que não é o do ato, raiz que não é a da sua
            // cadeia, objeto a que o ato não se vincula, ou ato de tipo que nem é único
            // por objeto — ocuparia a vaga de um objeto sem que ato publicado algum o
            // justificasse. Nenhuma dessas coisas passa pelo agregado; o trigger fecha a
            // brecha do INSERT cru, como os CHECKs do ato fecham a dele.
            migrationBuilder.Sql("""
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
                """);

            // A correspondência reversa, e é ela que faz o índice único valer alguma
            // coisa: todo vínculo de um ato único por objeto tem de ter a vaga daquele
            // objeto reservada em nome da sua linhagem. Sem isto, gravar o vínculo e
            // omitir a vaga — um importador, um seed, uma regressão — deixaria o objeto
            // livre para uma segunda linhagem, e o índice, que só vê as vagas que
            // existem, nada acusaria.
            migrationBuilder.Sql("""
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
                """);

            // DEFERRABLE INITIALLY DEFERRED nos dois: o ato, o vínculo e a vaga entram na
            // mesma transação, e a ordem em que o EF emite os INSERTs não é contratual —
            // uma verificação imediata acusaria a ausência do que ainda não foi gravado.
            // No fim da transação, ambas as pontas já existem.
            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_linhagem_unica_coerente_com_ato
                    AFTER INSERT ON publicacoes.linhagem_unica_por_objeto
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_linhagem_unica_coerente_com_ato();
                """);

            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER trg_vinculo_exige_vaga_da_linhagem
                    AFTER INSERT ON publicacoes.vinculo_ato_entidade
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_vinculo_exige_vaga_da_linhagem();
                """);

            // TRUNCATE não é DELETE: não dispara trigger de linha, e esvaziaria as tabelas
            // sem que o append-only percebesse. O trigger de statement fecha essa porta —
            // inclusive na do ato publicado, que a tinha aberta desde que nasceu.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_ato_normativo_bloqueia_truncate
                    BEFORE TRUNCATE ON publicacoes.ato_normativo
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_vinculo_ato_entidade_bloqueia_truncate
                    BEFORE TRUNCATE ON publicacoes.vinculo_ato_entidade
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_linhagem_unica_bloqueia_truncate
                    BEFORE TRUNCATE ON publicacoes.linhagem_unica_por_objeto
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_linhagem_unica_bloqueia_truncate ON publicacoes.linhagem_unica_por_objeto;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_vinculo_ato_entidade_bloqueia_truncate ON publicacoes.vinculo_ato_entidade;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_ato_normativo_bloqueia_truncate ON publicacoes.ato_normativo;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_ato_normativo_cadeia_aciclica ON publicacoes.ato_normativo;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_vinculo_exige_vaga_da_linhagem ON publicacoes.vinculo_ato_entidade;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_linhagem_unica_coerente_com_ato ON publicacoes.linhagem_unica_por_objeto;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_linhagem_unica_somente_insercao ON publicacoes.linhagem_unica_por_objeto;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_vinculo_ato_entidade_somente_insercao ON publicacoes.vinculo_ato_entidade;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_bloqueia_truncate();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_ato_normativo_cadeia_aciclica();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_vinculo_exige_vaga_da_linhagem();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_linhagem_unica_coerente_com_ato();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_linhagem_unica_somente_insercao();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_vinculo_ato_entidade_somente_insercao();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_raiz_da_cadeia(uuid);");

            migrationBuilder.DropTable(
                name: "linhagem_unica_por_objeto",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "vinculo_ato_entidade",
                schema: "publicacoes");

            migrationBuilder.DropIndex(
                name: "ix_ato_normativo_data_publicacao",
                schema: "publicacoes",
                table: "ato_normativo");
        }
    }
}
