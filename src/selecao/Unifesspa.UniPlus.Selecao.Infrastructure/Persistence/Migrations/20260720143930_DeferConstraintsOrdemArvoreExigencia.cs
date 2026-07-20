using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeferConstraintsOrdemArvoreExigencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_nos_exigencia_irmaos_ordem",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.DropIndex(
                name: "ux_nos_exigencia_raiz_ordem",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.CreateIndex(
                name: "ix_nos_exigencia_no_pai_id",
                schema: "selecao",
                table: "nos_exigencia",
                column: "no_pai_id");

            migrationBuilder.CreateIndex(
                name: "ix_nos_exigencia_processo_seletivo_id",
                schema: "selecao",
                table: "nos_exigencia",
                column: "processo_seletivo_id");

            // Pré-requisito das exclusion constraints abaixo: sem btree_gist o operador `=`
            // de uuid/int não tem classe GIST. `nos_exigencia` já compartilha o mesmo banco
            // físico do schema `publicacoes` (só schemas isolam os módulos, não bancos —
            // ver 20260710021244_AddTipoAtoPublicado, que já criou a extensão), mas
            // `IF NOT EXISTS` mantém esta migration autossuficiente mesmo rodando isolada
            // (ex.: containers efêmeros de teste de integração que só aplicam o DbContext
            // de Selecao).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Substituem os dois índices únicos filtrados dropados acima (issue #943): um
            // índice único comum é verificado por-statement, e nos_exigencia é
            // auto-referenciada (no_pai_id → id, Restrict) — substituir uma árvore com grupo
            // E/OU faz o EF Core apagar bottom-up (netos → raiz) mas inserir a raiz NOVA
            // antes da antiga sair do banco, dentro do mesmo SaveChangesAsync. A ordem
            // Added-antes-Deleted do EF Core não conhece este índice alternativo, e a
            // checagem imediata do índice comum recusava a raiz nova com a MESMA ordem da
            // antiga — mesmo a transação terminando num estado final válido. DEFERRABLE
            // INITIALLY DEFERRED adia a checagem para o COMMIT, quando a exclusão já se
            // efetivou. Mesmo padrão de ex_tipo_ato_publicado_codigo_vigencia
            // (TipoAtoPublicadoConfiguration.cs) — EXCLUDE com predicado parcial não é
            // modelável pelo Fluent API do EF Core, então vive só aqui e no
            // ModelSnapshot (não em NoExigenciaConfiguration.cs).
            migrationBuilder.Sql(
                """
                ALTER TABLE selecao.nos_exigencia
                ADD CONSTRAINT ex_nos_exigencia_irmaos_ordem
                EXCLUDE USING gist (
                    no_pai_id WITH =,
                    ordem WITH =
                ) WHERE (no_pai_id IS NOT NULL)
                DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE selecao.nos_exigencia
                ADD CONSTRAINT ex_nos_exigencia_raiz_ordem
                EXCLUDE USING gist (
                    processo_seletivo_id WITH =,
                    ordem WITH =
                ) WHERE (no_pai_id IS NULL)
                DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE selecao.nos_exigencia DROP CONSTRAINT ex_nos_exigencia_irmaos_ordem;");

            migrationBuilder.Sql(
                "ALTER TABLE selecao.nos_exigencia DROP CONSTRAINT ex_nos_exigencia_raiz_ordem;");

            migrationBuilder.DropIndex(
                name: "ix_nos_exigencia_no_pai_id",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.DropIndex(
                name: "ix_nos_exigencia_processo_seletivo_id",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.CreateIndex(
                name: "ux_nos_exigencia_irmaos_ordem",
                schema: "selecao",
                table: "nos_exigencia",
                columns: new[] { "no_pai_id", "ordem" },
                unique: true,
                filter: "no_pai_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_nos_exigencia_raiz_ordem",
                schema: "selecao",
                table: "nos_exigencia",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true,
                filter: "no_pai_id IS NULL");
        }
    }
}
