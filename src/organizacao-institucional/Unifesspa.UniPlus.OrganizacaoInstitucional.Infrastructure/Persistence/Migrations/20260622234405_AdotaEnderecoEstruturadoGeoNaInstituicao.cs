using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdotaEnderecoEstruturadoGeoNaInstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "endereco_sede",
                table: "instituicao");

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "instituicao",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "instituicao",
                type: "character(8)",
                fixedLength: true,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_codigo_ibge",
                table: "instituicao",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_nome",
                table: "instituicao",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_uf",
                table: "instituicao",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "instituicao",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "endereco_display_atualizado_em",
                table: "instituicao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_distrito",
                table: "instituicao",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "endereco_latitude",
                table: "instituicao",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "instituicao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "endereco_longitude",
                table: "instituicao",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_nivel_resolucao",
                table: "instituicao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "instituicao",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_origem",
                table: "instituicao",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_instituicao_endereco_cidade_coerente",
                table: "instituicao",
                sql: "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf = cidade_uf)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Reverter aqui destruiria o endereço
            // estruturado capturado e recriaria um endereco_sede texto-livre vazio
            // — perda silenciosa de dado num `database update <baseline>`, proibido.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
