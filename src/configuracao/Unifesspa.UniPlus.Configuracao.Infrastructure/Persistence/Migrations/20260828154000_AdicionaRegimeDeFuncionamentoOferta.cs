using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Acrescenta o regime de funcionamento (UNI-REQ-0138, ADR-0128) como coluna
    /// obrigatória de domínio fechado, com o CHECK do vocabulário e o CHECK da
    /// compatibilidade com o regime de turno.
    /// </summary>
    /// <remarks>
    /// A coluna nasce <c>NOT NULL</c> <b>sem</b> <c>defaultValue</c>, de
    /// propósito: um default preencheria toda oferta preexistente com um valor
    /// que ninguém declarou, e o regime de funcionamento não é derivável do
    /// regime de turno, dos turnos, do formato pedagógico nem do programa. Sobre
    /// tabela com linhas, o <c>ALTER TABLE</c> falha com <c>23502</c> — a recusa
    /// é o comportamento desejado: o dado preexistente precisa ser classificado
    /// explicitamente antes de a coluna existir.
    /// <para>A recusa é segura. Pela ADR-0127 as migrations rodam num Job de
    /// deploy que aborta o rollout antes de qualquer pod ser tocado, então a
    /// falha interrompe a publicação em vez de deixar o ambiente meio migrado.
    /// A alternativa de coluna anulável seguida de backfill foi descartada: ela
    /// abriria justamente a janela em que uma oferta existe sem regime de
    /// funcionamento declarado, que é o estado que este campo veio proibir.</para>
    /// </remarks>
    public partial class AdicionaRegimeDeFuncionamentoOferta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_oferta_curso_funcionamento_regime_de_turno",
                schema: "configuracao",
                table: "oferta_curso",
                sql: "(regime_de_funcionamento <> 'INTENSIVO' OR regime_de_turno = 'INTEGRAL')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_funcionamento_regime_de_turno",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_oferta_curso_regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso");

            migrationBuilder.DropColumn(
                name: "regime_de_funcionamento",
                schema: "configuracao",
                table: "oferta_curso");
        }
    }
}
