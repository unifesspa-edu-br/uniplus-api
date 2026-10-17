using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// O documento a publicar passa a declarar, no início do envio, para que serve: edital, anexo do
    /// edital ou documento de ato. A coluna é obrigatória e imutável.
    /// </summary>
    /// <remarks>
    /// Os documentos que já existem são todos do edital, e por isso entram com a finalidade Edital;
    /// o valor padrão serve só para adicionar a coluna e sai em seguida, para que o envio novo não
    /// possa omiti-la.
    /// </remarks>
    public partial class AdicionaAFinalidadeAoDocumentoAPublicar : Migration
    {
        private const string FinalidadeEdital = "1";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql($"""
                ALTER TABLE selecao.documentos_a_publicar
                    ADD COLUMN finalidade integer NOT NULL DEFAULT {FinalidadeEdital};
                ALTER TABLE selecao.documentos_a_publicar
                    ALTER COLUMN finalidade DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "finalidade",
                schema: "selecao",
                table: "documentos_a_publicar");
        }
    }
}
