using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeclaraNotaDeOrigemNoEnemNoTipoDeEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A coluna nasce com DEFAULT só para dar valor às linhas já existentes; o atributo
            // do tipo de nota do ENEM é ligado logo abaixo, e o DEFAULT é derrubado ao final.
            migrationBuilder.AddColumn<bool>(
                name: "nota_de_origem_no_enem",
                schema: "configuracao",
                table: "tipos_etapa",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "Nota das etapas deste tipo vem do ENEM; definido só pela carga do cadastro, nunca pela API.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipos_etapa_nota_de_origem_no_enem_admite_pontuacao",
                schema: "configuracao",
                table: "tipos_etapa",
                sql: "NOT nota_de_origem_no_enem OR admite_pontuacao");

            // O WHERE alcança só o tipo que a carga inicial trouxe sem autor. Se esse tipo
            // estiver sem pontuação, o CHECK acima recusa o UPDATE e a migration falha. Como ela
            // roda no startup do host, a versão nova não sobe: a pontuação tem de ser ligada pela
            // API da versão anterior, com autor, antes do deploy, e não aqui.
            migrationBuilder.Sql("""
                UPDATE configuracao.tipos_etapa
                   SET nota_de_origem_no_enem = true
                 WHERE codigo = 'NOTA_ENEM'
                   AND created_by IS NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE configuracao.tipos_etapa
                    ALTER COLUMN nota_de_origem_no_enem DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipos_etapa_nota_de_origem_no_enem_admite_pontuacao",
                schema: "configuracao",
                table: "tipos_etapa");

            migrationBuilder.DropColumn(
                name: "nota_de_origem_no_enem",
                schema: "configuracao",
                table: "tipos_etapa");
        }
    }
}
