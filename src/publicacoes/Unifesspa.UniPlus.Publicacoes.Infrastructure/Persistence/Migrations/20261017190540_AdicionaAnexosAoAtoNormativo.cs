using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAnexosAoAtoNormativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anexo_do_ato",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_midia = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexo_do_ato", x => x.id);
                    table.CheckConstraint("ck_anexo_do_ato_hash", "hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_anexo_do_ato_nome", "btrim(nome) <> ''");
                    table.CheckConstraint("ck_anexo_do_ato_ordem", "ordem >= 0");
                    table.CheckConstraint("ck_anexo_do_ato_tipo_midia", "tipo_midia ~ '^[a-z0-9][a-z0-9.+_-]*/[a-z0-9][a-z0-9.+_-]*$'");
                    table.ForeignKey(
                        name: "fk_anexo_do_ato_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_anexo_do_ato_nome",
                schema: "publicacoes",
                table: "anexo_do_ato",
                columns: new[] { "ato_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anexo_do_ato_ordem",
                schema: "publicacoes",
                table: "anexo_do_ato",
                columns: new[] { "ato_id", "ordem" },
                unique: true);

            // Append-only imposto pelo banco (ADR-0063), como no ato que o anexo acompanha.
            // Trocar o hash de um anexo, ou tirá-lo da lista, desfaria a prova de que o ato
            // publicado inclui aquele arquivo.
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_anexo_do_ato_somente_insercao
                    BEFORE UPDATE OR DELETE ON publicacoes.anexo_do_ato
                    FOR EACH ROW
                    EXECUTE FUNCTION publicacoes.fn_anexo_do_ato_somente_insercao();
                """);

            // TRUNCATE não dispara trigger de linha; a função é a mesma das demais tabelas.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_anexo_do_ato_bloqueia_truncate
                    BEFORE TRUNCATE ON publicacoes.anexo_do_ato
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION publicacoes.fn_bloqueia_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_anexo_do_ato_bloqueia_truncate ON publicacoes.anexo_do_ato;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_anexo_do_ato_somente_insercao ON publicacoes.anexo_do_ato;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS publicacoes.fn_anexo_do_ato_somente_insercao();");

            migrationBuilder.DropTable(
                name: "anexo_do_ato",
                schema: "publicacoes");
        }
    }
}
