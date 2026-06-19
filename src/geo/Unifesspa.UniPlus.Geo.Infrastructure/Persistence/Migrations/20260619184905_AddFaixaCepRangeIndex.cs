using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaixaCepRangeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_distrito_faixa_cep_range",
                table: "distrito_faixa_cep",
                columns: new[] { "cep_inicial", "cep_final" },
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "ix_cidade_faixa_cep_range",
                table: "cidade_faixa_cep",
                columns: new[] { "cep_inicial", "cep_final" },
                filter: "vigente");

            migrationBuilder.CreateIndex(
                name: "ix_bairro_faixa_cep_range",
                table: "bairro_faixa_cep",
                columns: new[] { "cep_inicial", "cep_final" },
                filter: "vigente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_distrito_faixa_cep_range",
                table: "distrito_faixa_cep");

            migrationBuilder.DropIndex(
                name: "ix_cidade_faixa_cep_range",
                table: "cidade_faixa_cep");

            migrationBuilder.DropIndex(
                name: "ix_bairro_faixa_cep_range",
                table: "bairro_faixa_cep");
        }
    }
}
