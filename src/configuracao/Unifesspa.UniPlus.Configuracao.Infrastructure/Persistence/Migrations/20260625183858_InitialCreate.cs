using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "configuracao");

            migrationBuilder.CreateTable(
                name: "campus",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    cidade_origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cidade_display_atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    endereco_cep = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_distrito = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true),
                    endereco_cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    endereco_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    endereco_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    endereco_nivel_resolucao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    endereco_display_atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campus", x => x.id);
                    table.CheckConstraint("ck_campus_endereco_cidade_coerente", "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
                    table.CheckConstraint("ck_campus_endereco_completo", "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL AND endereco_logradouro IS NULL AND endereco_numero IS NULL AND endereco_complemento IS NULL AND endereco_bairro IS NULL AND endereco_distrito IS NULL AND endereco_latitude IS NULL AND endereco_longitude IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_headers_json = table.Column<string>(type: "jsonb", nullable: true),
                    response_body_cipher = table.Column<byte[]>(type: "bytea", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "peso_area_enem",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolucao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    grupo_curso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    peso_redacao = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_ciencias_natureza = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_ciencias_humanas = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_linguagens = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    peso_matematica = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    corte_redacao = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false, defaultValue: 400m),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peso_area_enem", x => x.id);
                    table.CheckConstraint("ck_peso_area_enem_corte_redacao", "corte_redacao >= 0 AND corte_redacao <= 1000");
                    table.CheckConstraint("ck_peso_area_enem_grupo_curso", "grupo_curso IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
                    table.CheckConstraint("ck_peso_area_enem_peso_ciencias_humanas", "peso_ciencias_humanas >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_ciencias_natureza", "peso_ciencias_natureza >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_linguagens", "peso_linguagens >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_matematica", "peso_matematica >= 0");
                    table.CheckConstraint("ck_peso_area_enem_peso_redacao", "peso_redacao >= 0");
                });

            migrationBuilder.CreateTable(
                name: "referencia_reserva_demografica",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    censo_referencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ppi_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    quilombola_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    pcd_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referencia_reserva_demografica", x => x.id);
                    table.CheckConstraint("ck_referencia_reserva_demografica_pcd_percentual", "pcd_percentual >= 0 AND pcd_percentual <= 100");
                    table.CheckConstraint("ck_referencia_reserva_demografica_ppi_percentual", "ppi_percentual >= 0 AND ppi_percentual <= 100");
                    table.CheckConstraint("ck_referencia_reserva_demografica_quilombola_percentual", "quilombola_percentual >= 0 AND quilombola_percentual <= 100");
                });

            migrationBuilder.CreateTable(
                name: "local_oferta",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    campus_responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    cidade_origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cidade_display_atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    endereco_cep = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_distrito = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true),
                    endereco_cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    endereco_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    endereco_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    endereco_nivel_resolucao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    endereco_display_atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_local_oferta", x => x.id);
                    table.CheckConstraint("ck_local_oferta_endereco_cidade_coerente", "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
                    table.CheckConstraint("ck_local_oferta_endereco_completo", "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL AND endereco_logradouro IS NULL AND endereco_numero IS NULL AND endereco_complemento IS NULL AND endereco_bairro IS NULL AND endereco_distrito IS NULL AND endereco_latitude IS NULL AND endereco_longitude IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_local_oferta_campus_campus_responsavel_id",
                        column: x => x.campus_responsavel_id,
                        principalSchema: "configuracao",
                        principalTable: "campus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campus_cidade_codigo_ibge",
                schema: "configuracao",
                table: "campus",
                column: "cidade_codigo_ibge");

            migrationBuilder.CreateIndex(
                name: "ix_campus_sigla_vivo",
                schema: "configuracao",
                table: "campus",
                column: "sigla",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                schema: "configuracao",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                schema: "configuracao",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_local_oferta_campus_responsavel_id",
                schema: "configuracao",
                table: "local_oferta",
                column: "campus_responsavel_id");

            migrationBuilder.CreateIndex(
                name: "ix_local_oferta_cidade_codigo_ibge",
                schema: "configuracao",
                table: "local_oferta",
                column: "cidade_codigo_ibge");

            migrationBuilder.CreateIndex(
                name: "ix_peso_area_enem_resolucao_grupo_vivo",
                schema: "configuracao",
                table: "peso_area_enem",
                columns: new[] { "resolucao", "grupo_curso" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_referencia_reserva_demografica_censo_vivo",
                schema: "configuracao",
                table: "referencia_reserva_demografica",
                column: "censo_referencia",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_cache",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "local_oferta",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "peso_area_enem",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "referencia_reserva_demografica",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "campus",
                schema: "configuracao");
        }
    }
}
