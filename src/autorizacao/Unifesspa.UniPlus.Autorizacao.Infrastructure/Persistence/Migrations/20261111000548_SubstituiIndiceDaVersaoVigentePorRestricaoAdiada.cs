using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiIndiceDaVersaoVigentePorRestricaoAdiada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_versoes_do_perfil_vigente",
                schema: "autorizacao",
                table: "versoes_do_perfil");

            // A troca de vigência grava o encerramento da anterior e a entrada da nova na mesma transação, em ordem de
            // chave que o EF não controla. A exclusão adiada confere uma só vigente por perfil no commit.
            migrationBuilder.Sql(
                """
                ALTER TABLE autorizacao.versoes_do_perfil
                ADD CONSTRAINT ex_versoes_do_perfil_vigente_unica
                EXCLUDE USING btree (perfil_id WITH =) WHERE (situacao = 'vigente')
                DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE autorizacao.versoes_do_perfil DROP CONSTRAINT ex_versoes_do_perfil_vigente_unica;");

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_perfil_vigente",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "situacao = 'vigente'");
        }
    }
}
