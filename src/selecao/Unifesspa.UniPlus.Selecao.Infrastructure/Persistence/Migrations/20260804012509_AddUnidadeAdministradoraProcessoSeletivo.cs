using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadeAdministradoraProcessoSeletivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_nome",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "",
                comment: "Snapshot-copy do nome da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");

            migrationBuilder.AddColumn<Guid>(
                name: "unidade_administradora_origem_id",
                schema: "selecao",
                table: "processos_seletivos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                comment: "Id da Unidade administradora em Organização Institucional (ADR-0061, sem FK cross-schema) — congelado na criação, imutável.");

            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_sigla",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                comment: "Snapshot-copy da sigla da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");

            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_slug",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                comment: "Snapshot-copy do slug da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");

            migrationBuilder.AddColumn<string>(
                name: "unidade_administradora_tipo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                comment: "Snapshot-copy do tipo organizacional da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unidade_administradora_nome",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_origem_id",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_sigla",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_slug",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "unidade_administradora_tipo",
                schema: "selecao",
                table: "processos_seletivos");
        }
    }
}
