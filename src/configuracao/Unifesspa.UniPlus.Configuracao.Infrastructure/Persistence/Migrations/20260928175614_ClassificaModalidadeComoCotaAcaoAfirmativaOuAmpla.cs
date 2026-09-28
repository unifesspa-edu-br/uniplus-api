using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassificaModalidadeComoCotaAcaoAfirmativaOuAmpla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_modalidade_natureza_legal",
                schema: "configuracao",
                table: "modalidade");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"),
                column: "natureza_legal",
                value: "ACAO_AFIRMATIVA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000011"),
                column: "natureza_legal",
                value: "ACAO_AFIRMATIVA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000012"),
                column: "natureza_legal",
                value: "ACAO_AFIRMATIVA");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000013"),
                column: "natureza_legal",
                value: "ACAO_AFIRMATIVA");

            // SUPLEMENTAR e OUTRA_MODALIDADE deixam de existir. As modalidades semeadas que as
            // usavam ganharam natureza nova acima; uma modalidade cadastrada por admin com um
            // desses tokens não tem natureza a deduzir — OUTRA_MODALIDADE podia ser qualquer
            // coisa —, e decidir se ela é cota, ação afirmativa ou ampla concorrência é de quem
            // cadastra. Ela sai, como em MigraCodigoTipoDocumentoParaFormatoFechado, e o
            // ambiente recadastra; sem isso, o CHECK abaixo recusaria a migration inteira.
            // O DELETE alcança também as linhas soft-deleted: o CHECK vale para a tabela toda.
            migrationBuilder.Sql(
                "DELETE FROM configuracao.modalidade WHERE natureza_legal IN ('SUPLEMENTAR', 'OUTRA_MODALIDADE');");

            migrationBuilder.AddCheckConstraint(
                name: "ck_modalidade_natureza_legal",
                schema: "configuracao",
                table: "modalidade",
                sql: "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'ACAO_AFIRMATIVA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_modalidade_natureza_legal",
                schema: "configuracao",
                table: "modalidade");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000010"),
                column: "natureza_legal",
                value: "OUTRA_MODALIDADE");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000011"),
                column: "natureza_legal",
                value: "OUTRA_MODALIDADE");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000012"),
                column: "natureza_legal",
                value: "SUPLEMENTAR");

            migrationBuilder.UpdateData(
                schema: "configuracao",
                table: "modalidade",
                keyColumn: "id",
                keyValue: new Guid("70da1000-0000-7000-8000-000000000013"),
                column: "natureza_legal",
                value: "SUPLEMENTAR");

            // Simétrico ao Up: ACAO_AFIRMATIVA não existe no vocabulário anterior, e uma
            // modalidade cadastrada com ela depois do Up não tem natureza antiga a restaurar.
            migrationBuilder.Sql(
                "DELETE FROM configuracao.modalidade WHERE natureza_legal = 'ACAO_AFIRMATIVA';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_modalidade_natureza_legal",
                schema: "configuracao",
                table: "modalidade",
                sql: "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'SUPLEMENTAR', 'OUTRA_MODALIDADE')");
        }
    }
}
