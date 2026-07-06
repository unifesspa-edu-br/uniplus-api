using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcessoSeletivoConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "campus",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "codigo_curso",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "edital_id",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "turno",
                schema: "selecao",
                table: "processos_seletivos");

            // Drop+add explícito (não rename): total_vagas/nome_curso e tipo/nome
            // não são a mesma coluna — o scaffolder só as pareou por coincidência
            // de tipo. Sem produção nem dados, drop+add é o correto.
            migrationBuilder.DropColumn(
                name: "total_vagas",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "nome_curso",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.AddColumn<string>(
                name: "nome",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                schema: "selecao",
                table: "processos_seletivos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "status",
                schema: "selecao",
                table: "processos_seletivos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "etapas_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    carater = table.Column<int>(type: "integer", nullable: false),
                    peso = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    nota_minima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_etapas_processo_processos_seletivos_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ofertas_atendimento_especializado",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ofertas_atendimento_especializado", x => x.id);
                    table.ForeignKey(
                        name: "fk_ofertas_atendimento_especializado_processos_seletivos_proce",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ofertas_condicao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oferta_atendimento_especializado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condicao_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condicao_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    condicao_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ofertas_condicao", x => x.id);
                    table.ForeignKey(
                        name: "fk_ofertas_condicao_ofertas_atendimento_especializado_oferta_a",
                        column: x => x.oferta_atendimento_especializado_id,
                        principalSchema: "selecao",
                        principalTable: "ofertas_atendimento_especializado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ofertas_recurso",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oferta_atendimento_especializado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ofertas_recurso", x => x.id);
                    table.ForeignKey(
                        name: "fk_ofertas_recurso_ofertas_atendimento_especializado_oferta_at",
                        column: x => x.oferta_atendimento_especializado_id,
                        principalSchema: "selecao",
                        principalTable: "ofertas_atendimento_especializado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ofertas_tipo_deficiencia",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oferta_atendimento_especializado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_deficiencia_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_deficiencia_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ofertas_tipo_deficiencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_ofertas_tipo_deficiencia_ofertas_atendimento_especializado_",
                        column: x => x.oferta_atendimento_especializado_id,
                        principalSchema: "selecao",
                        principalTable: "ofertas_atendimento_especializado",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_etapas_processo_processo_seletivo_id_ordem",
                schema: "selecao",
                table: "etapas_processo",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_atendimento_especializado_processo_seletivo_id",
                schema: "selecao",
                table: "ofertas_atendimento_especializado",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_condicao_oferta_atendimento_especializado_id_condic",
                schema: "selecao",
                table: "ofertas_condicao",
                columns: new[] { "oferta_atendimento_especializado_id", "condicao_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_recurso_oferta_atendimento_especializado_id_recurso",
                schema: "selecao",
                table: "ofertas_recurso",
                columns: new[] { "oferta_atendimento_especializado_id", "recurso_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_tipo_deficiencia_oferta_atendimento_especializado_i",
                schema: "selecao",
                table: "ofertas_tipo_deficiencia",
                columns: new[] { "oferta_atendimento_especializado_id", "tipo_deficiencia_origem_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "etapas_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ofertas_condicao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ofertas_recurso",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ofertas_tipo_deficiencia",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ofertas_atendimento_especializado",
                schema: "selecao");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "tipo",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.DropColumn(
                name: "nome",
                schema: "selecao",
                table: "processos_seletivos");

            migrationBuilder.AddColumn<int>(
                name: "total_vagas",
                schema: "selecao",
                table: "processos_seletivos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "nome_curso",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "campus",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "codigo_curso",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "edital_id",
                schema: "selecao",
                table: "processos_seletivos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "turno",
                schema: "selecao",
                table: "processos_seletivos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
