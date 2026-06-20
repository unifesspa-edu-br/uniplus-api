using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdotaReferenciaCidadeGeoNaInstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem backfill: municipio_sede era texto livre (apenas o nome do
            // município), sem código IBGE nem UF — não há como derivar a referência
            // estruturada cidade_codigo_ibge sem geocodificação não confiável. A
            // composição no cliente (ADR-0090) passa a ser a fonte da cidade daqui
            // pra frente. Em fase de scaffolding não há dado semeado em
            // municipio_sede a preservar; a coluna é descartada.
            migrationBuilder.DropColumn(
                name: "municipio_sede",
                table: "instituicao");

            migrationBuilder.AddColumn<string>(
                name: "cidade_codigo_ibge",
                table: "instituicao",
                type: "character(7)",
                fixedLength: true,
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cidade_display_atualizado_em",
                table: "instituicao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade_nome",
                table: "instituicao",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade_origem",
                table: "instituicao",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cidade_uf",
                table: "instituicao",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_cidade_codigo_ibge",
                table: "instituicao",
                column: "cidade_codigo_ibge");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only per ADR-0054 §J: nova migration "Reverte..." é o
            // mecanismo canônico de revert. Dropar as colunas cidade_* aqui
            // destruiria a referência de cidade capturada e recriaria um
            // municipio_sede vazio — perda silenciosa de dado num
            // `database update <baseline>`, caminho proibido.
            throw new NotSupportedException("Forward-only migration per ADR-0054 §J.");
        }
    }
}
