using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdotaEnderecoEstruturadoGeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "endereco",
                table: "local_oferta");

            migrationBuilder.DropColumn(
                name: "endereco",
                table: "campus");

            // Descarta cep/latitude/longitude "pelados" do Campus (em vez de
            // renomeá-los para endereco_*): um cep isolado, sem o trio de cidade /
            // nivel_resolucao / origem do owned type, deixaria endereco_cep não-nulo
            // (sentinela de presença) e o EF materializaria um endereço PARCIAL e
            // incoerente. Sem produção (scaffolding, ADR-0096 §detalhes-4), o dado
            // dev/seed é descartado e o endereço estruturado recomeça vazio.
            migrationBuilder.DropColumn(
                name: "longitude",
                table: "campus");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "campus");

            migrationBuilder.DropColumn(
                name: "cep",
                table: "campus");

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "campus",
                type: "character(8)",
                fixedLength: true,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "endereco_latitude",
                table: "campus",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "endereco_longitude",
                table: "campus",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "local_oferta",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cep",
                table: "local_oferta",
                type: "character(8)",
                fixedLength: true,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_codigo_ibge",
                table: "local_oferta",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_nome",
                table: "local_oferta",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_uf",
                table: "local_oferta",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "local_oferta",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "endereco_display_atualizado_em",
                table: "local_oferta",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_distrito",
                table: "local_oferta",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "endereco_latitude",
                table: "local_oferta",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "local_oferta",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "endereco_longitude",
                table: "local_oferta",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_nivel_resolucao",
                table: "local_oferta",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "local_oferta",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_origem",
                table: "local_oferta",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_bairro",
                table: "campus",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_codigo_ibge",
                table: "campus",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_nome",
                table: "campus",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_cidade_uf",
                table: "campus",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_complemento",
                table: "campus",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "endereco_display_atualizado_em",
                table: "campus",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_distrito",
                table: "campus",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_logradouro",
                table: "campus",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_nivel_resolucao",
                table: "campus",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_numero",
                table: "campus",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "endereco_origem",
                table: "campus",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_local_oferta_endereco_cidade_coerente",
                table: "local_oferta",
                sql: "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_campus_endereco_cidade_coerente",
                table: "campus",
                sql: "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Reverter aqui destruiria o endereço
            // estruturado capturado (logradouro/bairro/distrito/coordenada) e
            // recriaria um endereco texto-livre vazio — perda silenciosa de dado
            // num `database update <baseline>`, caminho proibido.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
