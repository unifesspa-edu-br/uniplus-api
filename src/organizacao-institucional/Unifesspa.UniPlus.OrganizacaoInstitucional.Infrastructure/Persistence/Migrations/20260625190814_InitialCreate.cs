using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organizacao");

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                schema: "organizacao",
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
                name: "unidade",
                schema: "organizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unidade_superior_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unidade_academica = table.Column<bool>(type: "boolean", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("pk_unidade", x => x.id);
                    table.ForeignKey(
                        name: "fk_unidade_unidade_unidade_superior_id",
                        column: x => x.unidade_superior_id,
                        principalSchema: "organizacao",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instituicao",
                schema: "organizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    organizacao_academica = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    categoria_administrativa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mantenedora = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    codigo_mantenedora_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    situacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ato_credenciamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ato_recredenciamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    conceito_institucional = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    igc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
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
                    unidade_raiz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registro_vivo_sentinela = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_instituicao", x => x.id);
                    table.CheckConstraint("ck_instituicao_cidade_completa", "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)");
                    table.CheckConstraint("ck_instituicao_cidade_obrigatoria_com_endereco", "endereco_cep IS NULL OR cidade_codigo_ibge IS NOT NULL");
                    table.CheckConstraint("ck_instituicao_endereco_cidade_coerente", "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
                    table.CheckConstraint("ck_instituicao_endereco_completo", "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL AND endereco_logradouro IS NULL AND endereco_numero IS NULL AND endereco_complemento IS NULL AND endereco_bairro IS NULL AND endereco_distrito IS NULL AND endereco_latitude IS NULL AND endereco_longitude IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
                    table.CheckConstraint("ck_instituicao_singleton_sentinela", "registro_vivo_sentinela = true");
                    table.ForeignKey(
                        name: "fk_instituicao_unidades_unidade_raiz_id",
                        column: x => x.unidade_raiz_id,
                        principalSchema: "organizacao",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unidade_identificador_historico",
                schema: "organizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_identificador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo_mudanca = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidade_identificador_historico", x => x.id);
                    table.ForeignKey(
                        name: "fk_unidade_identificador_historico_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "organizacao",
                        principalTable: "unidade",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                schema: "organizacao",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                schema: "organizacao",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_cidade_codigo_ibge",
                schema: "organizacao",
                table: "instituicao",
                column: "cidade_codigo_ibge");

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_singleton_vivo",
                schema: "organizacao",
                table: "instituicao",
                column: "registro_vivo_sentinela",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_instituicao_unidade_raiz_id",
                schema: "organizacao",
                table: "instituicao",
                column: "unidade_raiz_id");

            migrationBuilder.CreateIndex(
                name: "ix_unidade_alias",
                schema: "organizacao",
                table: "unidade",
                column: "alias");

            migrationBuilder.CreateIndex(
                name: "ix_unidade_codigo_vivo",
                schema: "organizacao",
                table: "unidade",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_unidade_sigla_vivo",
                schema: "organizacao",
                table: "unidade",
                column: "sigla",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_unidade_slug_vivo",
                schema: "organizacao",
                table: "unidade",
                column: "slug",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_unidade_superior_id",
                schema: "organizacao",
                table: "unidade",
                column: "unidade_superior_id");

            migrationBuilder.CreateIndex(
                name: "ix_uid_hist_unidade_tipo_inicio",
                schema: "organizacao",
                table: "unidade_identificador_historico",
                columns: new[] { "unidade_id", "tipo_identificador", "vigencia_inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_cache",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "instituicao",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "unidade_identificador_historico",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "unidade",
                schema: "organizacao");
        }
    }
}
