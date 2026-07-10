using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAtoNormativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ato_normativo",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orgao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serie = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    congela_configuracao = table.Column<bool>(type: "boolean", nullable: false),
                    efeito_irreversivel = table.Column<bool>(type: "boolean", nullable: false),
                    data_publicacao = table.Column<DateOnly>(type: "date", nullable: false),
                    documento_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    assinante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versao_invocada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    versao_invocada_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ato_normativo", x => x.id);
                    table.CheckConstraint("ck_ato_normativo_ano_positivo", "ano > 0");
                    table.CheckConstraint("ck_ato_normativo_documento_hash", "documento_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_ato_normativo_versao_completa", "(versao_invocada_id IS NULL AND versao_invocada_hash IS NULL) OR (versao_invocada_id IS NOT NULL AND versao_invocada_hash IS NOT NULL)");
                    table.CheckConstraint("ck_ato_normativo_versao_hash", "versao_invocada_hash IS NULL OR versao_invocada_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_ato_normativo_versao_id_nao_zero", "versao_invocada_id IS NULL OR versao_invocada_id <> '00000000-0000-0000-0000-000000000000'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ato_normativo_numeracao",
                schema: "publicacoes",
                table: "ato_normativo",
                columns: new[] { "orgao", "serie", "ano", "numero" });

            // Enforcement de banco do append-only (ADR-0063). A entidade forense
            // não expõe mutadores e nenhum handler chama Update/Remove — o trigger
            // fecha a última brecha: um UPDATE/DELETE cru fora do agregado. O ato
            // publicado é prova legal; corromper o passado documental é bloqueado
            // pelo próprio banco, não só por convenção de código.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_ato_normativo_somente_insercao
                    BEFORE UPDATE OR DELETE ON publicacoes.ato_normativo
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_ato_normativo_somente_insercao();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_ato_normativo_somente_insercao ON publicacoes.ato_normativo;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_ato_normativo_somente_insercao();");

            migrationBuilder.DropTable(
                name: "ato_normativo",
                schema: "publicacoes");
        }
    }
}
