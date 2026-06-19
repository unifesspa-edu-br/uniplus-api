using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseNomeOrdenacaoGerada : Migration
    {
        private const string NomeOrdenacaoSql = "lower(translate(coalesce(nullif(nome_normalizado, ''), nome), 'àáâãäçèéêëìíîïñòóôõöùúûüÀÁÂÃÄÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜ', 'aaaaaceeeeiiiinooooouuuuAAAAACEEEEIIIINOOOOOUUUU'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_estado_nome_ordenacao;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_cidade_nome_ordenacao;");

            migrationBuilder.AddColumn<string>(
                name: "nome_ordenacao",
                table: "estado",
                type: "text",
                nullable: false,
                computedColumnSql: NomeOrdenacaoSql,
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "nome_ordenacao",
                table: "cidade",
                type: "text",
                nullable: false,
                computedColumnSql: NomeOrdenacaoSql,
                stored: true);

            migrationBuilder.Sql(
                "CREATE INDEX ix_estado_nome_ordenacao ON estado (nome_ordenacao, id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_cidade_nome_ordenacao ON cidade (nome_ordenacao, id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_cidade_nome_ordenacao;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_estado_nome_ordenacao;");

            migrationBuilder.DropColumn(
                name: "nome_ordenacao",
                table: "estado");

            migrationBuilder.DropColumn(
                name: "nome_ordenacao",
                table: "cidade");

            migrationBuilder.Sql(
                "CREATE INDEX ix_estado_nome_ordenacao ON estado (COALESCE(nome_normalizado, ''), id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_cidade_nome_ordenacao ON cidade (COALESCE(nome_normalizado, ''), id);");
        }
    }
}
