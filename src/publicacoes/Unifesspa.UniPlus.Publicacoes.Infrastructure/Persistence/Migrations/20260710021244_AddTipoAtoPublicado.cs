using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoAtoPublicado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "publicacoes");

            // Pré-requisito da exclusion constraint criada ao fim desta migration:
            // sem btree_gist o operador `=` de text não tem classe GIST (ADR-0060).
            // Em dev a extensão vem do init-db; nos demais ambientes — inclusive nos
            // containers efêmeros dos testes de integração, que não executam o
            // init-db — ela nasce aqui. `IF NOT EXISTS` torna a criação idempotente.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_headers_json = table.Column<string>(type: "jsonb", nullable: true),
                    response_body_cipher = table.Column<byte[]>(type: "bytea", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_ato_publicado",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    congela_configuracao = table.Column<bool>(type: "boolean", nullable: false),
                    unico_por_objeto = table.Column<bool>(type: "boolean", nullable: false),
                    efeito_irreversivel = table.Column<bool>(type: "boolean", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_ato_publicado", x => x.id);
                    table.CheckConstraint("ck_tipo_ato_publicado_codigo_formato", "codigo ~ '^[A-Z]+(_[A-Z]+)*$'");
                    table.CheckConstraint("ck_tipo_ato_publicado_vigencia", "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio");
                });

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                schema: "publicacoes",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                schema: "publicacoes",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipo_ato_publicado_codigo_vivo",
                schema: "publicacoes",
                table: "tipo_ato_publicado",
                column: "codigo",
                filter: "is_deleted = false");

            // Duas versões vivas do mesmo código não podem valer no mesmo dia: a
            // pergunta "o que este código significava nesta data" tem de ter uma
            // resposta. Um guard no handler não sustenta a invariante — entre a
            // consulta e o SaveChanges cabe uma transação concorrente. A janela é
            // semiaberta `[inicio, fim)`, então encerrar uma versão no dia em que a
            // sucessora começa é aceito. `vigencia_fim` nula produz `[inicio,)`.
            //
            // EXCLUDE não é expressável pelo model builder do EF Core, então vive em
            // SQL cru e não entra no ModelSnapshot: um squash de migrations a
            // descartaria em silêncio. É o que o teste de persistência sobre
            // pg_constraint existe para impedir.
            migrationBuilder.Sql(
                """
                ALTER TABLE publicacoes.tipo_ato_publicado
                ADD CONSTRAINT ex_tipo_ato_publicado_codigo_vigencia
                EXCLUDE USING gist (
                    codigo WITH =,
                    daterange(vigencia_inicio, vigencia_fim, '[)') WITH &&
                ) WHERE (is_deleted = false)
                DEFERRABLE INITIALLY IMMEDIATE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A exclusion constraint cai junto com a tabela. A extensão btree_gist
            // permanece: é do banco, compartilhada pelos demais schemas.
            migrationBuilder.DropTable(
                name: "idempotency_cache",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "tipo_ato_publicado",
                schema: "publicacoes");
        }
    }
}
