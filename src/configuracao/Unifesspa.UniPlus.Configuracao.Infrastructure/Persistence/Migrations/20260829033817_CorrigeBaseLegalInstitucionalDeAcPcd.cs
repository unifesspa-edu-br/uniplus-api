using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeBaseLegalInstitucionalDeAcPcd : Migration
    {
        // base_legal é uma das duas colunas (com descricao) que o cadastro deixa um admin
        // editar numa modalidade do catálogo legal fixo. Corrigir o texto do seed não pode
        // descartar em silêncio uma edição administrativa legítima — nem deixar a auditoria
        // da linha (updated_by/updated_at) descrevendo um valor que não existe mais. Por isso
        // o UPDATE é condicionado ao texto semeado, em vez do UpdateData por id que o
        // scaffold gera: a linha que alguém já reescreveu fica como está.
        private const string BaseLegalSemeadaOriginal = "Lei 12.711/2012 (red. Lei 14.723/2023)";
        private const string BaseLegalInstitucional =
            "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)";
        private const string IdAcPcd = "70da1000-0000-7000-8000-000000000010";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            AtualizarBaseLegalSeAindaFor(migrationBuilder, de: BaseLegalSemeadaOriginal, para: BaseLegalInstitucional);

        /// <inheritdoc />
        /// <remarks>
        /// A reversão é simétrica, e tem um limite que nenhuma migration resolve: se um admin
        /// tivesse escrito, por conta própria, exatamente o texto institucional ANTES desta
        /// migration, o <c>Up</c> corretamente não a tocaria — mas o <c>Down</c> não distingue
        /// esse valor do que ele mesmo gravou, e o devolveria ao texto do seed. Distinguir
        /// exigiria a migration registrar em algum lugar quais linhas ela alterou, estado que
        /// ela não tem. Fica assim de propósito: o cenário exige coincidência exata de texto,
        /// e <c>Down</c> é ferramenta de desenvolvimento, não de operação.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            AtualizarBaseLegalSeAindaFor(migrationBuilder, de: BaseLegalInstitucional, para: BaseLegalSemeadaOriginal);

        private static void AtualizarBaseLegalSeAindaFor(MigrationBuilder migrationBuilder, string de, string para)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                $"""
                UPDATE configuracao.modalidade
                   SET base_legal = '{para.Replace("'", "''", StringComparison.Ordinal)}'
                 WHERE id = '{IdAcPcd}'
                   AND base_legal = '{de.Replace("'", "''", StringComparison.Ordinal)}';
                """);
        }
    }
}
