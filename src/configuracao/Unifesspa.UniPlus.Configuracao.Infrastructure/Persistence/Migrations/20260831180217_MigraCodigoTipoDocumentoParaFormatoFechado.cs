using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigraCodigoTipoDocumentoParaFormatoFechado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O código passa a ter formato fechado (^[A-Z][A-Z0-9_]{1,49}$) e a coluna
            // ganha value object com reidratação fail-fast. Qualquer linha gravada sob
            // a regra antiga — o cadastro aceitava `01`, `1`, minúsculas e acento —
            // faria toda leitura da tabela estourar, não só a leitura daquela linha, e
            // ainda barraria o CHECK abaixo.
            //
            // Derivar um código semântico a partir do nome inventaria identidade, que é
            // justamente o que UNI-REQ-0013 exige ser declarado por quem cadastra. Em vez
            // disso as linhas preexistentes saem e os ambientes recadastram — mesma
            // decisão de AdicionaCodigoTipoDeficiencia. O catálogo consolidado de tipos
            // repovoa o cadastro em seguida.
            //
            // O DELETE alcança também as linhas soft-deleted: o índice único parcial as
            // ignora, mas o CHECK e o NOT NULL valem para a tabela inteira.
            migrationBuilder.Sql("DELETE FROM configuracao.tipo_documento;");

            migrationBuilder.AlterColumn<string>(
                name: "tipo_equivalente",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_codigo_formato",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
        }

        /// <inheritdoc />
        /// <remarks>
        /// A reversão devolve o schema ao estado anterior, mas não restaura as linhas
        /// removidas no <c>Up</c> — o cadastro volta vazio.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_codigo_formato",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.AlterColumn<string>(
                name: "tipo_equivalente",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
