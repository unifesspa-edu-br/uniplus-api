using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O módulo nasce sem entidades, então o EF não emite operação alguma: um
            // modelo vazio não materializa o schema declarado em HasDefaultSchema.
            // O EnsureSchema é explícito para que o schema exista desde o primeiro boot
            // — as tabelas do cadastro de tipos de ato, do ato normativo e do vínculo
            // ato-entidade chegam nas migrations seguintes.
            migrationBuilder.EnsureSchema(name: "publicacoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem operação: a tabela `__EFMigrationsHistory` deste módulo vive no próprio
            // schema `publicacoes`, então derrubar o schema no rollback removeria o registro
            // de que a migration existiu. Reverter para zero é feito por migration reversa,
            // não destruindo o schema — alinhado ao padrão de não destruir schema ou dados.
        }
    }
}
