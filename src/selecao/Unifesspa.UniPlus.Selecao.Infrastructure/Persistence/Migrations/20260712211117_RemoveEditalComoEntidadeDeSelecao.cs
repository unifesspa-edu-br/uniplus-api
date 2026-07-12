using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Elimina a tabela <c>editais</c> (ADR-0103): o documento normativo passa a viver
    /// exclusivamente no módulo Publicações, e a Seleção o referencia por VALOR — o par
    /// <c>{ato_criador_id, ato_criador_hash}</c> que <c>versoes_configuracao</c> já guarda
    /// (ADR-0061). Com ela vão o enum <c>natureza</c> e os dois índices que o
    /// carregavam no filtro: acrescentar um tipo de ato deixa de exigir migration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada coluna já tinha dono em outro lugar: <c>numero</c>, <c>documento_edital_id</c> e
    /// <c>motivo_retificacao</c> estão congelados nos bytes canônicos da versão (ADR-0100);
    /// <c>edital_retificado_id</c> é <c>versoes_configuracao.ato_criador_retifica_id</c>;
    /// <c>data_publicacao</c> é do ato, em Publicações. Os guard rails também:
    /// <c>ux_editais_processo_abertura_unica</c> (que filtrava por <c>natureza = 1</c>) é
    /// substituído por <c>ux_versoes_configuracao_processo_numero</c> — não há duas versões 1
    /// no mesmo certame —, e a linearidade da cadeia, por
    /// <c>ux_versoes_configuracao_ato_criador</c> mais o trigger de sucessão. Nenhum deles
    /// menciona tipo de ato.
    /// </para>
    /// <para>
    /// <b>O que NÃO tinha substituto</b>, e por isso entra aqui: a chave estrangeira
    /// <c>fk_editais_documento_edital_id</c> era, com <c>ON DELETE RESTRICT</c>, o que impedia
    /// apagar fisicamente o PDF de um edital já publicado. Sem ela, o documento que fundamenta
    /// uma configuração congelada ficaria removível por um <c>DELETE</c> cru — e a referência
    /// <c>{id, hash}</c> no JSON prova a integridade do conteúdo, não a sua permanência. O
    /// trigger abaixo repõe a retenção, e de forma mais ampla: um documento CONFIRMADO é
    /// evidência e não se remove, tenha ele sido publicado ou não. É o que o próprio ciclo de
    /// vida já declarava — <c>StatusDocumentoEdital.Confirmado</c> não tem caminho de volta.
    /// </para>
    /// </remarks>
    public partial class RemoveEditalComoEntidadeDeSelecao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "editais",
                schema: "selecao");

            // RAISE declara CONSTRAINT/TABLE/SCHEMA para que PostgresException.ConstraintName
            // chegue preenchida à aplicação — mesmo caminho de tradução dos CHECKs (ADR-0102).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION selecao.fn_documentos_edital_retencao()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'Documento confirmado é a evidência do que foi publicado: não se remove nem se desconfirma.'
                        USING ERRCODE = 'check_violation',
                              CONSTRAINT = 'ck_documentos_edital_confirmado_retido',
                              TABLE = 'documentos_edital',
                              SCHEMA = 'selecao';
                END;
                $$;
                """);

            // Só o CONFIRMADO é retido: o pendente é um upload que nunca se completou, e a
            // limpeza dos expirados continua possível. O literal aqui é o valor do enum
            // StatusDocumentoEdital (Confirmado = 1) — ciclo de vida fechado no domínio, não
            // um tipo de ato vindo de cadastro; é justamente a distinção que a ADR-0103 faz.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_documentos_edital_retencao_delete
                BEFORE DELETE ON selecao.documentos_edital
                FOR EACH ROW
                WHEN (OLD.status = 1)
                EXECUTE FUNCTION selecao.fn_documentos_edital_retencao();
                """);

            // Retenção só do DELETE seria contornável: bastaria rebaixar o status para
            // Pendente e então apagar. Confirmado não tem caminho de volta — é o que o
            // próprio ciclo de vida declara —, e barrar a desconfirmação fecha o desvio.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_documentos_edital_retencao_update
                BEFORE UPDATE ON selecao.documentos_edital
                FOR EACH ROW
                WHEN (OLD.status = 1 AND NEW.status <> 1)
                EXECUTE FUNCTION selecao.fn_documentos_edital_retencao();
                """);

            // E TRUNCATE não dispara trigger de linha: sem esta trava, um único comando
            // levaria a tabela inteira — confirmados e tudo. Mesmo par de defesas que
            // versoes_configuracao já usa.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION selecao.fn_documentos_edital_bloqueia_truncate()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'TRUNCATE apagaria os documentos confirmados, que são evidência do publicado.'
                        USING ERRCODE = 'check_violation',
                              CONSTRAINT = 'ck_documentos_edital_confirmado_retido',
                              TABLE = 'documentos_edital',
                              SCHEMA = 'selecao';
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_documentos_edital_bloqueia_truncate
                BEFORE TRUNCATE ON selecao.documentos_edital
                FOR EACH STATEMENT
                EXECUTE FUNCTION selecao.fn_documentos_edital_bloqueia_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // A retenção volta a ser da chave estrangeira recriada abaixo.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_documentos_edital_bloqueia_truncate ON selecao.documentos_edital;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_documentos_edital_retencao_update ON selecao.documentos_edital;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_documentos_edital_retencao_delete ON selecao.documentos_edital;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS selecao.fn_documentos_edital_bloqueia_truncate();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS selecao.fn_documentos_edital_retencao();");

            migrationBuilder.CreateTable(
                name: "editais",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_publicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    documento_edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_retificado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_retificacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    natureza = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_editais", x => x.id);
                    table.CheckConstraint("ck_editais_contrato_natureza", "(natureza = 1 AND edital_retificado_id IS NULL AND motivo_retificacao IS NULL) OR (natureza = 2 AND edital_retificado_id IS NOT NULL AND motivo_retificacao IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_editais_documento_edital_id",
                        column: x => x.documento_edital_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_edital",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_editais_edital_retificado_id",
                        column: x => x.edital_retificado_id,
                        principalSchema: "selecao",
                        principalTable: "editais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_editais_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_editais_documento_edital_id",
                schema: "selecao",
                table: "editais",
                column: "documento_edital_id");

            migrationBuilder.CreateIndex(
                name: "ux_editais_edital_retificado_unico",
                schema: "selecao",
                table: "editais",
                column: "edital_retificado_id",
                unique: true,
                filter: "edital_retificado_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_editais_processo_abertura_unica",
                schema: "selecao",
                table: "editais",
                column: "processo_seletivo_id",
                unique: true,
                filter: "natureza = 1");

            // Recriar a tabela vazia não desfaz a migration: a versão anterior da aplicação
            // LÊ `editais` para hidratar o snapshot vigente e para eleger o alvo da
            // retificação. Com a tabela vazia, todo certame já publicado voltaria referenciando
            // um ato sem linha correspondente, e as duas operações passariam a falhar. O
            // rollback repovoa a partir do que a Seleção ainda possui.
            //
            // Quase tudo se reconstrói: o id do Edital é o `ato_criador_id` da versão que ele
            // criou; a natureza sai da RELAÇÃO (quem não emenda ninguém é abertura — é
            // precisamente a bijeção que provava o enum redundante); número, documento e motivo
            // estão congelados nos bytes canônicos (ADR-0100).
            //
            // A exceção é `data_publicacao`. Ela é a data que o DOCUMENTO declara, e migrou
            // para o ato, em Publicações — a Seleção deixou de guardá-la, que é o ponto desta
            // migration. No rollback usa-se `vigente_a_partir_de` como aproximação: é o instante
            // do SISTEMA, não o do documento, e as duas coincidem no caso comum. O valor
            // autoritativo continua em `publicacoes.atos_normativos`, e é de lá que uma
            // restauração fiel o leria — não daqui, que não atravessa a fronteira do módulo
            // (ADR-0061). A coluna aceita nulo, mas deixá-la nula quebraria a hidratação do
            // snapshot no código antigo, que a exige preenchida.
            migrationBuilder.Sql("""
                INSERT INTO selecao.editais (
                    id, processo_seletivo_id, natureza, numero, data_publicacao,
                    documento_edital_id, edital_retificado_id, motivo_retificacao,
                    created_at, updated_at)
                SELECT
                    v.ato_criador_id,
                    v.processo_seletivo_id,
                    CASE WHEN v.ato_criador_retifica_id IS NULL THEN 1 ELSE 2 END,
                    v.configuracao_congelada -> 'periodo' ->> 'numero',
                    v.vigente_a_partir_de,
                    (v.configuracao_congelada -> 'hashesEdital' ->> 'documentoEditalId')::uuid,
                    v.ato_criador_retifica_id,
                    v.configuracao_congelada -> 'retificacao' ->> 'motivo',
                    v.vigente_a_partir_de,
                    NULL
                FROM selecao.versoes_configuracao v
                ORDER BY v.processo_seletivo_id, v.numero_versao;
                """);
        }
    }
}
