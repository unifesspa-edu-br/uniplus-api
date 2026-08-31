using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDominioFechadoDaCategoriaDoTipoDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A categoria deixa de ser vocabulário fechado em código e passa a ser o
            // código de um cadastro: o CHECK que a prendia aos sete tokens do enum
            // recusaria qualquer categoria criada pelo CEPS. Nenhum dado muda — o
            // conteúdo da coluna já era o próprio código —, e a proteção de forma
            // entra no lugar da de conjunto.
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_categoria",
                schema: "configuracao",
                table: "tipo_documento");

            // O teto sobe de 30 para 50 acompanhando o código no cadastro de
            // categorias: dimensionado por baixo, um código legítimo de 31 caracteres
            // seria aceito no cadastro e estouraria aqui como erro de banco.
            migrationBuilder.AlterColumn<string>(
                name: "categoria",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_categoria_formato",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "categoria ~ '^[A-Z][A-Z0-9_]{1,49}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O rollback só é aplicável enquanto nenhum tipo de documento usar
            // categoria fora dos sete tokens antigos ou com mais de 30 caracteres:
            // depois disso o CHECK e o limite recusam o dado existente. Falhar é
            // preferível a truncar categoria ou apagar linha.
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipo_documento_categoria_formato",
                schema: "configuracao",
                table: "tipo_documento");

            migrationBuilder.AlterColumn<string>(
                name: "categoria",
                schema: "configuracao",
                table: "tipo_documento",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipo_documento_categoria",
                schema: "configuracao",
                table: "tipo_documento",
                sql: "categoria IN ('IDENTIFICACAO', 'ESCOLARIDADE', 'RENDA', 'RACA_ETNIA', 'SAUDE', 'RESIDENCIA', 'OUTROS')");
        }
    }
}
