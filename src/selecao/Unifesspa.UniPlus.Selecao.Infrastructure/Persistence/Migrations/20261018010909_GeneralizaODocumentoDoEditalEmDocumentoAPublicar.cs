using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// O documento do edital passa a ser um caso do documento a publicar: a mesma tabela guarda o
    /// PDF do edital, o anexo do edital e o documento de um ato, cada um com o nome e o formato
    /// (PDF, DOCX ou ODT). Renomeia a tabela, a chave primária, a chave estrangeira, o índice e a
    /// retenção (funções e gatilhos), que só mudam de nome: a regra de que o confirmado não se
    /// remove nem se desconfirma é a mesma.
    /// </summary>
    /// <remarks>
    /// As colunas novas são obrigatórias. Os registros que já existem são PDFs — o único formato
    /// que a validação anterior aceitava —, e por isso entram com o formato PDF e o nome
    /// <c>edital.pdf</c>; o valor padrão serve só para adicionar a coluna e sai em seguida, para
    /// que o envio novo não possa omitir o nome nem o formato.
    /// </remarks>
    public partial class GeneralizaODocumentoDoEditalEmDocumentoAPublicar : Migration
    {
        private const string FormatoPdf = "3";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            RemoveARetencao(migrationBuilder, "documentos_edital");

            migrationBuilder.Sql("ALTER TABLE selecao.documentos_edital RENAME TO documentos_a_publicar;");
            migrationBuilder.Sql("ALTER TABLE selecao.documentos_a_publicar RENAME CONSTRAINT pk_documentos_edital TO pk_documentos_a_publicar;");
            migrationBuilder.Sql("ALTER TABLE selecao.documentos_a_publicar RENAME CONSTRAINT fk_documentos_edital_processos_seletivos_processo_seletivo_id TO fk_documentos_a_publicar_processos_seletivos_processo_seletivo;");
            migrationBuilder.Sql("ALTER INDEX selecao.ix_documentos_edital_processo_recentes RENAME TO ix_documentos_a_publicar_processo_recentes;");

            migrationBuilder.Sql($"""
                ALTER TABLE selecao.documentos_a_publicar
                    ADD COLUMN nome_arquivo character varying(120) NOT NULL DEFAULT 'edital.pdf',
                    ADD COLUMN formato integer NOT NULL DEFAULT {FormatoPdf};
                ALTER TABLE selecao.documentos_a_publicar
                    ALTER COLUMN nome_arquivo DROP DEFAULT,
                    ALTER COLUMN formato DROP DEFAULT;
                """);

            CriaARetencao(migrationBuilder, "documentos_a_publicar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            RemoveARetencao(migrationBuilder, "documentos_a_publicar");

            migrationBuilder.Sql("ALTER TABLE selecao.documentos_a_publicar DROP COLUMN nome_arquivo, DROP COLUMN formato;");

            migrationBuilder.Sql("ALTER INDEX selecao.ix_documentos_a_publicar_processo_recentes RENAME TO ix_documentos_edital_processo_recentes;");
            migrationBuilder.Sql("ALTER TABLE selecao.documentos_a_publicar RENAME CONSTRAINT fk_documentos_a_publicar_processos_seletivos_processo_seletivo TO fk_documentos_edital_processos_seletivos_processo_seletivo_id;");
            migrationBuilder.Sql("ALTER TABLE selecao.documentos_a_publicar RENAME CONSTRAINT pk_documentos_a_publicar TO pk_documentos_edital;");
            migrationBuilder.Sql("ALTER TABLE selecao.documentos_a_publicar RENAME TO documentos_edital;");

            CriaARetencao(migrationBuilder, "documentos_edital");
        }

        private static void RemoveARetencao(MigrationBuilder migrationBuilder, string tabela)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{tabela}_bloqueia_truncate ON selecao.{tabela};");
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{tabela}_retencao_update ON selecao.{tabela};");
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{tabela}_retencao_delete ON selecao.{tabela};");
            migrationBuilder.Sql($"DROP FUNCTION IF EXISTS selecao.fn_{tabela}_bloqueia_truncate();");
            migrationBuilder.Sql($"DROP FUNCTION IF EXISTS selecao.fn_{tabela}_retencao();");
        }

        /// <summary>
        /// A retenção do documento confirmado, que é a evidência do que foi publicado. O RAISE
        /// declara CONSTRAINT/TABLE/SCHEMA para que <c>PostgresException.ConstraintName</c> chegue
        /// preenchida à aplicação (ADR-0102). Só o confirmado (<c>status = 1</c>) é retido: o
        /// pendente é um envio que nunca se completou, e a limpeza dos vencidos continua possível.
        /// A trava de TRUNCATE existe porque ele não dispara gatilho de linha.
        /// </summary>
        private static void CriaARetencao(MigrationBuilder migrationBuilder, string tabela)
        {
            migrationBuilder.Sql($$"""
                CREATE OR REPLACE FUNCTION selecao.fn_{{tabela}}_retencao()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'Documento confirmado é a evidência do que foi publicado: não se remove nem se desconfirma.'
                        USING ERRCODE = 'check_violation',
                              CONSTRAINT = 'ck_{{tabela}}_confirmado_retido',
                              TABLE = '{{tabela}}',
                              SCHEMA = 'selecao';
                END;
                $$;
                """);

            migrationBuilder.Sql($"""
                CREATE TRIGGER trg_{tabela}_retencao_delete
                BEFORE DELETE ON selecao.{tabela}
                FOR EACH ROW
                WHEN (OLD.status = 1)
                EXECUTE FUNCTION selecao.fn_{tabela}_retencao();
                """);

            migrationBuilder.Sql($"""
                CREATE TRIGGER trg_{tabela}_retencao_update
                BEFORE UPDATE ON selecao.{tabela}
                FOR EACH ROW
                WHEN (OLD.status = 1 AND NEW.status <> 1)
                EXECUTE FUNCTION selecao.fn_{tabela}_retencao();
                """);

            migrationBuilder.Sql($$"""
                CREATE OR REPLACE FUNCTION selecao.fn_{{tabela}}_bloqueia_truncate()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'TRUNCATE apagaria os documentos confirmados, que são evidência do publicado.'
                        USING ERRCODE = 'check_violation',
                              CONSTRAINT = 'ck_{{tabela}}_confirmado_retido',
                              TABLE = '{{tabela}}',
                              SCHEMA = 'selecao';
                END;
                $$;
                """);

            migrationBuilder.Sql($"""
                CREATE TRIGGER trg_{tabela}_bloqueia_truncate
                BEFORE TRUNCATE ON selecao.{tabela}
                FOR EACH STATEMENT
                EXECUTE FUNCTION selecao.fn_{tabela}_bloqueia_truncate();
                """);
        }
    }
}
