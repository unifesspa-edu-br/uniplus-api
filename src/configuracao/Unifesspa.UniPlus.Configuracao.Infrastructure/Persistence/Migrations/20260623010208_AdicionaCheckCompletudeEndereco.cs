using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCheckCompletudeEndereco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_local_oferta_endereco_completo",
                table: "local_oferta",
                sql: "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_campus_endereco_completo",
                table: "campus",
                sql: "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: o revert se dá por nova migration.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
