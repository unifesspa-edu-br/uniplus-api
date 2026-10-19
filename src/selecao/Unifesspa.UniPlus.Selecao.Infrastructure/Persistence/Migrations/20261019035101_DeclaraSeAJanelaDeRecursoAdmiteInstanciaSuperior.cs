using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Cada janela de recurso, da fase e da etapa, passa a declarar se admite recurso em
    /// instância superior.
    /// </summary>
    /// <remarks>
    /// A coluna é obrigatória. Os registros que já existem recebem "admite", que é o que a
    /// configuração anterior dizia ao deixar o tempo de bloqueio superior declarável; o valor
    /// padrão serve só para adicionar a coluna e sai em seguida, para que a gravação nova não
    /// possa omitir a declaração.
    /// </remarks>
    public partial class DeclaraSeAJanelaDeRecursoAdmiteInstanciaSuperior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "admite_instancia_superior",
                schema: "selecao",
                table: "regras_recurso_fase",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "admite_instancia_superior",
                schema: "selecao",
                table: "recursos_da_etapa",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("ALTER TABLE selecao.regras_recurso_fase ALTER COLUMN admite_instancia_superior DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE selecao.recursos_da_etapa ALTER COLUMN admite_instancia_superior DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "admite_instancia_superior",
                schema: "selecao",
                table: "regras_recurso_fase");

            migrationBuilder.DropColumn(
                name: "admite_instancia_superior",
                schema: "selecao",
                table: "recursos_da_etapa");
        }
    }
}
