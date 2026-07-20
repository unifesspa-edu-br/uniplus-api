using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardinalidadeQualificadaFolha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_nos_exigencia_tipo_campos_coerentes",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.AddColumn<int>(
                name: "chave_distincao",
                schema: "selecao",
                table: "nos_exigencia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_referencia",
                schema: "selecao",
                table: "nos_exigencia",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ocorrencias_esperadas",
                schema: "selecao",
                table: "nos_exigencia",
                type: "jsonb",
                nullable: true);

            // Backfill obrigatório antes do novo CHECK: o schema anterior exigia
            // quantidade_minima IS NULL em folha (tipo=1); o novo CHECK exige NOT NULL
            // (cardinalidade de apresentações, Story #921) — sem isto, o CHECK abaixo
            // rejeita a migração em qualquer banco com folhas já persistidas.
            migrationBuilder.Sql(
                "UPDATE selecao.nos_exigencia SET quantidade_minima = 1 WHERE tipo = 1 AND quantidade_minima IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_nos_exigencia_chave_distincao_coerente",
                schema: "selecao",
                table: "nos_exigencia",
                sql: "(chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (chave_distincao IS NOT NULL AND chave_distincao IN (1, 2) AND data_referencia IS NOT NULL AND ocorrencias_esperadas IS NULL) OR (chave_distincao IS NOT NULL AND chave_distincao = 3 AND data_referencia IS NULL AND (ocorrencias_esperadas IS NULL OR jsonb_array_length(ocorrencias_esperadas) > 0))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_nos_exigencia_tipo_campos_coerentes",
                schema: "selecao",
                table: "nos_exigencia",
                sql: "(tipo = 1 AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NOT NULL AND consequencia IS NULL) OR (tipo = 2 AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL AND chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (tipo = 3 AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL AND chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_nos_exigencia_chave_distincao_coerente",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_nos_exigencia_tipo_campos_coerentes",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.DropColumn(
                name: "chave_distincao",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.DropColumn(
                name: "data_referencia",
                schema: "selecao",
                table: "nos_exigencia");

            migrationBuilder.DropColumn(
                name: "ocorrencias_esperadas",
                schema: "selecao",
                table: "nos_exigencia");

            // Espelha o backfill do Up: o CHECK antigo exige quantidade_minima IS NULL em
            // folha — sem zerar antes, o Down falha em qualquer banco com folhas cardinalizadas.
            migrationBuilder.Sql(
                "UPDATE selecao.nos_exigencia SET quantidade_minima = NULL WHERE tipo = 1;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_nos_exigencia_tipo_campos_coerentes",
                schema: "selecao",
                table: "nos_exigencia",
                sql: "(tipo = 1 AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NULL AND consequencia IS NULL) OR (tipo = 2 AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL) OR (tipo = 3 AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL)");
        }
    }
}
