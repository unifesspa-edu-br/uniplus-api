using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeBaseLegalDePcdPuro : Migration
    {
        // PCD_PURO é a reserva do processo que não oferta as cotas federais, instituída pela
        // Res. 64/2015-CONSEPE. É norma distinta da que sustenta AC_PCD — a reserva dentro da
        // ampla concorrência nos certames da Lei 12.711 —, e as duas modalidades deixam de
        // compartilhar a base legal por isso.
        //
        // O UPDATE é condicionado ao texto semeado, e não o UpdateData por id que o scaffold
        // gera: base_legal é uma das colunas que o cadastro deixa um admin editar, e
        // sobrescrever uma edição legítima deixaria a auditoria da linha descrevendo um valor
        // que não existe mais. A linha que alguém já reescreveu fica como está.
        private const string BaseLegalSemeadaOriginal =
            "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)";
        private const string BaseLegalDaReservaSemCotasFederais =
            "Res. Unifesspa 64/2015-CONSEPE (reserva de vaga para pessoa com deficiência)";
        private const string IdPcdPuro = "70da1000-0000-7000-8000-000000000011";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            AtualizarBaseLegalSeAindaFor(migrationBuilder, de: BaseLegalSemeadaOriginal, para: BaseLegalDaReservaSemCotasFederais);

        /// <inheritdoc />
        /// <remarks>
        /// Simétrica ao <c>Up</c>, com o mesmo limite da correção equivalente de <c>AC_PCD</c>:
        /// se um admin tivesse escrito, por conta própria, exatamente o texto da Res. 64/2015
        /// antes desta migration, o <c>Up</c> corretamente não a tocaria, mas o <c>Down</c> não
        /// distingue esse valor do que ele mesmo gravou. Exigiria a migration registrar quais
        /// linhas alterou, estado que ela não tem — e <c>Down</c> é ferramenta de
        /// desenvolvimento, não de operação.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            AtualizarBaseLegalSeAindaFor(migrationBuilder, de: BaseLegalDaReservaSemCotasFederais, para: BaseLegalSemeadaOriginal);

        private static void AtualizarBaseLegalSeAindaFor(MigrationBuilder migrationBuilder, string de, string para)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                $"""
                UPDATE configuracao.modalidade
                   SET base_legal = '{para.Replace("'", "''", StringComparison.Ordinal)}'
                 WHERE id = '{IdPcdPuro}'
                   AND base_legal = '{de.Replace("'", "''", StringComparison.Ordinal)}';
                """);
        }
    }
}
