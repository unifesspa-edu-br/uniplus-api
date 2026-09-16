using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaraterAdmitidoPorTipoDeEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Colunas nascem permissivas para que as linhas já semeadas satisfaçam o CHECK
            // assim que ele entra; o valor de cada tipo é declarado logo abaixo, e o DEFAULT
            // é derrubado ao final — quem declara o que um tipo admite é o cadastro, não o
            // banco.
            migrationBuilder.AddColumn<bool>(
                name: "admite_eliminacao",
                schema: "configuracao",
                table: "tipos_etapa",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "admite_pontuacao",
                schema: "configuracao",
                table: "tipos_etapa",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipos_etapa_carater_admitido",
                schema: "configuracao",
                table: "tipos_etapa",
                sql: "admite_pontuacao OR admite_eliminacao");

            // Análise documental e banca de heteroidentificação conferem conformidade: aprovam
            // ou reprovam, sem atribuir nota que entre na média. Os demais tipos do vocabulário
            // semeado — prova objetiva, redação, entrevista, análise de histórico e nota do
            // Enem — produzem nota, e continuam podendo também eliminar por nota mínima.
            //
            // O WHERE alcança só o que a carga inicial trouxe sem autor: tipo criado depois pela
            // tela administrativa tem dono, declarou os próprios sinalizadores, e nenhuma
            // migration pode sobrescrever essa escolha.
            migrationBuilder.Sql("""
                UPDATE configuracao.tipos_etapa
                   SET admite_pontuacao = false
                 WHERE codigo IN ('ANALISE_DOCUMENTAL', 'BANCA_HETEROIDENTIFICACAO')
                   AND created_by IS NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE configuracao.tipos_etapa
                    ALTER COLUMN admite_pontuacao DROP DEFAULT,
                    ALTER COLUMN admite_eliminacao DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipos_etapa_carater_admitido",
                schema: "configuracao",
                table: "tipos_etapa");

            migrationBuilder.DropColumn(
                name: "admite_eliminacao",
                schema: "configuracao",
                table: "tipos_etapa");

            migrationBuilder.DropColumn(
                name: "admite_pontuacao",
                schema: "configuracao",
                table: "tipos_etapa");
        }
    }
}
