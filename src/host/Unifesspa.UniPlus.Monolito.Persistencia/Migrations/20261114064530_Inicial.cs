using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unifesspa.UniPlus.Monolito.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "selecao");

            migrationBuilder.EnsureSchema(
                name: "autorizacao");

            migrationBuilder.EnsureSchema(
                name: "publicacoes");

            migrationBuilder.EnsureSchema(
                name: "configuracao");

            migrationBuilder.EnsureSchema(
                name: "ingresso");

            migrationBuilder.EnsureSchema(
                name: "plataforma");

            migrationBuilder.EnsureSchema(
                name: "organizacao");

            migrationBuilder.EnsureSchema(
                name: "discentes");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateSequence(
                name: "numero_inscricao_seq",
                schema: "selecao");

            migrationBuilder.CreateSequence(
                name: "ordem_da_trilha_de_contencao",
                schema: "autorizacao");

            migrationBuilder.CreateTable(
                name: "ato_normativo",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orgao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serie = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    congela_configuracao = table.Column<bool>(type: "boolean", nullable: false),
                    efeito_irreversivel = table.Column<bool>(type: "boolean", nullable: false),
                    unico_por_objeto = table.Column<bool>(type: "boolean", nullable: false),
                    data_do_ato = table.Column<DateOnly>(type: "date", nullable: false),
                    documento_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    assinante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versao_invocada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    versao_invocada_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ato_retificado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_retificacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ato_normativo", x => x.id);
                    table.CheckConstraint("ck_ato_normativo_ano_positivo", "ano > 0");
                    table.CheckConstraint("ck_ato_normativo_documento_hash", "documento_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_ato_normativo_nao_autorretifica", "ato_retificado_id IS NULL OR ato_retificado_id <> id");
                    table.CheckConstraint("ck_ato_normativo_retificacao_completa", "(ato_retificado_id IS NULL AND motivo_retificacao IS NULL) OR (ato_retificado_id IS NOT NULL AND motivo_retificacao IS NOT NULL AND btrim(motivo_retificacao) <> '')");
                    table.CheckConstraint("ck_ato_normativo_versao_completa", "(versao_invocada_id IS NULL AND versao_invocada_hash IS NULL) OR (versao_invocada_id IS NOT NULL AND versao_invocada_hash IS NOT NULL)");
                    table.CheckConstraint("ck_ato_normativo_versao_hash", "versao_invocada_hash IS NULL OR versao_invocada_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_ato_normativo_versao_id_nao_zero", "versao_invocada_id IS NULL OR versao_invocada_id <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_ato_normativo_ato_retificado",
                        column: x => x.ato_retificado_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos_da_reclassificacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos_da_reclassificacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "avisos_emitidos",
                schema: "autorizacao",
                columns: table => new
                {
                    mensagem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emitido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos_emitidos", x => x.mensagem_id);
                });

            migrationBuilder.CreateTable(
                name: "base_legal_bonus_regional",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_instrumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    identificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("pk_base_legal_bonus_regional", x => x.id);
                    table.CheckConstraint("CK_base_legal_bonus_regional_tipo_instrumento", "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER','EDITAL')");
                });

            migrationBuilder.CreateTable(
                name: "calendario_dias_uteis",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendario_dias_uteis", x => x.id);
                });

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
                name: "categoria_documento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_categoria_documento", x => x.id);
                    table.CheckConstraint("ck_categoria_documento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_categoria_documento_ordem_nao_negativa", "ordem >= 0");
                });

            migrationBuilder.CreateTable(
                name: "certames_divulgados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_versao = table.Column<int>(type: "integer", nullable: false),
                    ato_criador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    versao_projecao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    identificador_legivel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    modalidades_ofertadas = table.Column<string[]>(type: "text[]", nullable: false),
                    inscricoes_de = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    inscricoes_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    certame = table.Column<string>(type: "jsonb", nullable: false),
                    editais_divulgados = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb", comment: "Os documentos de cada ato divulgado do processo — o edital de cada versão e o ato que não cria versão —, com o ato e o endereço no acervo público."),
                    divulgado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nome_ordenacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(nome)", stored: true, collation: "C"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certames_divulgados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chamadas",
                schema: "ingresso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    data_publicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prazo_manifestacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chamadas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chaves_a_limpar",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "A chave do objeto; leva só o id do arquivo."),
                    limpar_apos = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "A partir de quando a chave pode ser apagada."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chaves_a_limpar", x => x.id);
                },
                comment: "Chaves do armazenamento a apagar depois de uma hora: a URL de envio recria o objeto em claro até vencer.");

            migrationBuilder.CreateTable(
                name: "condicao_atendimento_especializado",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_condicao_atendimento_especializado", x => x.id);
                    table.CheckConstraint("ck_condicao_atendimento_especializado_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                });

            migrationBuilder.CreateTable(
                name: "curso",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grau = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nivel_ensino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    grupo_area_enem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_curso_grupo_area_enem. O rótulo fica em grupo_area_enem_rotulo. Nulo quando o curso não declara grupo."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    nome_ordenacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(nome)", stored: true, collation: "C"),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Rótulo do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código em grupo_area_enem. Nulo quando o curso não declara grupo."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curso", x => x.id);
                    table.CheckConstraint("ck_curso_grupo_area_enem", "grupo_area_enem IS NULL OR grupo_area_enem IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')");
                });

            migrationBuilder.CreateTable(
                name: "fase_canonica",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    agrupa_etapas = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    permite_complementacao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    coleta_inscricao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    coleta_solicitacao_isencao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    origem_data = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unidade_sugerida_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_sugerida_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    unidade_sugerida_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("pk_fase_canonica", x => x.id);
                    table.CheckConstraint("ck_fase_canonica_agrupa_etapas", "agrupa_etapas = false OR codigo = 'AVALIACAO'");
                    table.CheckConstraint("ck_fase_canonica_codigo_canonico", "codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_fase_canonica_codigo_formato", "codigo ~ '^[A-Z_]+$'");
                    table.CheckConstraint("ck_fase_canonica_complementacao", "permite_complementacao = false OR codigo IN ('HOMOLOGACAO', 'RECURSOS')");
                    table.CheckConstraint("ck_fase_canonica_origem_data", "origem_data IN ('NO_EDITAL', 'NA_EXECUCAO')");
                });

            migrationBuilder.CreateTable(
                name: "idempotency_cache",
                schema: "plataforma",
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
                name: "implantacoes_das_permissoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    impressao = table.Column<string>(type: "char(64)", nullable: false),
                    implantada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_implantacoes_das_permissoes", x => x.id);
                    table.CheckConstraint("ck_implantacoes_das_permissoes_impressao", "impressao ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "inscricoes",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "O processo seletivo desta inscrição."),
                    cpf_indice = table.Column<byte[]>(type: "bytea", nullable: false, comment: "Índice cego (HMAC-SHA256) do CPF — sustenta a busca pelo dono e o índice único por processo, sem decifrar a tabela."),
                    sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "O sub do token de quem iniciou a inscrição — nunca troca de dono."),
                    emissor_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "O emissor (issuer) do token de quem iniciou a inscrição."),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    versao_formulario_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A versão do formulário da inscrição (UNI-REQ-0084): a divulgada que o rascunho adotou por último; o primeiro envio a congela."),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    oferta_curso_segunda_opcao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_inscricao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Gerado na primeira submissão (sequência do banco, formatada); nulo em rascunho."),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "O instante em que o candidato cancelou a inscrição enviada; nulo se não foi cancelada."),
                    cancelada_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "O sub do token de quem cancelou; nulo se não foi cancelada."),
                    motivo_do_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "O motivo do cancelamento; 'a pedido do candidato' quando o candidato não o disse. Nulo se não foi cancelada."),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscricoes", x => x.id);
                },
                comment: "A inscrição de um candidato num processo seletivo (issue #1858) — uma por CPF e processo. As respostas ficam em respostas_da_inscricao e nas tabelas vizinhas.");

            migrationBuilder.CreateTable(
                name: "instalacoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    executor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    executor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    executor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    numero_do_documento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tamanho_do_pdf = table.Column<long>(type: "bigint", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    documento_etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    documento_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    documento_verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    documento_recusado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo_resumo = table.Column<byte[]>(type: "bytea", nullable: true),
                    codigo_enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    codigo_expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacoes", x => x.id);
                    table.CheckConstraint("ck_instalacoes_codigo_completo", "(codigo_enviado_em IS NULL) = (codigo_expira_em IS NULL)");
                    table.CheckConstraint("ck_instalacoes_tentativas", "tentativas >= 0 AND tentativas <= 5");
                });

            migrationBuilder.CreateTable(
                name: "matriculas",
                schema: "ingresso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    convocacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    codigo_curso = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matriculas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modalidade",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    natureza_legal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_vagas = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_origem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    regra_remanejamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    remanejamento_args = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    regra_de_aplicabilidade = table.Column<string>(type: "jsonb", nullable: false),
                    acao_quando_indeferido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_modalidade", x => x.id);
                    table.CheckConstraint("ck_modalidade_acao_quando_indeferido", "acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ('RECLASSIFICAR_AC', 'RECLASSIFICAR_REGRA_EDITAL')");
                    table.CheckConstraint("ck_modalidade_codigo_formato", "codigo ~ '^[A-Z0-9_]+$'");
                    table.CheckConstraint("ck_modalidade_composicao_vagas", "composicao_vagas IN ('RESIDUAL_DO_VO', 'DENTRO_DO_VR', 'RETIRA_DE', 'SUPLEMENTAR_AO_TOTAL')");
                    table.CheckConstraint("ck_modalidade_natureza_legal", "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'ACAO_AFIRMATIVA')");
                    table.CheckConstraint("ck_modalidade_regra_remanejamento", "regra_remanejamento IS NULL OR regra_remanejamento IN ('SEGUE_CASCATA', 'DESTINO_UNICO', 'CRUZADO')");
                    table.CheckConstraint("ck_modalidade_retira_de_origem", "(composicao_vagas = 'RETIRA_DE') = (composicao_origem IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "modelos_formulario",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos_formulario", x => x.id);
                    table.CheckConstraint("ck_modelos_formulario_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                });

            migrationBuilder.CreateTable(
                name: "motivos_de_decisao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subclassificacao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sentido = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_de_decisao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "motivos_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo_de_decisao_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subclassificacao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sentido = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    congelado = table.Column<bool>(type: "boolean", nullable: false),
                    justificativa_da_inclusao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_do_processo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedades_legais",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    categoria = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    predicado = table.Column<string>(type: "jsonb", nullable: false),
                    descricao_humana = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ato_normativo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    portaria_interna_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obrigatoriedades_legais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfis",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    governanca = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    nome_normalizado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, computedColumnSql: "lower(nome)", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "peso_area_enem",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolucao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    grupo_curso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_peso_area_enem_grupo_curso; com a resolução, forma a chave de negócio da linha. O rótulo fica em grupo_curso_rotulo."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    grupo_curso_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Rótulo do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código em grupo_curso."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peso_area_enem", x => x.id);
                    table.CheckConstraint("ck_peso_area_enem_grupo_curso", "grupo_curso IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')");
                });

            migrationBuilder.CreateTable(
                name: "precedencia_fase",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    antecessora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sucessora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    permite_sobreposicao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("pk_precedencia_fase", x => x.id);
                    table.CheckConstraint("ck_precedencia_fase_antecessora_canonica", "antecessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_precedencia_fase_antecessora_formato", "antecessora_codigo ~ '^[A-Z_]+$'");
                    table.CheckConstraint("ck_precedencia_fase_sem_self_loop", "antecessora_codigo <> sucessora_codigo");
                    table.CheckConstraint("ck_precedencia_fase_sucessora_canonica", "sucessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_PRELIMINAR', 'RECURSOS', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_precedencia_fase_sucessora_formato", "sucessora_codigo ~ '^[A-Z_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "processos_seletivos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    identificador_legivel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Identificador legível escolhido no cadastro; dele derivam o endereço público do certame e a chave no acervo. Ausência = ainda não declarado."),
                    tipo_processo_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id de origem do tipo de processo em Configuração, sem FK cross-schema; congelado na criação."),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tipo_processo_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    origem_candidatos = table.Column<int>(type: "integer", nullable: false),
                    unidade_administradora_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id da Unidade administradora em Organização Institucional (ADR-0061, sem FK cross-schema) — congelado na criação, imutável."),
                    unidade_administradora_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Snapshot-copy da sigla da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Snapshot-copy do slug da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Snapshot-copy do nome da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Snapshot-copy do tipo organizacional da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true, comment: "Snapshot-copy do código IBGE da cidade da Unidade administradora no momento da criação — nulo para processos anteriores à issue #1114."),
                    unidade_administradora_cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true, comment: "Snapshot-copy do nome de exibição da cidade da Unidade administradora no momento da criação."),
                    unidade_administradora_cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, comment: "Snapshot-copy da UF da cidade da Unidade administradora no momento da criação."),
                    localidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false, comment: "Código IBGE do município cujo calendário rege a contagem dos prazos — o único valor normativo da localidade."),
                    localidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Nome do município da localidade regente — cache de exibição, não entra em cálculo de prazo."),
                    localidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false, comment: "UF da localidade regente — cache de exibição; a UF que vale é a derivada do prefixo do código."),
                    algoritmo_contagem_prazo_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Código da entrada de algoritmo de contagem do rol_de_regras que o certame declarou."),
                    algoritmo_contagem_prazo_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "Versão da entrada declarada — evolução da convenção é versão nova, nunca alteração da vigente."),
                    algoritmo_contagem_prazo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true, comment: "Hash da definição resolvida no rol_de_regras — é o que prova que a convenção aplicada não mudou depois."),
                    referencia_temporal_fatos_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    referencia_temporal_fatos_data = table.Column<DateOnly>(type: "date", nullable: true),
                    referencia_temporal_fatos_fase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aplica_bonus_regional = table.Column<bool>(type: "boolean", nullable: true, comment: "Declaração de que o processo aplica (true) ou não aplica (false) o bônus regional. Nulo = ainda não declarado."),
                    numero_de_opcoes_de_curso = table.Column<int>(type: "integer", nullable: true, comment: "Quantos cursos o candidato escolhe na inscrição (1 ou 2). Nulo = ainda não declarado."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processos_seletivos", x => x.id);
                    table.CheckConstraint("ck_processos_seletivos_numero_de_opcoes_de_curso", "numero_de_opcoes_de_curso IS NULL OR numero_de_opcoes_de_curso IN (1, 2)");
                    table.CheckConstraint("ck_processos_seletivos_unidade_administradora_cidade_completa", "(unidade_administradora_cidade_codigo_ibge IS NULL AND unidade_administradora_cidade_nome IS NULL AND unidade_administradora_cidade_uf IS NULL) OR (unidade_administradora_cidade_codigo_ibge IS NOT NULL AND unidade_administradora_cidade_nome IS NOT NULL AND unidade_administradora_cidade_uf IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "protecao_do_catalogo_atos",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cadastro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ciclo = table.Column<int>(type: "integer", nullable: false),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    classificacao_recomendada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    diante_de_manifestacao_desfavoravel = table.Column<bool>(type: "boolean", nullable: false),
                    ator_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ator_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ator_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protecao_do_catalogo_atos", x => x.id);
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_ajuste_com_recomendacao", "ato <> 'AJUSTE_RECOMENDADO' OR classificacao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_ato", "ato IN ('SOLICITACAO', 'APROVACAO', 'AJUSTE_RECOMENDADO', 'REPROVACAO', 'ACEITE')");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_cadastro", "cadastro IN ('TIPO_DOCUMENTO', 'FATO')");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_ciclo_positivo", "ciclo >= 1");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_diante_so_na_decisao", "ato IN ('APROVACAO', 'AJUSTE_RECOMENDADO', 'REPROVACAO') OR NOT diante_de_manifestacao_desfavoravel");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_justificativa_diante_desfavoravel", "ato NOT IN ('APROVACAO', 'AJUSTE_RECOMENDADO') OR NOT diante_de_manifestacao_desfavoravel OR justificativa IS NOT NULL");
                    table.CheckConstraint("ck_protecao_do_catalogo_atos_reprovacao_com_justificativa", "ato <> 'REPROVACAO' OR justificativa IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "protecao_do_catalogo_manifestacoes",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo = table.Column<int>(type: "integer", nullable: false),
                    parecer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    substitui_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    autor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    cadastro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protecao_do_catalogo_manifestacoes", x => x.id);
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_cadastro", "cadastro IN ('TIPO_DOCUMENTO', 'FATO')");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_ciclo_positivo", "ciclo >= 1");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_parecer", "parecer IN ('ADEQUADA', 'RECOMENDA_AJUSTE', 'INADEQUADA')");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_substitui_outra", "substitui_id IS NULL OR substitui_id <> id");
                    table.CheckConstraint("ck_protecao_do_catalogo_manifestacoes_texto_no_desfavoravel", "parecer = 'ADEQUADA' OR texto IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_protecao_do_catalogo_manifestacoes_substitui",
                        column: x => x.substitui_id,
                        principalSchema: "configuracao",
                        principalTable: "protecao_do_catalogo_manifestacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurso_acessibilidade",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_recurso_acessibilidade", x => x.id);
                    table.CheckConstraint("ck_recurso_acessibilidade_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
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
                name: "revisoes_de_acessos",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aberta_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aberta_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aberta_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisoes_de_acessos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol_de_fatos_candidato",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    componente_exibicao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    dominio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cardinalidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fonte_valores = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ponto_resolucao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    binding = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    formato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    escopo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sistema = table.Column<bool>(type: "boolean", nullable: false),
                    protecao_situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    protecao_ciclo = table.Column<int>(type: "integer", nullable: false),
                    protecao_solicitada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_solicitada_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    protecao_decidida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_decidida_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    protecao_recomendada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    protecao_justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    protecao_aceita_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_aceita_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    protecao_manifestada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_manifestacao_desfavoravel = table.Column<bool>(type: "boolean", nullable: false),
                    protecao_manifestacoes_no_ciclo = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    regras_padrao = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    dependencias = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_de_fatos_candidato", x => x.id);
                    table.CheckConstraint("ck_rol_de_fatos_candidato_cardinalidade", "cardinalidade IN ('ESCALAR', 'MULTIVALORADO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_classificacao_minima_do_dominio", "(dominio NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') OR (dominio = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (dominio IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_classificacao_protecao", "classificacao_protecao IN ('PUBLICO', 'INTERNO', 'PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_componente_exibicao", "componente_exibicao IS NULL OR componente_exibicao IN ('MOEDA', 'SITUACAO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_dominio", "dominio IN ('CATEGORICO', 'BOOLEANO', 'NUMERICO', 'TEXTO', 'DATA', 'ENDERECO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_escopo", "escopo IN ('CANDIDATO', 'MEMBRO_GRUPO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_fonte_valores_coerente", "(dominio = 'CATEGORICO' AND fonte_valores IS NOT NULL AND fonte_valores IN ('GLOBAL', 'PROCESSO', 'MODALIDADE', 'MUNICIPIOS_BONUS', 'GEO_UF', 'GEO_MUNICIPIO', 'TIPOS_DEFICIENCIA')) OR (dominio <> 'CATEGORICO' AND fonte_valores IS NULL)");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_formato_coerente", "(dominio = 'TEXTO' AND formato IS NOT NULL AND formato IN ('LIVRE', 'CPF', 'EMAIL', 'TELEFONE', 'CEP', 'NOME_PESSOA')) OR (dominio <> 'TEXTO' AND formato IS NULL)");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_hipotese_legal", "hipotese_legal IN ('CONSENTIMENTO', 'CUMPRIMENTO_OBRIGACAO_LEGAL', 'EXECUCAO_POLITICAS_PUBLICAS', 'ESTUDOS_POR_ORGAO_DE_PESQUISA', 'EXECUCAO_CONTRATO', 'EXERCICIO_REGULAR_DE_DIREITOS', 'PROTECAO_DA_VIDA', 'TUTELA_DA_SAUDE', 'INTERESSE_LEGITIMO', 'PROTECAO_DO_CREDITO', 'PREVENCAO_A_FRAUDE')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_origem", "origem IN ('DERIVADO', 'DECLARADO', 'INTEGRACAO')");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_protecao_ajuste_com_recomendacao", "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_protecao_ciclo_positivo", "protecao_ciclo >= 1");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_protecao_desfavoravel_manifestada", "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_protecao_manifestacoes_contadas", "protecao_manifestacoes_no_ciclo >= 0 AND (protecao_manifestacoes_no_ciclo = 0) = (protecao_manifestada_em IS NULL)");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_protecao_reprovada_com_justificativa", "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");
                    table.CheckConstraint("ck_rol_de_fatos_candidato_protecao_situacao", "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'AJUSTE_RECOMENDADO', 'REPROVADA')");
                });

            migrationBuilder.CreateTable(
                name: "rol_de_regras",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tipo = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    esquema_args = table.Column<string>(type: "jsonb", nullable: false),
                    invariantes = table.Column<string>(type: "jsonb", nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_de_regras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_run",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) da execução."),
                    status = table.Column<int>(type: "integer", nullable: false, comment: "Estado da execução (Running/Completed/Partial/Failed)."),
                    data_de_referencia = table.Column<DateOnly>(type: "date", nullable: false, comment: "Dia a que a execução se refere — não o instante em que rodou. Uma execução disparada de madrugada refere-se ao dia que começou."),
                    total_items = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade total de itens previstos para esta execução."),
                    processed_items = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens já processados."),
                    success_count = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens processados com sucesso."),
                    error_count = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens que falharam durante o processamento."),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Instante de início da execução."),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Instante de conclusão da execução — nulo enquanto o status é Running."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_run", x => x.id);
                },
                comment: "Controle de execução das rotinas de sincronização de dados discentes com o SIGAA — uma linha por execução.");

            migrationBuilder.CreateTable(
                name: "termo_consentimento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    texto_rascunho = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    base_legal_rascunho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    forma_aceite_rascunho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    revisado = table.Column<bool>(type: "boolean", nullable: false),
                    revisado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revisado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termo_consentimento", x => x.id);
                    table.CheckConstraint("ck_termo_consentimento_forma_aceite_rascunho", "forma_aceite_rascunho IN ('A_DEFINIR', 'REGISTRO_DIGITAL_SEM_LOG_IP', 'REGISTRO_DIGITAL_COM_LOG_IP')");
                });

            migrationBuilder.CreateTable(
                name: "tipo_ato_publicado",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    congela_configuracao = table.Column<bool>(type: "boolean", nullable: false),
                    unico_por_objeto = table.Column<bool>(type: "boolean", nullable: false),
                    efeito_irreversivel = table.Column<bool>(type: "boolean", nullable: false),
                    eh_resultado = table.Column<bool>(type: "boolean", nullable: false),
                    publicavel_como_avulso = table.Column<bool>(type: "boolean", nullable: false),
                    exige_ato_corrigido = table.Column<bool>(type: "boolean", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_tipo_ato_publicado", x => x.id);
                    table.CheckConstraint("ck_tipo_ato_publicado_codigo_formato", "codigo ~ '^[A-Z]+(_[A-Z]+)*$'");
                    table.CheckConstraint("ck_tipo_ato_publicado_vigencia", "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio");
                });

            migrationBuilder.CreateTable(
                name: "tipo_banca",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fase_tipica = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("pk_tipo_banca", x => x.id);
                    table.CheckConstraint("ck_tipo_banca_codigo_canonico", "codigo IN ('BANCA_ANALISE_DOCUMENTAL', 'BANCA_ENTREVISTA', 'BANCA_CORRECAO_REDACOES', 'BANCA_ANALISE_RECURSOS', 'BANCA_HETEROIDENTIFICACAO', 'BANCA_BIOPSICOSSOCIAL')");
                    table.CheckConstraint("ck_tipo_banca_codigo_formato", "codigo ~ '^[A-Z_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "tipo_deficiencia",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    permanente = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("pk_tipo_deficiencia", x => x.id);
                    table.CheckConstraint("ck_tipo_deficiencia_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                });

            migrationBuilder.CreateTable(
                name: "tipo_documento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    formatos_aceitos = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tamanho_maximo_mb = table.Column<int>(type: "integer", nullable: true),
                    tipo_equivalente = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    protecao_situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    protecao_ciclo = table.Column<int>(type: "integer", nullable: false),
                    protecao_solicitada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_solicitada_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    protecao_decidida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_decidida_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    protecao_recomendada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    protecao_justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    protecao_aceita_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_aceita_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    protecao_manifestada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    protecao_manifestacao_desfavoravel = table.Column<bool>(type: "boolean", nullable: false),
                    protecao_manifestacoes_no_ciclo = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_documento", x => x.id);
                    table.CheckConstraint("ck_tipo_documento_categoria_formato", "categoria ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_tipo_documento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_tipo_documento_equivalente_diferente_codigo", "tipo_equivalente IS NULL OR tipo_equivalente <> codigo");
                    table.CheckConstraint("ck_tipo_documento_protecao_ajuste_com_recomendacao", "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_tipo_documento_protecao_ciclo_positivo", "protecao_ciclo >= 1");
                    table.CheckConstraint("ck_tipo_documento_protecao_desfavoravel_manifestada", "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");
                    table.CheckConstraint("ck_tipo_documento_protecao_manifestacoes_contadas", "protecao_manifestacoes_no_ciclo >= 0 AND (protecao_manifestacoes_no_ciclo = 0) = (protecao_manifestada_em IS NULL)");
                    table.CheckConstraint("ck_tipo_documento_protecao_reprovada_com_justificativa", "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");
                    table.CheckConstraint("ck_tipo_documento_protecao_situacao", "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'AJUSTE_RECOMENDADO', 'REPROVADA')");
                    table.CheckConstraint("ck_tipo_documento_tamanho_maximo_mb_positivo", "tamanho_maximo_mb IS NULL OR tamanho_maximo_mb > 0");
                });

            migrationBuilder.CreateTable(
                name: "tipos_etapa",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    admite_pontuacao = table.Column<bool>(type: "boolean", nullable: false),
                    admite_eliminacao = table.Column<bool>(type: "boolean", nullable: false),
                    nota_de_origem_no_enem = table.Column<bool>(type: "boolean", nullable: false, comment: "Nota das etapas deste tipo vem do ENEM; definido só pela carga do cadastro, nunca pela API."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_etapa", x => x.id);
                    table.CheckConstraint("ck_tipos_etapa_carater_admitido", "admite_pontuacao OR admite_eliminacao");
                    table.CheckConstraint("ck_tipos_etapa_nota_de_origem_no_enem_admite_pontuacao", "NOT nota_de_origem_no_enem OR admite_pontuacao");
                });

            migrationBuilder.CreateTable(
                name: "tipos_processo",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_processo", x => x.id);
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
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true, comment: "Código IBGE (7 dígitos) da cidade da Unidade — referência ao Geo, sem FK cross-banco; opcional all-or-nothing com nome/UF."),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true, comment: "Nome de exibição da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização."),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, comment: "UF da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização."),
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
                    table.CheckConstraint("ck_unidade_cidade_completa", "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_unidade_unidade_unidade_superior_id",
                        column: x => x.unidade_superior_id,
                        principalSchema: "organizacao",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_discente",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) do registro — não confundir com id_discente_sigaa, a chave natural do SIGAA."),
                    id_discente_sigaa = table.Column<long>(type: "bigint", nullable: false, comment: "Identificador do discente no SIGAA (chave natural do módulo) — usado para localizar e fazer upsert durante a sincronização."),
                    matricula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Matrícula do discente na instituição."),
                    cpf_ciphertext = table.Column<byte[]>(type: "bytea", nullable: false, comment: "CPF cifrado em repouso (AES-GCM, ADR-0121) — envelope autenticado (nonce + tag + dado); nunca texto claro."),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Nome do discente."),
                    nivel = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, comment: "Nível de ensino do vínculo (ex.: G para graduação) — vocabulário do SIGAA."),
                    curso_id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador do curso no SIGAA."),
                    curso_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Nome do curso."),
                    curso_codigo_emec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Código e-MEC do curso, quando disponível."),
                    curso_unidade_id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador da unidade acadêmica responsável pelo curso, no SIGAA."),
                    curso_unidade_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Nome da unidade acadêmica responsável pelo curso."),
                    situacao_id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador da situação acadêmica do discente no SIGAA."),
                    situacao_descricao = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Descrição da situação acadêmica (ex.: Matriculado, Concluído)."),
                    situacao_vinculo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Qualificador de vínculo associado à situação, no vocabulário do SIGAA."),
                    ano_ingresso = table.Column<int>(type: "integer", nullable: false, comment: "Ano de ingresso do discente no curso."),
                    periodo_ingresso = table.Column<int>(type: "integer", nullable: false, comment: "Período letivo de ingresso do discente no curso."),
                    resumo_do_conteudo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Resumo do conteúdo trazido do SIGAA na última sincronização — permite reconhecer que o vínculo não mudou e poupar a reescrita. Não cobre o CPF: como as demais colunas ficam legíveis, um resumo que o cobrisse permitiria recuperá-lo por tentativa e erro, desfazendo a cifra em repouso.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_discente", x => x.id);
                },
                comment: "Réplica local dos vínculos de discentes sincronizados do SIGAA (ADR-0121) — snapshot desnormalizado, sem referência viva a outras tabelas.");

            migrationBuilder.CreateTable(
                name: "anexo_do_ato",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_midia = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexo_do_ato", x => x.id);
                    table.CheckConstraint("ck_anexo_do_ato_hash", "hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_anexo_do_ato_nome", "btrim(nome) <> ''");
                    table.CheckConstraint("ck_anexo_do_ato_ordem", "ordem >= 0");
                    table.CheckConstraint("ck_anexo_do_ato_tipo_midia", "tipo_midia ~ '^[a-z0-9][a-z0-9.+_-]*/[a-z0-9][a-z0-9.+_-]*$'");
                    table.ForeignKey(
                        name: "fk_anexo_do_ato_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "linhagem_unica_por_objeto",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    raiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linhagem_unica_por_objeto", x => x.id);
                    table.CheckConstraint("ck_linhagem_unica_entidade_id_nao_zero", "entidade_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_linhagem_unica_entidade_tipo_formato", "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_linhagem_unica_raiz_nao_zero", "raiz_id <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_linhagem_unica_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_linhagem_unica_raiz",
                        column: x => x.raiz_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_ato_entidade",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_ato_entidade", x => x.id);
                    table.CheckConstraint("ck_vinculo_ato_entidade_id_nao_zero", "entidade_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_vinculo_ato_entidade_tipo_formato", "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_vinculo_ato_entidade_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "ato_normativo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_legal_bonus_regional_municipio",
                schema: "configuracao",
                columns: table => new
                {
                    base_legal_bonus_regional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_base_legal_bonus_regional_municipio", x => new { x.base_legal_bonus_regional_id, x.id });
                    table.CheckConstraint("CK_base_legal_bonus_regional_municipio_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'");
                    table.ForeignKey(
                        name: "fk_base_legal_bonus_regional_municipio_base_legal_bonus_region",
                        column: x => x.base_legal_bonus_regional_id,
                        principalSchema: "configuracao",
                        principalTable: "base_legal_bonus_regional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dia_nao_util",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendario_dias_uteis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    municipio_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    municipio_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    municipio_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dia_nao_util", x => x.id);
                    table.CheckConstraint("ck_dia_nao_util_municipio_coerente", "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)");
                    table.CheckConstraint("ck_dia_nao_util_uf_coerente", "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_dia_nao_util_calendario_dias_uteis_calendario_dias_uteis_id",
                        column: x => x.calendario_dias_uteis_id,
                        principalSchema: "configuracao",
                        principalTable: "calendario_dias_uteis",
                        principalColumn: "id");
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

            migrationBuilder.CreateTable(
                name: "convocacoes",
                schema: "ingresso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chamada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    protocolo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    posicao = table.Column<int>(type: "integer", nullable: false),
                    codigo_curso = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_manifestacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convocacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_convocacoes_chamadas_chamada_id",
                        column: x => x.chamada_id,
                        principalSchema: "ingresso",
                        principalTable: "chamadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permissoes_implantadas",
                schema: "autorizacao",
                columns: table => new
                {
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    modulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    classificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    de_unidade = table.Column<bool>(type: "boolean", nullable: false),
                    reservada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissoes_implantadas", x => new { x.implantacao_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_permissoes_implantadas_implantacao",
                        column: x => x.implantacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "implantacoes_das_permissoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "aceites_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    termo_codigo = table.Column<string>(type: "text", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_versao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceites_da_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_aceites_da_inscricao_inscricao_record_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "O aceite de um termo na inscrição, com a versão e o hash da versão fixada.");

            migrationBuilder.CreateTable(
                name: "apresentacoes_de_documento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exigencia_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "O DocumentoExigido.Id da árvore congelada da versão da inscrição. Sem chave estrangeira para a configuração viva (ADR-0070)."),
                    ocorrencia_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "A ocorrência do grupo repetível a que o documento se refere; nulo quando a exigência não se repete."),
                    nome_do_documento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "O nome do documento como a exigência o chamava no envio; dá nome ao arquivo baixado."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_apresentacoes_de_documento", x => x.id);
                    table.CheckConstraint("ck_apresentacoes_de_documento_nome_do_documento_preenchido", "btrim(nome_do_documento) <> ''");
                    table.ForeignKey(
                        name: "fk_apresentacoes_de_documento_inscricao_record_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "A apresentação de um documento exigido pela inscrição: a unidade que a exigência conta, com um ou mais arquivos.");

            migrationBuilder.CreateTable(
                name: "comprovantes_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_inscricao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hash_integridade = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    oferta_curso_segunda_opcao_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "A oferta de curso da 2ª opção; nulo quando a inscrição não a declarou nesta submissão."),
                    respostas = table.Column<string>(type: "jsonb", nullable: false),
                    derivados = table.Column<string>(type: "jsonb", nullable: false, comment: "O que o sistema derivou das respostas nesta submissão, como a UF a partir do endereço."),
                    grupos = table.Column<string>(type: "jsonb", nullable: false),
                    aceites = table.Column<string>(type: "jsonb", nullable: false),
                    documentos = table.Column<string>(type: "jsonb", nullable: false, comment: "As exigências documentais da inscrição neste envio, com a situação, a consequência escrita e, de cada arquivo, SHA-256, tamanho e formato — nunca nome nem local de armazenamento."),
                    sensiveis_cifrados = table.Column<byte[]>(type: "bytea", nullable: true, comment: "Respostas de dado pessoal sensível com cifra individual (identidade de gênero, orientação sexual), cifradas (AES-GCM, ADR-0121, chave dos sensíveis) — nunca texto claro."),
                    sensiveis_hmac = table.Column<byte[]>(type: "bytea", nullable: true, comment: "HMAC do id do comprovante e das respostas sensíveis, que prende o envelope cifrado a este comprovante."),
                    modalidades = table.Column<string>(type: "jsonb", nullable: true, comment: "As modalidades em que o candidato concorre nesta submissão, com o nome congelado e a ordem das ofertadas; nulo quando a versão não deriva a modalidade."),
                    modalidades_segunda_opcao = table.Column<string>(type: "jsonb", nullable: true, comment: "As modalidades em que o candidato concorre na 2ª opção, sobre a distribuição de vagas da oferta dela; nulo quando não há 2ª opção ou a versão não deriva a modalidade.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comprovantes_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_comprovantes_inscricao_inscricoes",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Comprovante de uma submissão da inscrição (issue #1858) — append-only, sem nenhum número de documento; uma correção soma uma linha nova, nunca muda a anterior.");

            migrationBuilder.CreateTable(
                name: "ocorrencias_de_grupo_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    membro_id = table.Column<string>(type: "text", nullable: false, comment: "O id estável que o formulário dá ao membro — reordenar a lista não troca a identidade dele."),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocorrencias_de_grupo_da_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_ocorrencias_de_grupo_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Uma ocorrência de grupo repetível da inscrição, como cada dependente de um grupo de dependentes.");

            migrationBuilder.CreateTable(
                name: "instalacao_gestores",
                schema: "autorizacao",
                columns: table => new
                {
                    emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacao_gestores", x => new { x.instalacao_id, x.emissor, x.sub });
                    table.ForeignKey(
                        name: "fk_instalacao_gestores_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instalacao_tentativas",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacao_tentativas", x => x.id);
                    table.ForeignKey(
                        name: "fk_instalacao_tentativas_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos_matricula",
                schema: "ingresso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    matricula_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    caminho_storage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    validado = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_rejeicao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_matricula", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_matricula_matricula_matricula_id",
                        column: x => x.matricula_id,
                        principalSchema: "ingresso",
                        principalTable: "matriculas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "obrigatoriedade_legal_historico",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conteudo_json = table.Column<string>(type: "jsonb", nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    snapshot_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obrigatoriedade_legal_historico", x => x.id);
                    table.ForeignKey(
                        name: "fk_obrigatoriedade_legal_historico_regra_id",
                        column: x => x.regra_id,
                        principalSchema: "selecao",
                        principalTable: "obrigatoriedades_legais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "desativacoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desativada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    desativada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    desativada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    desativada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reativada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reativada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reativada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reativada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_da_reativacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_desativacoes_do_perfil", x => x.id);
                    table.CheckConstraint("ck_desativacoes_do_perfil_reativacao", "(reativada_em IS NULL AND reativada_por_emissor IS NULL AND reativada_por_sub IS NULL AND reativada_por_nome IS NULL AND motivo_da_reativacao IS NULL) OR (reativada_em IS NOT NULL AND reativada_por_emissor IS NOT NULL AND reativada_por_sub IS NOT NULL AND motivo_da_reativacao IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_desativacoes_do_perfil_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "divergencias_da_governanca",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    de = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    para = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_divergencias_da_governanca", x => x.id);
                    table.CheckConstraint("ck_divergencias_da_governanca_tipo", "tipo IN ('composicao_faltante', 'composicao_excedente', 'classificacao', 'escopo')");
                    table.ForeignKey(
                        name: "fk_divergencias_da_governanca_implantacao",
                        column: x => x.implantacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "implantacoes_das_permissoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_divergencias_da_governanca_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "perfil_permissoes",
                schema: "autorizacao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil_permissoes", x => new { x.perfil_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_perfil_permissoes_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reclassificacoes_da_permissao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    classificacao_anterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classificacao_nova = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    de_unidade_anterior = table.Column<bool>(type: "boolean", nullable: false),
                    de_unidade_novo = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reclassificacoes_da_permissao", x => x.id);
                    table.CheckConstraint("ck_reclassificacoes_da_permissao_mudanca", "classificacao_anterior <> classificacao_nova OR de_unidade_anterior <> de_unidade_novo");
                    table.ForeignKey(
                        name: "fk_reclassificacoes_da_permissao_implantacao",
                        column: x => x.implantacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "implantacoes_das_permissoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reclassificacoes_da_permissao_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suspensoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suspensa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    suspensa_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    suspensa_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    suspensa_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    papel_de_quem_suspendeu = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    retomada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suspensoes_do_perfil", x => x.id);
                    table.CheckConstraint("ck_suspensoes_do_perfil_papel", "papel_de_quem_suspendeu IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.ForeignKey(
                        name: "fk_suspensoes_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    protocolo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alcance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    exige_protecao_de_dados = table.Column<bool>(type: "boolean", nullable: false),
                    ciclo_de_aprovacao = table.Column<int>(type: "integer", nullable: false),
                    criador_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    criada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    criada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    criada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    criada_por_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    motivo_da_remocao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categoria_da_remocao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    versao_do_documento = table.Column<Guid>(type: "uuid", nullable: true),
                    forma_do_cancelamento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    motivo_do_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expirada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_do_perfil", x => x.id);
                    table.UniqueConstraint("ak_versoes_do_perfil_perfil_versao", x => new { x.perfil_id, x.id });
                    table.CheckConstraint("ck_versoes_do_perfil_cancelamento", "(situacao = 'cancelada') = (forma_do_cancelamento IS NOT NULL AND cancelada_em IS NOT NULL) AND (forma_do_cancelamento IS NULL) = (cancelada_em IS NULL)");
                    table.CheckConstraint("ck_versoes_do_perfil_expiracao", "(situacao = 'expirada') = (expirada_em IS NOT NULL)");
                    table.CheckConstraint("ck_versoes_do_perfil_motivo_do_cancelamento", "forma_do_cancelamento IS DISTINCT FROM 'pessoa' OR motivo_do_cancelamento IS NOT NULL");
                    table.CheckConstraint("ck_versoes_do_perfil_origem", "(origem = 'remocao' AND motivo_da_remocao IS NOT NULL AND versao_do_documento IS NOT NULL AND expira_em IS NULL) OR (origem = 'proposta' AND expira_em IS NOT NULL AND motivo_da_remocao IS NULL AND versao_do_documento IS NULL)");
                    table.ForeignKey(
                        name: "fk_versoes_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versoes_do_perfil_versoes_do_perfil_perfil_id_versao_do_doc",
                        columns: x => new { x.perfil_id, x.versao_do_documento },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "peso_area_enem_area",
                schema: "configuracao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código da área do ENEM, sem abreviação e sem acento (ex.: REDACAO); identidade estável."),
                    peso_area_enem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Linha de Pesos por Área (resolução e grupo de área) a que a área pertence."),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Rótulo oficial da área (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código."),
                    peso = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, comment: "Peso da área na média ponderada da nota do ENEM: multiplicador adimensional, de 0 a 99,99."),
                    corte = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: true, comment: "Nota mínima da área na escala do ENEM (0 a 1000). Nulo quando a área não tem corte.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peso_area_enem_area", x => new { x.peso_area_enem_id, x.codigo });
                    table.CheckConstraint("ck_peso_area_enem_area_codigo", "codigo IN ('REDACAO', 'CIENCIAS_DA_NATUREZA', 'CIENCIAS_HUMANAS', 'LINGUAGENS', 'MATEMATICA')");
                    table.CheckConstraint("ck_peso_area_enem_area_corte", "corte IS NULL OR (corte >= 0 AND corte <= 1000)");
                    table.CheckConstraint("ck_peso_area_enem_area_peso", "peso >= 0");
                    table.ForeignKey(
                        name: "fk_peso_area_enem_area_peso_area_enem_peso_area_enem_id",
                        column: x => x.peso_area_enem_id,
                        principalSchema: "configuracao",
                        principalTable: "peso_area_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peso e corte de cada uma das cinco áreas do ENEM numa linha de Pesos por Área. Código e rótulo são postos pelo sistema; o operador edita só peso e corte.");

            migrationBuilder.CreateTable(
                name: "configuracoes_bonus_regional",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    fator = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    teto = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    fato_elegibilidade_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    base_legal_bonus_regional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_instrumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    identificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_bonus_regional", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_bonus_regional_processo_seletivo_processo_sel",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_cascata_remanejamento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    fallback_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_cascata_remanejamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_cascata_remanejamento_processo_seletivo_proce",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_classificacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_calculo_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_calculo_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_calculo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    regra_arredondamento_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    regra_arredondamento_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    regra_arredondamento_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    casas_arredondamento = table.Column<int>(type: "integer", nullable: true),
                    escala_nota_final = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true, comment: "Escala em que a nota final é publicada (10, 100, 1.000...); a média ponderada das notas convertidas é multiplicada por ela. Nula na classificação importada e no rascunho que ainda não a declarou."),
                    regra_ordem_alocacao_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_ordem_alocacao_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_ordem_alocacao_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    baseado_em_enem = table.Column<bool>(type: "boolean", nullable: false, comment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito (Story #850) do qual as regras de eliminação do ENEM dependem, substituindo a ramificação por TipoProcesso."),
                    resolucao_peso_area_enem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true, comment: "Resolução de Pesos por Área declarada pela classificação baseada em ENEM com cálculo local; o vínculo com o cadastro é pelo valor, e o quadro fica congelado em grupos_peso_area_enem_congelados. Nulo nas demais classificações."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_classificacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_classificacao_processo_seletivo_processo_sele",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_derivacao_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_derivacao_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_derivacao_fato_processo_seletivo_processo_sel",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_distribuicao_vagas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oferta_curso_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vo_base = table.Column<int>(type: "integer", nullable: false),
                    pr = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    regra_distribuicao_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_distribuicao_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_distribuicao_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    referencia_demografica_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    referencia_demografica_censo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    referencia_demografica_ppi_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    referencia_demografica_quilombola_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    referencia_demografica_pcd_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    referencia_demografica_base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    regra_ajuste_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    regra_ajuste_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    regra_ajuste_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Código do grupo de área do ENEM do curso da oferta, sem abreviação e sem acento, congelado por valor do cadastro de cursos na definição da distribuição; casa a oferta com a linha de Pesos por Área. Nulo quando o curso não declara grupo."),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Rótulo do grupo de área do ENEM do curso da oferta, congelado por valor junto do código na definição da distribuição. Nulo quando o curso não declara grupo."),
                    oferta_curso_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "Nome do curso da oferta, congelado por valor do cadastro na definição da distribuição."),
                    oferta_curso_grau = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Grau do curso da oferta, congelado por valor do cadastro na definição da distribuição."),
                    oferta_unidade_ofertante_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true, comment: "Nome da unidade ofertante, congelado por valor do cadastro na definição da distribuição."),
                    oferta_turnos = table.Column<string[]>(type: "text[]", nullable: true, comment: "Turnos que a oferta ocupa (tokens), congelados por valor do cadastro na definição da distribuição."),
                    vr_nominal = table.Column<int>(type: "integer", nullable: false),
                    vr_final = table.Column<int>(type: "integer", nullable: false),
                    estouro = table.Column<int>(type: "integer", nullable: false),
                    capado_em_vo = table.Column<bool>(type: "boolean", nullable: false),
                    total_publicado = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_distribuicao_vagas", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_distribuicao_vagas_processo_seletivo_processo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_divulgacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campos_publicos = table.Column<string>(type: "jsonb", nullable: false),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_divulgacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_divulgacao_processo_seletivo_processo_seletiv",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_taxa_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) — não confundir com o Id do processo seletivo, a FK."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id do processo seletivo dono desta configuração (FK 1:1, cascade delete)."),
                    cobra = table.Column<bool>(type: "boolean", nullable: false, comment: "Declaração explícita de cobrança de taxa — nunca inferida pela ausência da linha (CA-01)."),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, comment: "Valor da taxa em reais, positivo quando cobra=true; sempre nulo quando cobra=false (CA-02/CA-03)."),
                    fundamentos = table.Column<string>(type: "jsonb", nullable: false, comment: "Fundamentos de isenção referenciados (tokens de FundamentoIsencaoCodigo), deduplicados e em ordem canônica; vazio somente quando cobra=false (issue #1310)."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_taxa_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_taxa_inscricao_processo_seletivo_processo_sel",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configuração de taxa de inscrição e fundamentos de isenção do processo seletivo (issue #1112) — entidade dependente 1:1 de processos_seletivos.");

            migrationBuilder.CreateTable(
                name: "criterios_desempate",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    args = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criterios_desempate", x => x.id);
                    table.ForeignKey(
                        name: "fk_criterios_desempate_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos_a_publicar",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    formato = table.Column<int>(type: "integer", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    object_key_confirmado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_a_publicar", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_a_publicar_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "etapas_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código canônico da fase em que a etapa acontece; é por ele que a raiz a resolve no cronograma."),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    carater = table.Column<int>(type: "integer", nullable: false),
                    tipo_etapa_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id de origem do tipo de etapa em Configuração, sem FK cross-schema; congelado na definição."),
                    tipo_etapa_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tipo_etapa_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_etapa_admite_pontuacao = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_etapa_admite_eliminacao = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_etapa_nota_de_origem_no_enem = table.Column<bool>(type: "boolean", nullable: false, comment: "Nota da etapa vem do ENEM, congelado do tipo junto com a identidade."),
                    peso = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    nota_minima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    nota_maxima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "Nota máxima da escala da etapa com nota; ausente na etapa sem nota e na do ENEM."),
                    passo_do_lancamento = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "De quanto em quanto a nota pode ser lançada; vale só para a nota digitada."),
                    forma_de_avaliacao = table.Column<int>(type: "integer", nullable: true, comment: "Como a nota da etapa é obtida (1 objetiva, 2 humana); ausente em rascunho, na etapa sem nota e na do ENEM."),
                    regra_consolidacao_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    regra_consolidacao_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    regra_consolidacao_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    escala_de_atendimento = table.Column<string>(type: "jsonb", nullable: true, comment: "Níveis de atendimento (rótulo e fração da pontuação do item), do que mais vale ao que menos vale; ausente quando a banca digita a nota."),
                    ordem = table.Column<int>(type: "integer", nullable: true),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    emite_parecer_individual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas_processo", x => x.id);
                    table.CheckConstraint("ck_etapas_processo_forma_de_avaliacao_valida", "forma_de_avaliacao IS NULL OR forma_de_avaliacao IN (1, 2)");
                    table.CheckConstraint("ck_etapas_processo_nota_maxima_positiva", "nota_maxima IS NULL OR nota_maxima > 0");
                    table.CheckConstraint("ck_etapas_processo_passo_positivo", "passo_do_lancamento IS NULL OR passo_do_lancamento > 0");
                    table.ForeignKey(
                        name: "fk_etapas_processo_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fases_cronograma",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    fase_canonica_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unidade_responsavel_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_responsavel_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    unidade_responsavel_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    origem_data = table.Column<int>(type: "integer", nullable: false),
                    agrupa_etapas = table.Column<bool>(type: "boolean", nullable: false),
                    permite_complementacao = table.Column<bool>(type: "boolean", nullable: false),
                    coleta_inscricao = table.Column<bool>(type: "boolean", nullable: false),
                    coleta_solicitacao_isencao = table.Column<bool>(type: "boolean", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fase_concluinte_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    emite_parecer_individual = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fases_cronograma", x => x.id);
                    table.ForeignKey(
                        name: "fk_fases_cronograma_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "formularios_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    fase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    modelo_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modelo_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formularios_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_formularios_processo_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "geracoes_de_envio_ao_inep",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edicao_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    leiaute_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    leiaute_versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    leiaute_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    gerado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "O sub do token de quem gerou."),
                    gerado_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "O nome de quem gerou, gravado no momento da ação."),
                    gerado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_de_cpfs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geracoes_de_envio_ao_inep", x => x.id);
                    table.CheckConstraint("ck_geracoes_de_envio_ao_inep_leiaute_hash", "leiaute_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_geracoes_de_envio_ao_inep_total", "total_de_cpfs >= 0");
                    table.ForeignKey(
                        name: "fk_geracoes_de_envio_ao_inep_processo_seletivo_processo_seleti",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "O registro de que o arquivo de envio ao INEP foi gerado para uma edição do ENEM. Append-only; o arquivo e os CPFs não são guardados.");

            migrationBuilder.CreateTable(
                name: "grupos_coletados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    etapa_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    minimo = table.Column<int>(type: "integer", nullable: false),
                    maximo = table.Column<int>(type: "integer", nullable: true),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false),
                    inclui_candidato = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_coletados", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_coletados_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modelos_de_documento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    formato = table.Column<int>(type: "integer", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    object_key_confirmado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos_de_documento", x => x.id);
                    table.ForeignKey(
                        name: "fk_modelos_de_documento_processo_seletivo_processo_seletivo_id",
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
                        name: "fk_ofertas_atendimento_especializado_processo_seletivo_process",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opcoes_declaradas_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opcoes_declaradas_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_opcoes_declaradas_fato_processo_seletivo_processo_seletivo_",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rascunhos_publicacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rascunhos_publicacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_rascunhos_publicacao_processos_seletivos_processo_seletivo_",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rascunhos_retificacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    versao_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_versao_base = table.Column<int>(type: "integer", nullable: false),
                    versao_base_com_identificador_legivel = table.Column<bool>(type: "boolean", nullable: false),
                    fatos_das_versoes_publicadas = table.Column<string>(type: "jsonb", nullable: false),
                    aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aberto_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rascunhos_retificacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_rascunhos_retificacao_processos_seletivos_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "termos_exigidos_formulario",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    texto = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    forma_aceite = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hash_versao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_exigidos_formulario", x => x.id);
                    table.ForeignKey(
                        name: "fk_termos_exigidos_formulario_processos_seletivos_processo_sel",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "versoes_configuracao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_versao = table.Column<int>(type: "integer", nullable: false),
                    vigente_a_partir_de = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    algoritmo_hash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    configuracao_congelada_canonica = table.Column<byte[]>(type: "bytea", nullable: false),
                    configuracao_congelada = table.Column<string>(type: "jsonb", nullable: false),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ato_criador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_criador_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ato_criador_retifica_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ator_usuario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_configuracao", x => x.id);
                    table.CheckConstraint("ck_versoes_configuracao_ato_criador_hash", "ato_criador_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versoes_configuracao_ato_criador_nao_zero", "ato_criador_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_versoes_configuracao_contrato_abertura", "(numero_versao = 1 AND ato_criador_retifica_id IS NULL) OR (numero_versao > 1 AND ato_criador_retifica_id IS NOT NULL)");
                    table.CheckConstraint("ck_versoes_configuracao_hash_configuracao", "hash_configuracao ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versoes_configuracao_nao_autorretifica", "ato_criador_retifica_id IS NULL OR ato_criador_retifica_id <> ato_criador_id");
                    table.CheckConstraint("ck_versoes_configuracao_numero_positivo", "numero_versao > 0");
                    table.ForeignKey(
                        name: "fk_versoes_configuracao_processo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fato_valor_dominio",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    orientacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fato_valor_dominio", x => x.id);
                    table.ForeignKey(
                        name: "fk_fato_valor_dominio_fato_candidato_fato_candidato_id",
                        column: x => x.fato_candidato_id,
                        principalSchema: "configuracao",
                        principalTable: "rol_de_fatos_candidato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "termo_consentimento_versao",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    termo_consentimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    base_legal = table.Column<string>(type: "text", nullable: false),
                    forma_aceite = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    promovida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    promovida_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termo_consentimento_versao", x => x.id);
                    table.CheckConstraint("ck_termo_consentimento_versao_forma_aceite", "forma_aceite IN ('A_DEFINIR', 'REGISTRO_DIGITAL_SEM_LOG_IP', 'REGISTRO_DIGITAL_COM_LOG_IP')");
                    table.ForeignKey(
                        name: "fk_termo_consentimento_versao_termo_consentimento_termo_consen",
                        column: x => x.termo_consentimento_id,
                        principalSchema: "configuracao",
                        principalTable: "termo_consentimento",
                        principalColumn: "id");
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
                        name: "fk_instituicao_unidade_unidade_raiz_id",
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

            migrationBuilder.CreateTable(
                name: "oferta_curso",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_oferta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_oft_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_oft_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unidade_oft_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unidade_oft_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    programa_de_oferta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    formato_pedagogico = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    regime_de_funcionamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    regime_de_turno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    turnos = table.Column<string[]>(type: "character varying(30)[]", nullable: false),
                    e_mec_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codigo_sga = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    vagas_anuais_autorizadas = table.Column<int>(type: "integer", nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ato_autorizacao_mec = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("pk_oferta_curso", x => x.id);
                    table.CheckConstraint("ck_oferta_curso_base_legal_programa", "programa_de_oferta = 'REGULAR' OR base_legal IS NOT NULL");
                    table.CheckConstraint("ck_oferta_curso_formato_pedagogico", "formato_pedagogico IN ('PRESENCIAL', 'SEMIPRESENCIAL', 'EAD')");
                    table.CheckConstraint("ck_oferta_curso_funcionamento_regime_de_turno", "(regime_de_funcionamento <> 'INTENSIVO' OR regime_de_turno = 'INTEGRAL')");
                    table.CheckConstraint("ck_oferta_curso_programa_de_oferta", "programa_de_oferta IN ('REGULAR', 'FORMA_PARA', 'PARFOR', 'PRONERA', 'PEPETI', 'CONVENIO_OUTRO', 'OUTRO')");
                    table.CheckConstraint("ck_oferta_curso_regime_de_funcionamento", "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO', 'ALTERNANCIA_PEDAGOGICA')");
                    table.CheckConstraint("ck_oferta_curso_regime_de_turno", "regime_de_turno IN ('REGULAR', 'INTEGRAL')");
                    table.CheckConstraint("ck_oferta_curso_turnos_dominio", "turnos::text[] <@ ARRAY['MATUTINO', 'VESPERTINO', 'NOTURNO']::text[]");
                    table.CheckConstraint("ck_oferta_curso_turnos_regime", "coalesce(array_ndims(turnos), 0) = 1 AND coalesce(array_lower(turnos, 1), 1) = 1 AND ((regime_de_turno = 'REGULAR' AND cardinality(turnos) = 1) OR (regime_de_turno = 'INTEGRAL' AND cardinality(turnos) = 2 AND turnos[1] <> turnos[2]))");
                    table.CheckConstraint("ck_oferta_curso_vagas_anuais_autorizadas", "vagas_anuais_autorizadas IS NULL OR vagas_anuais_autorizadas >= 0");
                    table.ForeignKey(
                        name: "fk_oferta_curso_curso_curso_id",
                        column: x => x.curso_id,
                        principalSchema: "configuracao",
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_oferta_curso_local_oferta_local_oferta_id",
                        column: x => x.local_oferta_id,
                        principalSchema: "configuracao",
                        principalTable: "local_oferta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apresentacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    formato = table.Column<int>(type: "integer", nullable: false, comment: "O formato do arquivo (PDF, JPEG ou PNG), conferido contra a exigência no início do envio."),
                    tamanho_maximo_bytes = table.Column<long>(type: "bigint", nullable: false, comment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite, e nunca acima de 20 MB. Copiado da exigência no início do envio."),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "A chave do objeto no bucket de envio (quarentena); só o id do arquivo a compõe."),
                    situacao = table.Column<int>(type: "integer", nullable: false, comment: "Pendente (0), em verificação (1), verificado (2), recusado (3) ou reenvio necessário (4). Só o verificado atende a exigência."),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "O SHA-256 do conteúdo, calculado pelo verificador; nulo enquanto não verificado."),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    etag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "O ETag que o PUT devolveu ao candidato, enviado na confirmação; fixa a versão do objeto que o verificador confere."),
                    publicacoes = table.Column<int>(type: "integer", nullable: false, comment: "Quantas vezes o pedido de verificação foi publicado; o prazo da reconciliação dobra a cada uma."),
                    proxima_publicacao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Quando a reconciliação pode republicar o pedido: a última publicação mais o prazo, que cresce a cada publicação."),
                    chave_definitiva = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "A chave do objeto promovido ao bucket definitivo; nula enquanto não verificado."),
                    motivo_da_recusa = table.Column<int>(type: "integer", nullable: true, comment: "Por que a verificação recusou o arquivo; nulo fora da recusa."),
                    constou_de_envio = table.Column<bool>(type: "boolean", nullable: false, comment: "O arquivo verificado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168)."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arquivos_da_apresentacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_arquivos_da_apresentacao_apresentacoes_de_documento_apresen",
                        column: x => x.apresentacao_id,
                        principalSchema: "selecao",
                        principalTable: "apresentacoes_de_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_arquivos_da_apresentacao_inscricao_record_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "O arquivo de uma apresentação de documento da inscrição. O conteúdo fica no armazenamento privado, cifrado pelo próprio armazenamento; o nome original do arquivo não é guardado.");

            migrationBuilder.CreateTable(
                name: "enderecos_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    cep = table.Column<string>(type: "text", nullable: true),
                    logradouro = table.Column<string>(type: "text", nullable: true),
                    numero = table.Column<string>(type: "text", nullable: true),
                    complemento = table.Column<string>(type: "text", nullable: true),
                    bairro = table.Column<string>(type: "text", nullable: true),
                    distrito = table.Column<string>(type: "text", nullable: true),
                    municipio_codigo_ibge = table.Column<string>(type: "text", nullable: true),
                    municipio_nome = table.Column<string>(type: "text", nullable: true),
                    uf = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<string>(type: "text", nullable: true),
                    longitude = table.Column<string>(type: "text", nullable: true),
                    nivel_resolucao = table.Column<string>(type: "text", nullable: true),
                    procedencia = table.Column<string>(type: "text", nullable: true, comment: "Como o endereço foi preenchido no formulário: pela busca do CEP no Geo ou à mão."),
                    cep_confirmado = table.Column<bool>(type: "boolean", nullable: true, comment: "O candidato confirmou que o CEP é o geral da cidade ou do bairro, quando o Geo só o resolveu por faixa.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enderecos_da_inscricao", x => x.id);
                    table.CheckConstraint("ck_enderecos_da_inscricao_origem", "origem IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_enderecos_da_inscricao_inscricao_record_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_enderecos_da_inscricao_ocorrencia_de_grupo_da_inscricao_rec",
                        column: x => x.ocorrencia_id,
                        principalSchema: "selecao",
                        principalTable: "ocorrencias_de_grupo_da_inscricao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "A resposta de um campo de endereço da inscrição, um componente por coluna.");

            migrationBuilder.CreateTable(
                name: "respostas_da_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    posicao = table.Column<int>(type: "integer", nullable: true),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor_booleano = table.Column<bool>(type: "boolean", nullable: true),
                    valor_numero = table.Column<decimal>(type: "numeric", nullable: true),
                    valor_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    valor_rotulo = table.Column<string>(type: "text", nullable: true, comment: "O nome que o Geo conferiu para o município de valor_codigo; nulo no rascunho e preenchido na inscrição enviada."),
                    valor_texto = table.Column<string>(type: "text", nullable: true),
                    valor_data = table.Column<DateOnly>(type: "date", nullable: true),
                    valor_cifrado = table.Column<byte[]>(type: "bytea", nullable: true, comment: "Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_respostas_da_inscricao", x => x.id);
                    table.CheckConstraint("ck_respostas_da_inscricao_origem", "origem IN (1, 2)");
                    table.CheckConstraint("ck_respostas_da_inscricao_valor_do_tipo", "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo IN (1, 2, 3, 4, 5, 7) AND (valor_cifrado IS NOT NULL OR (tipo = 1 AND valor_booleano IS NOT NULL) OR (tipo = 2 AND valor_numero IS NOT NULL) OR (tipo = 3 AND valor_codigo IS NOT NULL) OR (tipo = 4 AND valor_texto IS NOT NULL) OR (tipo = 5 AND valor_data IS NOT NULL) OR (tipo = 7 AND valor_codigo IS NOT NULL)) AND (valor_rotulo IS NULL OR (tipo = 7 AND valor_codigo IS NOT NULL))");
                    table.ForeignKey(
                        name: "fk_respostas_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_respostas_da_inscricao_ocorrencias_de_grupo_da_inscricao_oc",
                        column: x => x.ocorrencia_id,
                        principalSchema: "selecao",
                        principalTable: "ocorrencias_de_grupo_da_inscricao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Um valor respondido na inscrição, na coluna do tipo do fato, com a origem: informado pelo candidato ou derivado de outra resposta.");

            migrationBuilder.CreateTable(
                name: "decisoes_da_retomada",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suspensao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    perfil_protegido_na_decisao = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_retomada", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_retomada_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.ForeignKey(
                        name: "fk_decisoes_da_retomada_suspensoes_do_perfil_suspensao_id",
                        column: x => x.suspensao_id,
                        principalSchema: "autorizacao",
                        principalTable: "suspensoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo = table.Column<int>(type: "integer", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    acrescimo_avaliado = table.Column<string[]>(type: "text[]", nullable: false),
                    exigia_protecao_de_dados = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_versao", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_versao_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.CheckConstraint("ck_decisoes_da_versao_motivo_na_recusa", "(resultado = 'recusado') = (motivo IS NOT NULL)");
                    table.CheckConstraint("ck_decisoes_da_versao_resultado", "resultado IN ('aprovado', 'recusado')");
                    table.ForeignKey(
                        name: "fk_decisoes_da_versao_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tamanho_declarado = table.Column<long>(type: "bigint", nullable: false),
                    etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recusado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_da_versao", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_da_versao_versoes_do_perfil_perfil_id_versao_id",
                        columns: x => new { x.perfil_id, x.versao_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "janelas_de_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_nova_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_da_versao_nova = table.Column<int>(type: "integer", nullable: false),
                    versao_alvo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    prazo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    forma_do_encerramento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motivo_do_encerramento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    encerrada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encerrada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encerrada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aviso_48h_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aviso_24h_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_janelas_de_migracao", x => x.id);
                    table.UniqueConstraint("ak_janela_de_migracao_id_perfil_id", x => new { x.id, x.perfil_id });
                    table.CheckConstraint("ck_janelas_de_migracao_encerramento", "(encerrada_em IS NULL) = (forma_do_encerramento IS NULL)");
                    table.CheckConstraint("ck_janelas_de_migracao_forma", "forma_do_encerramento IS NULL OR forma_do_encerramento IN ('concluida', 'prazo', 'manual')");
                    table.CheckConstraint("ck_janelas_de_migracao_manual", "COALESCE(forma_do_encerramento = 'manual', false) = (motivo_do_encerramento IS NOT NULL AND encerrada_por_sub IS NOT NULL)");
                    table.CheckConstraint("ck_janelas_de_migracao_prazo", "prazo = aberta_em + interval '72 hours'");
                    table.ForeignKey(
                        name: "fk_janelas_de_migracao_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_janelas_de_migracao_versao_do_perfil_perfil_id_versao_alvo_",
                        columns: x => new { x.perfil_id, x.versao_alvo_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_janelas_de_migracao_versao_do_perfil_perfil_id_versao_nova_",
                        columns: x => new { x.perfil_id, x.versao_nova_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recalculos_da_aprovacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_anterior = table.Column<int>(type: "integer", nullable: false),
                    exigia_antes = table.Column<bool>(type: "boolean", nullable: false),
                    exige_agora = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recalculos_da_aprovacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_recalculos_da_aprovacao_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "versao_do_perfil_permissoes",
                schema: "autorizacao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    de_unidade = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versao_do_perfil_permissoes", x => new { x.versao_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_versao_do_perfil_permissoes_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vigencias_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vigencias_do_perfil", x => x.id);
                    table.ForeignKey(
                        name: "fk_vigencias_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vigencias_do_perfil_versoes_do_perfil_perfil_id_versao_id",
                        columns: x => new { x.perfil_id, x.versao_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_bonus_regional_municipio",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_bonus_regional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_bonus_regional_municipio", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_bonus_regional_municipio_configuracoes_bonus_",
                        column: x => x.configuracao_bonus_regional_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_bonus_regional",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "destinos_remanejamento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_cascata_remanejamento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    modalidade_destino_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinos_remanejamento", x => x.id);
                    table.CheckConstraint("ck_destinos_remanejamento_ordem_positiva", "ordem >= 1");
                    table.CheckConstraint("ck_destinos_remanejamento_origem_diferente_destino", "modalidade_origem_codigo <> modalidade_destino_codigo");
                    table.ForeignKey(
                        name: "fk_destinos_remanejamento_configuracoes_cascata_remanejamento_",
                        column: x => x.configuracao_cascata_remanejamento_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_cascata_remanejamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grupos_peso_area_enem_congelados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) do grupo congelado."),
                    configuracao_classificacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id da configuração de classificação dona do quadro (FK, cascade delete)."),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código do grupo de área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área; casa a oferta com a linha do quadro."),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Rótulo do grupo de área do ENEM, copiado junto do código."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Dispositivo legal que fundamenta os pesos do grupo, copiado da resolução de Pesos por Área."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_peso_area_enem_congelados", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_peso_area_enem_congelados_configuracoes_classificaca",
                        column: x => x.configuracao_classificacao_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_classificacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Quadro de pesos por área do ENEM congelado na classificação do processo seletivo: uma linha por grupo de área, copiada por valor da resolução de Pesos por Área declarada. Substituída por inteiro quando a classificação é redefinida.");

            migrationBuilder.CreateTable(
                name: "regras_eliminacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_classificacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    args = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_eliminacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_eliminacao_configuracoes_classificacao_configuracao_",
                        column: x => x.configuracao_classificacao_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_classificacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_derivacao_configuradas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_derivacao_fato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    contribui = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_derivacao_configuradas", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_derivacao_configuradas_configuracoes_derivacao_fato_",
                        column: x => x.configuracao_derivacao_fato_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_derivacao_fato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modalidades_selecionadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_distribuicao_vagas_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    natureza_legal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_vagas = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    composicao_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    regra_remanejamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    remanejamento_destino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    remanejamento_par = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    remanejamento_fallback = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    regra_de_aplicabilidade = table.Column<string>(type: "jsonb", nullable: false),
                    acao_quando_indeferido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantidade_declarada = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modalidades_selecionadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_modalidades_selecionadas_configuracoes_distribuicao_vagas_c",
                        column: x => x.configuracao_distribuicao_vagas_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_distribuicao_vagas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vagas_ofertadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracao_distribuicao_vagas_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vagas_ofertadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_vagas_ofertadas_configuracoes_distribuicao_vagas_configurac",
                        column: x => x.configuracao_distribuicao_vagas_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_distribuicao_vagas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "atos_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    data_do_ato = table.Column<DateOnly>(type: "date", nullable: false),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assinante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unidade_administradora = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    identificador_legivel_do_processo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_do_edital = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    documento_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_corrigido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_da_correcao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    politica_de_divulgacao_confirmada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    politica_de_divulgacao_confirmada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atos_do_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_atos_do_processo_documento_a_publicar_documento_principal_id",
                        column: x => x.documento_principal_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_a_publicar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_atos_do_processo_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bancas_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_banca_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    regra_deliberacao_modo = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_integrantes = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_suplentes = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_quorum = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_criterio = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_desempate = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_encerramento = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_sem_unanimidade = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancas_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_bancas_da_etapa_etapa_processo_etapa_processo_id",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itens_de_avaliacao_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    pontuacao_maxima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, comment: "O quanto o item vale da nota máxima da etapa."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_de_avaliacao_da_etapa", x => x.id);
                    table.CheckConstraint("ck_itens_de_avaliacao_da_etapa_pontuacao_positiva", "pontuacao_maxima > 0");
                    table.ForeignKey(
                        name: "fk_itens_de_avaliacao_da_etapa_etapas_processo_etapa_processo_",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Itens de avaliação (critérios ou blocos) da etapa; o id é a identidade a que lançamento e recurso apontam.");

            migrationBuilder.CreateTable(
                name: "produtos_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtos_da_etapa_etapas_processo_etapa_processo_id",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recursos_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ancora = table.Column<int>(type: "integer", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    prazo_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    prazo_unidade = table.Column<int>(type: "integer", nullable: false),
                    susp_1a_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    susp_1a_unidade = table.Column<int>(type: "integer", nullable: true),
                    admite_instancia_superior = table.Column<bool>(type: "boolean", nullable: false),
                    susp_2a_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    susp_2a_unidade = table.Column<int>(type: "integer", nullable: true),
                    produto_ancora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recursos_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_recursos_da_etapa_etapas_processo_etapa_processo_id",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bancas_requeridas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_banca_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    regra_deliberacao_modo = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_integrantes = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_suplentes = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_quorum = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_criterio = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_desempate = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_encerramento = table.Column<int>(type: "integer", nullable: true),
                    regra_deliberacao_sem_unanimidade = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancas_requeridas", x => x.id);
                    table.ForeignKey(
                        name: "fk_bancas_requeridas_fase_cronograma_fase_cronograma_id",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos_exigidos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exigido_na_fase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exigido_na_etapa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    finalidade = table.Column<int>(type: "integer", nullable: true),
                    tipo_documento_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo_documento_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_documento_categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fatos_comprovados = table.Column<string[]>(type: "text[]", nullable: false),
                    aplicabilidade = table.Column<int>(type: "integer", nullable: false),
                    obrigatorio = table.Column<bool>(type: "boolean", nullable: false),
                    consequencia_indeferimento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    idade_maxima_valor = table.Column<int>(type: "integer", nullable: true),
                    idade_maxima_unidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    idade_maxima_referencia_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    idade_maxima_referencia_data = table.Column<DateOnly>(type: "date", nullable: true),
                    idade_maxima_referencia_fase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    formatos_permitidos_qualquer = table.Column<bool>(type: "boolean", nullable: false),
                    formatos_permitidos_lista = table.Column<string>(type: "jsonb", nullable: true),
                    tamanho_maximo_bytes = table.Column<int>(type: "integer", nullable: true),
                    modelo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modelo_nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    modelo_formato = table.Column<int>(type: "integer", nullable: true),
                    modelo_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_exigidos", x => x.id);
                    table.CheckConstraint("ck_documentos_exigidos_formatos_permitidos_coerente", "(formatos_permitidos_qualquer = true AND formatos_permitidos_lista IS NULL) OR (formatos_permitidos_qualquer = false AND formatos_permitidos_lista IS NOT NULL AND jsonb_array_length(formatos_permitidos_lista) > 0)");
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_etapa_processo_exigido_na_etapa_id",
                        column: x => x.exigido_na_etapa_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_fase_cronograma_exigido_na_fase_id",
                        column: x => x.exigido_na_fase_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produtos_da_fase",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos_da_fase", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtos_da_fase_fases_cronograma_fase_cronograma_id",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regras_recurso_fase",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    prazo_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    prazo_unidade = table.Column<int>(type: "integer", nullable: false),
                    suspensividade_1a_instancia_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    suspensividade_1a_instancia_unidade = table.Column<int>(type: "integer", nullable: true),
                    admite_instancia_superior = table.Column<bool>(type: "boolean", nullable: false),
                    suspensividade_2a_instancia_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    suspensividade_2a_instancia_unidade = table.Column<int>(type: "integer", nullable: true),
                    produto_ancora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_recurso_fase", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_recurso_fase_fases_cronograma_fase_cronograma_id",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "etapas_formulario",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulario_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    bloco = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    aviso = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas_formulario", x => x.id);
                    table.ForeignKey(
                        name: "fk_etapas_formulario_formulario_processo_formulario_processo_id",
                        column: x => x.formulario_processo_id,
                        principalSchema: "selecao",
                        principalTable: "formularios_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fatos_coletados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_coletado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_renderizacao = table.Column<int>(type: "integer", nullable: false),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false),
                    ajuda = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    pedir_confirmacao = table.Column<bool>(type: "boolean", nullable: false),
                    restricoes = table.Column<string>(type: "jsonb", nullable: false),
                    origem_valores = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    classificacao_protecao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    componente_exibicao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    impedimento = table.Column<string>(type: "jsonb", nullable: true),
                    finalidade = table.Column<int>(type: "integer", nullable: false),
                    etapa_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fatos_coletados", x => x.id);
                    table.ForeignKey(
                        name: "fk_fatos_coletados_grupo_coletado_grupo_coletado_id",
                        column: x => x.grupo_coletado_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_coletados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fatos_coletados_processo_seletivo_processo_seletivo_id",
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
                    recurso_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    tipo_deficiencia_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "lotes_de_notas_do_enem",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa de nota do ENEM, guardada por valor e sem chave estrangeira: pertence à configuração do processo, que a retificação recria."),
                    edicao_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "O código da opção do fato EDICAO_ENEM, como o processo a declara."),
                    leiaute_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    leiaute_versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    leiaute_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "SHA-256 da definição do leiaute com que o arquivo foi lido."),
                    arquivo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "SHA-256 dos bytes do arquivo importado."),
                    importado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "O sub do token de quem importou."),
                    importado_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "O nome de quem importou, gravado no momento da ação."),
                    importado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versao_configuracao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A versão divulgada do certame cujos pesos apuraram as avaliações do lote."),
                    total_de_linhas = table.Column<int>(type: "integer", nullable: false),
                    total_de_inscritos_com_nota = table.Column<int>(type: "integer", nullable: false),
                    total_de_lancamentos = table.Column<int>(type: "integer", nullable: false),
                    total_de_inscritos_sem_linha = table.Column<int>(type: "integer", nullable: false),
                    total_de_linhas_sem_inscrito = table.Column<int>(type: "integer", nullable: false),
                    total_de_inscritos_em_conflito = table.Column<int>(type: "integer", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes_de_notas_do_enem", x => x.id);
                    table.UniqueConstraint("ak_lotes_de_notas_do_enem_id_etapa", x => new { x.id, x.etapa_processo_id });
                    table.CheckConstraint("ck_lotes_de_notas_do_enem_edicao", "edicao_codigo <> ''");
                    table.CheckConstraint("ck_lotes_de_notas_do_enem_hashes", "arquivo_hash ~ '^[0-9a-f]{64}$' AND leiaute_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_lotes_de_notas_do_enem_totais", "total_de_linhas >= 0 AND total_de_inscritos_com_nota >= 0 AND total_de_lancamentos >= 0 AND total_de_inscritos_sem_linha >= 0 AND total_de_linhas_sem_inscrito >= 0 AND total_de_inscritos_em_conflito >= 0");
                    table.ForeignKey(
                        name: "fk_lotes_de_notas_do_enem_processo_seletivo_processo_seletivo_",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lotes_de_notas_do_enem_versao_configuracao_versao_configura",
                        column: x => x.versao_configuracao_id,
                        principalSchema: "selecao",
                        principalTable: "versoes_configuracao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Uma importação do retorno do INEP para um processo, uma etapa e uma edição do ENEM. Append-only; vale o lote mais recente do escopo, por inteiro. O arquivo não é guardado, só o hash.");

            migrationBuilder.CreateTable(
                name: "eventos_da_contencao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    suspensao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    desativacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ator = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    decisao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    versao_que_entrou_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permissao_codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ator_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ator_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ator_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    papel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    versao_vigente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    perfil_protegido = table.Column<bool>(type: "boolean", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_da_contencao", x => x.id);
                    table.CheckConstraint("ck_eventos_da_contencao_desativacao", "tipo <> 'desativacao' OR (ator = 'humano' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_da_contencao_reativacao", "tipo <> 'reativacao' OR (ator = 'humano' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_da_contencao_retomada", "tipo <> 'retomada' OR (ator = 'sistema' AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND ator_emissor IS NULL AND ator_sub IS NULL AND ator_nome IS NULL AND papel IS NULL AND motivo IS NULL AND origem IS NOT NULL AND ((origem = 'decisao_da_etapa' AND decisao_id IS NOT NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'troca_de_vigencia' AND versao_que_entrou_id IS NOT NULL AND decisao_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'reclassificacao' AND permissao_codigo IS NOT NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL)))");
                    table.CheckConstraint("ck_eventos_da_contencao_suspensao", "tipo <> 'suspensao' OR (ator = 'humano' AND ator_emissor IS NOT NULL AND ator_sub IS NOT NULL AND motivo IS NOT NULL AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND papel IS NOT NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_da_contencao_tipo", "tipo IN ('suspensao', 'retomada', 'desativacao', 'reativacao')");
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_decisoes_da_retomada_decisao_id",
                        column: x => x.decisao_id,
                        principalSchema: "autorizacao",
                        principalTable: "decisoes_da_retomada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_desativacoes_do_perfil_desativacao_id",
                        column: x => x.desativacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "desativacoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_suspensao_do_perfil_suspensao_id",
                        column: x => x.suspensao_id,
                        principalSchema: "autorizacao",
                        principalTable: "suspensoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_versao_do_perfil_versao_que_entrou_id",
                        column: x => x.versao_que_entrou_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_da_contencao_versao_do_perfil_versao_vigente_id",
                        column: x => x.versao_vigente_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leituras_do_documento_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leitor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leituras_do_documento_da_versao", x => x.id);
                    table.ForeignKey(
                        name: "fk_leituras_do_documento_da_versao_documentos_da_versao_docume",
                        column: x => x.documento_id,
                        principalSchema: "autorizacao",
                        principalTable: "documentos_da_versao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "concessoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    beneficiario_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    beneficiario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    beneficiario_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    escopo_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origem_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concedida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concedida_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    concedida_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    concedida_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    valida_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revogada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revogada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_revogacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revogada_pela_janela_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concessoes", x => x.id);
                    table.UniqueConstraint("ak_concessao_perfil_id_id", x => new { x.perfil_id, x.id });
                    table.CheckConstraint("ck_concessoes_escopo_unidade", "(escopo_tipo = 'unidade') = (unidade_id IS NOT NULL)");
                    table.CheckConstraint("ck_concessoes_revogacao_completa", "(revogada_em IS NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NULL AND revogada_pela_janela_id IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NOT NULL AND revogada_por_sub IS NOT NULL AND motivo_revogacao IS NOT NULL AND revogada_pela_janela_id IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NULL AND revogada_por_sub IS NULL AND motivo_revogacao IS NOT NULL AND revogada_pela_janela_id IS NOT NULL)");
                    table.CheckConstraint("ck_concessoes_validade_posterior", "valida_ate IS NULL OR valida_ate > concedida_em");
                    table.ForeignKey(
                        name: "fk_concessoes_janela_de_migracao_revogada_pela_janela_id_perfi",
                        columns: x => new { x.revogada_pela_janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_concessoes_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_concessoes_versao_do_perfil_perfil_id_versao_id",
                        columns: x => new { x.perfil_id, x.versao_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lotes_da_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    janela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enviado_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enviado_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enviado_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    enviado_por_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    confirmou_exclusao = table.Column<bool>(type: "boolean", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes_da_migracao", x => x.id);
                    table.UniqueConstraint("ak_lote_da_migracao_janela_id_id_etapa", x => new { x.janela_id, x.id, x.etapa });
                    table.CheckConstraint("ck_lotes_da_migracao_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.ForeignKey(
                        name: "fk_lotes_da_migracao_janelas_de_migracao_janela_id_perfil_id",
                        columns: x => new { x.janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "areas_peso_area_enem_congeladas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) da área congelada."),
                    grupo_peso_area_enem_congelado_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id do grupo congelado dono da área (FK, cascade delete)."),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código da área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área."),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Rótulo oficial da área do ENEM, copiado junto do código."),
                    peso = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false, comment: "Peso da área na média do grupo, copiado da resolução de Pesos por Área."),
                    corte = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true, comment: "Nota mínima da área (0 a 1000), copiada da resolução de Pesos por Área; nulo quando a área não tem corte."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_areas_peso_area_enem_congeladas", x => x.id);
                    table.CheckConstraint("ck_areas_peso_area_enem_congeladas_corte", "corte IS NULL OR (corte >= 0 AND corte <= 1000)");
                    table.CheckConstraint("ck_areas_peso_area_enem_congeladas_peso", "peso >= 0");
                    table.ForeignKey(
                        name: "fk_areas_peso_area_enem_congeladas_grupo_peso_area_enem_congel",
                        column: x => x.grupo_peso_area_enem_congelado_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_peso_area_enem_congelados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peso e corte de cada área do ENEM num grupo do quadro de pesos por área congelado na classificação, copiados por valor da resolução de Pesos por Área declarada.");

            migrationBuilder.CreateTable(
                name: "condicoes_regra_derivacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_derivacao_configurada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula = table.Column<int>(type: "integer", nullable: false),
                    fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_regra_derivacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_regra_derivacao_regra_derivacao_configurada_regra",
                        column: x => x.regra_derivacao_configurada_id,
                        principalSchema: "selecao",
                        principalTable: "regras_derivacao_configuradas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "anexos_do_ato_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_do_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_a_publicar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_de_exibicao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos_do_ato_do_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_do_processo_ato_do_processo_ato_do_processo_id",
                        column: x => x.ato_do_processo_id,
                        principalSchema: "selecao",
                        principalTable: "atos_do_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_do_processo_documento_a_publicar_documento_a_",
                        column: x => x.documento_a_publicar_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_a_publicar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categorias_julgadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banca_requerida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_documento_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias_julgadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_categorias_julgadas_bancas_requeridas_banca_requerida_id",
                        column: x => x.banca_requerida_id,
                        principalSchema: "selecao",
                        principalTable: "bancas_requeridas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condicoes_gatilho",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula = table.Column<int>(type: "integer", nullable: false),
                    fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_gatilho", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_gatilho_documento_exigido_documento_exigido_id",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos_exigidos_base_legal",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_exigidos_base_legal", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_base_legal_documento_exigido_documento_",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nos_exigencia",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    no_pai_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantidade_minima = table.Column<int>(type: "integer", nullable: true),
                    consequencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    chave_distincao = table.Column<int>(type: "integer", nullable: true),
                    data_referencia = table.Column<DateOnly>(type: "date", nullable: true),
                    ocorrencias_esperadas = table.Column<string>(type: "jsonb", nullable: true),
                    repete_por_entidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nos_exigencia", x => x.id);
                    table.CheckConstraint("ck_nos_exigencia_chave_distincao_coerente", "(chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (chave_distincao IS NOT NULL AND chave_distincao IN (1, 2) AND data_referencia IS NOT NULL AND ocorrencias_esperadas IS NULL) OR (chave_distincao IS NOT NULL AND chave_distincao = 3 AND data_referencia IS NULL AND (ocorrencias_esperadas IS NULL OR jsonb_array_length(ocorrencias_esperadas) > 0))");
                    table.CheckConstraint("ck_nos_exigencia_id_diferente_de_no_pai_id", "id <> no_pai_id");
                    table.CheckConstraint("ck_nos_exigencia_ordem_nao_negativa", "ordem >= 0");
                    table.CheckConstraint("ck_nos_exigencia_quantidade_minima_positiva", "quantidade_minima IS NULL OR quantidade_minima >= 1");
                    table.CheckConstraint("ck_nos_exigencia_tipo_campos_coerentes", "(tipo = 1 AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NOT NULL AND consequencia IS NULL) OR (tipo = 2 AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL AND chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (tipo = 3 AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL AND chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL)");
                    table.ForeignKey(
                        name: "fk_nos_exigencia_documentos_exigidos_documento_exigido_id",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nos_exigencia_nos_exigencia_no_pai_id",
                        column: x => x.no_pai_id,
                        principalSchema: "selecao",
                        principalTable: "nos_exigencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_nos_exigencia_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condicoes_precondicao_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_coletado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula = table.Column<int>(type: "integer", nullable: false),
                    fato = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_precondicao_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_precondicao_fato_fato_coletado_fato_coletado_id",
                        column: x => x.fato_coletado_id,
                        principalSchema: "selecao",
                        principalTable: "fatos_coletados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "avaliacoes_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa do lote, repetida para ler a avaliação sem o lote; a chave estrangeira composta com o lote impede que divirja."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A inscrição avaliada, guardada por valor: quem não tem linha no arquivo do lote também tem avaliação, sem nota."),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A oferta de curso concorrida (origem da oferta no edital divulgado), guardada por valor e sem chave estrangeira: pertence à configuração do processo."),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "O código do grupo de área do ENEM cujos pesos apuraram a nota."),
                    situacao = table.Column<short>(type: "smallint", nullable: false, comment: "1 = apurada, 2 = inscrição sem linha no arquivo, 3 = inscrição com linhas em conflito. Só a apurada tem nota; a falta de nota nunca vira zero."),
                    nota = table.Column<decimal>(type: "numeric", nullable: true, comment: "Σ(nota × peso) ÷ Σ(pesos) das cinco áreas, sem arredondar. Nulo quando não há nota. Não vai para log.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avaliacoes_da_etapa", x => x.id);
                    table.CheckConstraint("ck_avaliacoes_da_etapa_nota", "(situacao = 1) = (nota IS NOT NULL) AND (nota IS NULL OR nota BETWEEN 0 AND 1000)");
                    table.CheckConstraint("ck_avaliacoes_da_etapa_situacao", "situacao IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_avaliacoes_da_etapa_lotes_de_notas_do_enem_lote_id_etapa_pr",
                        columns: x => new { x.lote_id, x.etapa_processo_id },
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumns: new[] { "id", "etapa_processo_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "A nota da etapa de uma inscrição numa oferta de curso concorrida (1ª ou 2ª opção), apurada pelos pesos do grupo de área do curso quando o lote foi importado. Append-only; vale com o lote. A nota por área está nos lançamentos do mesmo lote.");

            migrationBuilder.CreateTable(
                name: "inscritos_do_lote_de_notas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_de_inscricao_no_enem = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, comment: "O número de inscrição do INEP, guardado para a conferência de um recurso.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscritos_do_lote_de_notas", x => x.id);
                    table.UniqueConstraint("ak_inscritos_do_lote_de_notas_lote_inscricao", x => new { x.lote_id, x.inscricao_id });
                    table.CheckConstraint("ck_inscritos_do_lote_de_notas_numero_enem", "numero_de_inscricao_no_enem ~ '^[0-9]{12}$'");
                    table.ForeignKey(
                        name: "fk_inscritos_do_lote_de_notas_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inscritos_do_lote_de_notas_lotes_de_notas_do_enem_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "A inscrição com linha válida em um lote de notas, com o número de inscrição que o INEP lhe deu. Append-only.");

            migrationBuilder.CreateTable(
                name: "ocorrencias_do_lote_de_notas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false, comment: "1 = inscrito sem linha, 2 = linha sem inscrito, 3 = conflito."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numeros_das_linhas = table.Column<int[]>(type: "integer[]", nullable: true, comment: "Os números das linhas do arquivo, a partir de 1."),
                    cpf_mascarado = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true, comment: "O CPF mascarado da linha sem inscrito (***.999.999-**); nunca o CPF puro."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "O nome que o arquivo traz para a linha sem inscrito."),
                    sexo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true, comment: "O sexo (F ou M) que o arquivo traz para a linha sem inscrito.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocorrencias_do_lote_de_notas", x => x.id);
                    table.CheckConstraint("ck_ocorrencias_do_lote_de_notas_forma", "(tipo = 1 AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NULL) OR (tipo = 2 AND inscricao_id IS NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) = 1) OR (tipo = 3 AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) >= 2)");
                    table.ForeignKey(
                        name: "fk_ocorrencias_do_lote_de_notas_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ocorrencias_do_lote_de_notas_lotes_de_notas_do_enem_lote_id",
                        column: x => x.lote_id,
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "O que não virou nota ao importar um lote: inscrito sem linha, linha de CPF sem inscrito e inscrito em conflito. Nunca guarda o CPF.");

            migrationBuilder.CreateTable(
                name: "bases_da_permissao_na_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    classificacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    de_unidade = table.Column<bool>(type: "boolean", nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bases_da_permissao_na_concessao", x => x.id);
                    table.CheckConstraint("ck_bases_da_permissao_origem", "origem IN ('migracao', 'saida', 'liberacao', 'conciliacao')");
                    table.CheckConstraint("ck_bases_da_permissao_saida", "(origem = 'saida') = (classificacao IS NULL AND de_unidade IS NULL)");
                    table.ForeignKey(
                        name: "fk_bases_da_permissao_na_concessao_concessao_perfil_id_concess",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "liberacoes_da_reclassificacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reclassificacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_na_trilha = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_liberacoes_da_reclassificacao", x => x.id);
                    table.CheckConstraint("ck_liberacoes_da_reclassificacao_etapa", "etapa IN ('gestao_de_acessos', 'protecao_de_dados')");
                    table.CheckConstraint("ck_liberacoes_da_reclassificacao_motivo", "(resultado = 'mantida') = (motivo IS NOT NULL) AND resultado IN ('liberada', 'mantida')");
                    table.ForeignKey(
                        name: "fk_liberacoes_da_reclassificacao_concessoes_perfil_id_concessa",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_liberacoes_da_reclassificacao_reclassificacao_da_permissao_",
                        column: x => x.reclassificacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "reclassificacoes_da_permissao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "migracoes_da_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    janela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    exige_protecao_de_dados = table.Column<bool>(type: "boolean", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_migracoes_da_concessao", x => x.id);
                    table.CheckConstraint("ck_migracoes_da_concessao_situacao", "situacao IN ('retida', 'aguardando_protecao_de_dados', 'migrada', 'recusada', 'nao_migrada', 'fora_da_lista')");
                    table.ForeignKey(
                        name: "fk_migracoes_da_concessao_concessoes_perfil_id_concessao_id",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_migracoes_da_concessao_janelas_de_migracao_janela_id_perfil",
                        columns: x => new { x.janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "providencias",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fundamento = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    solicitada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitada_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    solicitada_por_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    desfecho_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    desfecho_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    desfecho_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    desfecho_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_do_desfecho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_providencias", x => x.id);
                    table.CheckConstraint("ck_providencias_desfecho_completo", "(estado = 'aberta' AND desfecho_em IS NULL AND desfecho_por_emissor IS NULL AND desfecho_por_sub IS NULL AND desfecho_por_nome IS NULL AND motivo_do_desfecho IS NULL) OR (estado <> 'aberta' AND desfecho_em IS NOT NULL AND desfecho_por_emissor IS NOT NULL AND desfecho_por_sub IS NOT NULL AND motivo_do_desfecho IS NOT NULL)");
                    table.CheckConstraint("ck_providencias_estado", "estado IN ('aberta', 'respondida', 'atendida')");
                    table.CheckConstraint("ck_providencias_fundamento", "char_length(fundamento) BETWEEN 10 AND 2000");
                    table.CheckConstraint("ck_providencias_tipo", "tipo IN ('retirada', 'revisao')");
                    table.ForeignKey(
                        name: "fk_providencias_concessoes_concessao_id",
                        column: x => x.concessao_id,
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "revisao_itens",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decidido_como = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    decidido_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_por_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisao_itens", x => x.id);
                    table.CheckConstraint("ck_revisao_itens_decisao_completa", "(resultado IS NULL AND decidido_como IS NULL AND decidido_por_emissor IS NULL AND decidido_por_sub IS NULL AND decidido_em IS NULL) OR (resultado IS NOT NULL AND decidido_como IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_sub IS NOT NULL AND decidido_em IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_revisao_itens_concessoes_concessao_id",
                        column: x => x.concessao_id,
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revisao_itens_revisoes_de_acessos_revisao_id",
                        column: x => x.revisao_id,
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_de_acessos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_da_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    janela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_de_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lote_de_origem_etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    envio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    envio_etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ator = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: true),
                    versao_avaliada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    composicao_avaliada = table.Column<string[]>(type: "text[]", nullable: false),
                    acrescimo_avaliado = table.Column<string[]>(type: "text[]", nullable: false),
                    prazo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_migracao", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_migracao_envio", "(envio_id IS NULL) = (envio_etapa IS NULL) AND (envio_etapa IS NULL OR envio_etapa IN ('gestao_de_acessos', 'protecao_de_dados'))");
                    table.CheckConstraint("ck_decisoes_da_migracao_lote_de_origem", "(lote_de_origem_id IS NULL) = (lote_de_origem_etapa IS NULL) AND (lote_de_origem_etapa IS NULL OR lote_de_origem_etapa = 'gestao_de_acessos')");
                    table.CheckConstraint("ck_decisoes_da_migracao_ramo", "(ator = 'humano' AND etapa IN ('gestao_de_acessos', 'protecao_de_dados') AND decisao IN ('migrar', 'recusar') AND decisor_emissor IS NOT NULL AND decisor_sub IS NOT NULL AND decisor_tem_concessao IS NOT NULL AND lote_de_origem_id IS NOT NULL AND (envio_etapa IS NULL OR envio_etapa = etapa) AND (etapa <> 'protecao_de_dados' OR envio_id IS NOT NULL) AND (etapa <> 'gestao_de_acessos' OR envio_id IS NULL OR envio_id = lote_de_origem_id)) OR (ator = 'sistema' AND decisor_emissor IS NULL AND decisor_sub IS NULL AND decisor_nome IS NULL AND decisor_tem_concessao IS NULL AND envio_id IS NULL AND ((etapa = 'gestao_de_acessos' AND decisao = 'reter' AND lote_de_origem_id IS NOT NULL) OR (etapa = 'sistema' AND decisao IN ('nao_migrada', 'fora_da_lista'))))");
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_concessoes_perfil_id_concessao_id",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_janelas_de_migracao_janela_id_perfil_id",
                        columns: x => new { x.janela_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "janelas_de_migracao",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_lote_da_migracao_janela_id_envio_id_en",
                        columns: x => new { x.janela_id, x.envio_id, x.envio_etapa },
                        principalSchema: "autorizacao",
                        principalTable: "lotes_da_migracao",
                        principalColumns: new[] { "janela_id", "id", "etapa" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_lote_da_migracao_janela_id_lote_de_ori",
                        columns: x => new { x.janela_id, x.lote_de_origem_id, x.lote_de_origem_etapa },
                        principalSchema: "autorizacao",
                        principalTable: "lotes_da_migracao",
                        principalColumns: new[] { "janela_id", "id", "etapa" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_versao_do_perfil_perfil_id_versao_aval",
                        columns: x => new { x.perfil_id, x.versao_avaliada_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nos_exigencia_base_legal",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    no_exigencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nos_exigencia_base_legal", x => x.id);
                    table.ForeignKey(
                        name: "fk_nos_exigencia_base_legal_no_exigencia_no_exigencia_id",
                        column: x => x.no_exigencia_id,
                        principalSchema: "selecao",
                        principalTable: "nos_exigencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lancamentos_de_nota",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa do lote, repetida para ler o lançamento sem o lote; a chave estrangeira composta com o lote impede que divirja."),
                    item_codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "A área do ENEM. Só o domínio enumera as áreas: o banco não carrega o vocabulário, para que acrescentar um item não exija migration."),
                    nota = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false),
                    situacao = table.Column<short>(type: "smallint", nullable: false, comment: "1 = presente, 2 = ausente. Quem faltou tem nota zero, que não vale como nota obtida.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lancamentos_de_nota", x => x.id);
                    table.CheckConstraint("ck_lancamentos_de_nota_nota", "nota BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_lancamentos_de_nota_situacao", "situacao IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_lancamentos_de_nota_inscritos_do_lote_de_notas_lote_id_insc",
                        columns: x => new { x.lote_id, x.inscricao_id },
                        principalSchema: "selecao",
                        principalTable: "inscritos_do_lote_de_notas",
                        principalColumns: new[] { "lote_id", "inscricao_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lancamentos_de_nota_lotes_de_notas_do_enem_lote_id_etapa_pr",
                        columns: x => new { x.lote_id, x.etapa_processo_id },
                        principalSchema: "selecao",
                        principalTable: "lotes_de_notas_do_enem",
                        principalColumns: new[] { "id", "etapa_processo_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Uma nota individual de uma inscrição numa etapa: uma área do ENEM, com a nota e a situação. Append-only; corrigir é importar outro lote.");

            migrationBuilder.CreateTable(
                name: "aceites_do_termo",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_do_termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    resumo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    pessoa_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pessoa_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pessoa_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aceito_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceites_do_termo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedido_anexo_leituras",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anexo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leitor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido_anexo_leituras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedido_anexos",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tamanho_declarado = table.Column<long>(type: "bigint", nullable: false),
                    etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    enviado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recusado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido_anexos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedido_decisoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    permissoes_avaliadas = table.Column<string[]>(type: "text[]", nullable: false),
                    sensibilidade_avaliada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dispensa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedido_decisoes", x => x.id);
                    table.CheckConstraint("ck_pedido_decisoes_motivo_na_recusa", "resultado <> 'recusado' OR motivo IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "pedidos_de_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitante_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitante_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    solicitante_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    beneficiario_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    beneficiario_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_aceitou_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valida_ate_sugerida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valida_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anexara_documento = table.Column<bool>(type: "boolean", nullable: false),
                    termo_versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termo_numero = table.Column<int>(type: "integer", nullable: true),
                    termo_resumo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_de_concessao", x => x.id);
                    table.CheckConstraint("ck_pedidos_beneficiario_completo", "(beneficiario_emissor IS NULL) = (beneficiario_sub IS NULL)");
                    table.CheckConstraint("ck_pedidos_concessao_so_aprovado", "concessao_id IS NULL OR situacao = 'aprovado'");
                    table.CheckConstraint("ck_pedidos_decisao_com_beneficiario", "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'cancelado', 'devolvido') OR beneficiario_sub IS NOT NULL");
                    table.CheckConstraint("ck_pedidos_encerramento", "(situacao IN ('aprovado', 'recusado', 'cancelado', 'devolvido')) = (encerrado_em IS NOT NULL)");
                    table.CheckConstraint("ck_pedidos_termo_completo", "(termo_versao_id IS NULL) = (termo_numero IS NULL) AND (termo_versao_id IS NULL) = (termo_resumo IS NULL)");
                    table.ForeignKey(
                        name: "fk_pedidos_de_concessao_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidos_de_concessao_versao_do_perfil_perfil_id_versao_id",
                        columns: x => new { x.perfil_id, x.versao_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trocas_de_versao_do_pedido",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_anterior = table.Column<int>(type: "integer", nullable: false),
                    versao_nova = table.Column<int>(type: "integer", nullable: false),
                    desfecho = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trocas_de_versao_do_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_trocas_de_versao_do_pedido_pedidos_de_concessao_pedido_id",
                        column: x => x.pedido_id,
                        principalSchema: "autorizacao",
                        principalTable: "pedidos_de_concessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "termos_de_responsabilidade",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_vigente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultimo_numero = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_de_responsabilidade", x => x.id);
                    table.CheckConstraint("ck_termos_de_responsabilidade_vigente", "(versao_vigente_id IS NULL) = (ultimo_numero = 0)");
                });

            migrationBuilder.CreateTable(
                name: "versoes_do_termo",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    resumo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ato = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    publicada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_do_termo", x => x.id);
                    table.CheckConstraint("ck_versoes_do_termo_numero", "numero >= 1");
                    table.CheckConstraint("ck_versoes_do_termo_resumo", "resumo ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_versoes_do_termo_texto", "char_length(texto) BETWEEN 200 AND 20000");
                    table.ForeignKey(
                        name: "fk_versoes_do_termo_termos_de_responsabilidade_termo_id",
                        column: x => x.termo_id,
                        principalSchema: "autorizacao",
                        principalTable: "termos_de_responsabilidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "termos_na_instalacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    versao_do_termo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero = table.Column<int>(type: "integer", nullable: true),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_na_instalacao", x => x.id);
                    table.CheckConstraint("ck_termos_na_instalacao_versao", "(situacao = 'sem_termo_publicado') = (versao_do_termo_id IS NULL) AND (versao_do_termo_id IS NULL) = (numero IS NULL)");
                    table.ForeignKey(
                        name: "fk_termos_na_instalacao_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_termos_na_instalacao_versao_do_termo_versao_do_termo_id",
                        column: x => x.versao_do_termo_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_termo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "categoria_documento",
                columns: new[] { "id", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "ordem", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("ca7e0000-0000-7000-8000-000000000001"), "IDENTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Identificação", 1, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000002"), "ESCOLARIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Escolaridade", 2, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000003"), "TITULACAO_EXPERIENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Titulação e experiência", 3, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000004"), "RENDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Renda", 4, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000005"), "RESIDENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Residência", 5, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000006"), "RACA_ETNIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Raça/etnia", 6, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000007"), "SAUDE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Saúde", 7, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000008"), "DOCUMENTO_PROCESSUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Documento processual", 8, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000009"), "PRODUCAO_AVALIATIVA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Produção avaliativa", 9, null, null },
                    { new Guid("ca7e0000-0000-7000-8000-000000000010"), "OUTROS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, false, "Outros", 10, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "coleta_inscricao", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000001"), null, "INSCRICAO", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Período em que o candidato se inscreve no processo seletivo.", false, "Inscrição", "NO_EDITAL", null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "coleta_solicitacao_isencao", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000002"), "Lei nº 12.799/2013", "SOLICITACAO_ISENCAO", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Janela em que o candidato pede isenção da taxa de inscrição. Abre junto com as inscrições e termina antes delas.", false, "Solicitação de isenção", "NO_EDITAL", null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "permite_complementacao", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000003"), null, "HOMOLOGACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Conferência das inscrições recebidas e publicação de quais foram homologadas.", false, "Homologação das inscrições", "NO_EDITAL", true, null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000004"), null, "ENSALAMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Distribuição dos candidatos pelos locais de prova.", false, "Ensalamento", "NO_EDITAL", null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "agrupa_etapas", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000005"), true, null, "AVALIACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Fase que agrupa as etapas pontuadas do certame.", false, "Avaliação", "NO_EDITAL", null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("f45e0000-0000-7000-8000-000000000006"), null, "CLASSIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Apuração das notas e ordenação dos candidatos por modalidade.", false, "Classificação", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000007"), null, "RESULTADO_PRELIMINAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Publicação do resultado que ainda admite recurso.", false, "Resultado preliminar", "NO_EDITAL", null, null, null, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "permite_complementacao", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000008"), null, "RECURSOS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Interposição e análise dos recursos contra o resultado preliminar.", false, "Recursos", "NO_EDITAL", true, null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fase_canonica",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("f45e0000-0000-7000-8000-000000000009"), null, "RESULTADO_FINAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Publicação do resultado depois de julgados os recursos.", false, "Resultado final", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000010"), "Lei nº 12.711/2012; Portaria Normativa MEC nº 4/2018", "HETEROIDENTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Procedimento de heteroidentificação étnico-racial dos candidatos que concorrem por cota.", false, "Heteroidentificação", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000011"), null, "HABILITACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Comprovação documental dos requisitos declarados pelo candidato.", false, "Habilitação", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000012"), null, "HOMOLOGACAO_RESULTADO_FINAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Ato do conselho que homologa o resultado final do processo seletivo.", false, "Homologação do resultado final", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000013"), null, "MATRICULA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Efetivação do vínculo do candidato aprovado com a instituição.", false, "Matrícula", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000014"), null, "LISTA_ESPERA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Fila de candidatos classificados além das vagas, convocados conforme as vagas são liberadas.", false, "Lista de espera", "NA_EXECUCAO", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000015"), null, "CHAMADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Convocação dos candidatos da lista de espera para ocupar vagas remanescentes.", false, "Chamada", "NA_EXECUCAO", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000016"), "Lei nº 13.146/2015, art. 2º §1º e art. 30; Lei nº 12.711/2012 c/c Lei nº 13.409/2016", "AVALIACAO_BIOPSICOSSOCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Avaliação multiprofissional e interdisciplinar que verifica se o candidato com deficiência atende aos requisitos legais para concorrer às vagas reservadas às pessoas com deficiência.", false, "Avaliação biopsicossocial", "NO_EDITAL", null, null, null, null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "modalidade",
                columns: new[] { "id", "acao_quando_indeferido", "base_legal", "codigo", "composicao_origem", "composicao_vagas", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "natureza_legal", "regra_de_aplicabilidade", "regra_remanejamento", "remanejamento_args", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("70da1000-0000-7000-8000-000000000001"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "AC", null, "RESIDUAL_DO_VO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Ampla concorrência", false, "AMPLA", "[]", null, "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000002"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_PPI", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, preto/pardo/indígena", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"EM\",\"valor\":[\"PRETA\",\"PARDA\"]},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}],[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"IGUAL\",\"valor\":\"INDIGENA\"},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000003"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_Q", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, quilombola", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_Q\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000004"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_PCD", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, pessoa com deficiência", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000005"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_EP", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, egresso de escola pública", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000006"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_PPI", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, preto/pardo/indígena", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"EM\",\"valor\":[\"PRETA\",\"PARDA\"]},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true}],[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"IGUAL\",\"valor\":\"INDIGENA\"},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000007"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_Q", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, quilombola", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_Q\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000008"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_PCD", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, pessoa com deficiência", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000009"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_EP", null, "DENTRO_DO_VR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, egresso de escola pública", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_CASCATA", "{\"destino\":null,\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000010"), null, "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)", "AC_PCD", "AC", "RETIRA_DE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Ampla Concorrência – Pessoa com Deficiência (V)", false, "ACAO_AFIRMATIVA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true}]]", "DESTINO_UNICO", "{\"destino\":\"AC\",\"par\":null,\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000012"), null, "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "AC_I", null, "SUPLEMENTAR_AO_TOTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Vaga por acréscimo — candidato indígena (PSIQ)", false, "ACAO_AFIRMATIVA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_VAGA_POR_ACRESCIMO\",\"operador\":\"IGUAL\",\"valor\":true}]]", "CRUZADO", "{\"destino\":null,\"par\":\"AC_Q\",\"fallback\":null}", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000013"), null, "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "AC_Q", null, "SUPLEMENTAR_AO_TOTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Vaga por acréscimo — candidato quilombola (PSIQ)", false, "ACAO_AFIRMATIVA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_VAGA_POR_ACRESCIMO\",\"operador\":\"IGUAL\",\"valor\":true}]]", "CRUZADO", "{\"destino\":null,\"par\":\"AC_I\",\"fallback\":null}", null, null }
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfis",
                columns: new[] { "id", "ativo", "created_at", "descricao", "governanca", "nome", "updated_at" },
                values: new object[,]
                {
                    { new Guid("019a0000-0000-7000-8000-000000000001"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Opera as concessões de acesso, mantém os perfis de acesso e publica o termo de responsabilidade.", "gestor-de-acessos", "Gestor de acessos", null },
                    { new Guid("019a0000-0000-7000-8000-000000000002"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Decide, em nome da Unifesspa, a segunda etapa dos pedidos com dado pessoal, as versões protegidas de perfil, a janela de migração e a proteção do catálogo, e conduz a revisão periódica dos acessos.", "representante-do-controlador", "Representante do controlador", null },
                    { new Guid("019a0000-0000-7000-8000-000000000003"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Acompanha o tratamento de dados pessoais na gestão de acessos: consulta pedidos, concessões, revisões, versões de perfil, janela de migração e catálogo, registra manifestação técnica na proteção do catálogo e solicita providências sobre concessões, sem aprovar, recusar nem revogar.", "encarregado-de-dados", "Encarregado de dados", null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "precedencia_fase",
                columns: new[] { "id", "antecessora_codigo", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "sucessora_codigo", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("93ec0000-0000-7000-8000-000000000001"), "INSCRICAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HOMOLOGACAO", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000002"), "RESULTADO_PRELIMINAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "RECURSOS", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000003"), "RECURSOS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "RESULTADO_FINAL", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000004"), "RESULTADO_FINAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HABILITACAO", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000005"), "HABILITACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "MATRICULA", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000006"), "HETEROIDENTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HOMOLOGACAO_RESULTADO_FINAL", null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                columns: new[] { "id", "ativo", "binding", "cardinalidade", "classificacao_protecao", "codigo", "componente_exibicao", "created_at", "created_by", "dependencias", "descricao", "dominio", "escopo", "finalidade_tratamento", "fonte_valores", "formato", "hipotese_legal", "nome", "origem", "ponto_resolucao", "regras_padrao", "sistema", "updated_at", "updated_by", "protecao_aceita_em", "protecao_aceita_por", "protecao_ciclo", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_justificativa", "protecao_manifestacao_desfavoravel", "protecao_manifestacoes_no_ciclo", "protecao_manifestada_em", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000001"), true, "CAMPO_FORMULARIO:COR_RACA", "ESCALAR", "SENSIVEL", "COR_RACA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Cor ou raça que o candidato declara. É o dado de que depende a concorrência às vagas reservadas a pretos, pardos e indígenas.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Cor ou raça", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000002"), true, "CAMPO_FORMULARIO:QUILOMBOLA", "ESCALAR", "SENSIVEL", "QUILOMBOLA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato se declara quilombola. É o dado de que depende a concorrência às vagas reservadas a quilombolas.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Quilombola", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000003"), true, "CAMPO_FORMULARIO:PCD", "ESCALAR", "SENSIVEL", "PCD", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato declara ser pessoa com deficiência. É o dado de que depende a concorrência às vagas reservadas a pessoas com deficiência.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Pessoa com deficiência", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000004"), true, "REGRA_DERIVACAO:EGRESSO_ESCOLA_PUBLICA", "ESCALAR", "PESSOAL", "EGRESSO_ESCOLA_PUBLICA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato cursou o ensino médio em escola pública. O sistema o calcula a partir das duas respostas sobre a origem escolar, pela regra da Lei nº 12.711/2012.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Egresso de escola pública", "DERIVADO", "INSCRICAO", "[{\"contribui\":null,\"quando\":[[{\"fato\":\"ONDE_CURSOU_EM\",\"operador\":\"EM\",\"valor\":[\"SOMENTE_PUBLICA\",\"COMUNITARIA_CAMPO\",\"PUBLICA_E_COMUNITARIA\"]}],[{\"fato\":\"ONDE_CURSOU_EM\",\"operador\":\"IGUAL\",\"valor\":\"CERTIFICACAO\"},{\"fato\":\"FORMA_CONCLUSAO_EM\",\"operador\":\"EM\",\"valor\":[\"ENCCEJA\",\"PROFICIENCIA\",\"ENEM\"]}]]}]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000005"), true, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", "ESCALAR", "PESSOAL", "RENDA_PER_CAPITA", "MOEDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Renda mensal da família do candidato dividida pelo número de integrantes da composição familiar, em reais. Define o enquadramento nas vagas reservadas por renda.", "NUMERICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita", "DERIVADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000006"), true, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", "ESCALAR", "PESSOAL", "FAIXA_ETARIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new[] { "DATA_NASCIMENTO" }, "Idade em anos completos do candidato na data de referência dos fatos do processo, calculada pelo sistema a partir da data de nascimento, para verificar os requisitos de participação.", "NUMERICO", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Idade", "DERIVADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000007"), true, "CAMPO_FORMULARIO:SEXO", "ESCALAR", "PESSOAL", "SEXO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Sexo biológico do candidato, conforme ele declara na inscrição.", "CATEGORICO", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sexo biológico", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000008"), true, "REGRA_DERIVACAO:MODALIDADE", "MULTIVALORADO", "INTERNO", "MODALIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Modalidades de concorrência em que o candidato concorre, resultantes das regras de reserva de vagas do processo aplicadas às respostas dele.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "MODALIDADE", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Modalidade de concorrência", "DERIVADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000009"), true, "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO", "MULTIVALORADO", "SENSIVEL", "CONDICAO_ATENDIMENTO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Condições do candidato pelas quais ele pede atendimento especializado nas etapas do processo.", "CATEGORICO", "CANDIDATO", "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.", "PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Condição de atendimento especializado", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000010"), true, "CAMPO_FORMULARIO:NACIONALIDADE", "ESCALAR", "PESSOAL", "NACIONALIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nacionalidade do candidato, que define os documentos de identificação que ele informa.", "CATEGORICO", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nacionalidade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000011"), true, "CAMPO_FORMULARIO:TIPO_DEFICIENCIA", "ESCALAR", "SENSIVEL", "TIPO_DEFICIENCIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Tipo de deficiência do candidato com deficiência, entre os do cadastro de tipos de deficiência.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "TIPOS_DEFICIENCIA", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Tipo de deficiência", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000012"), true, "CAMPO_FORMULARIO:BAIXA_RENDA", "ESCALAR", "PESSOAL", "BAIXA_RENDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Autodeclaração de que a renda familiar per capita é igual ou inferior a um salário mínimo, feita na inscrição para as vagas reservadas por renda.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita igual ou inferior a um salário mínimo", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000013"), true, "CAMPO_FORMULARIO:CONCORRER_PCD", "ESCALAR", "INTERNO", "CONCORRER_PCD", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a pessoas com deficiência.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a pessoas com deficiência", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000014"), true, "CAMPO_FORMULARIO:CONCORRER_EP", "ESCALAR", "INTERNO", "CONCORRER_EP", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a egressos de escola pública.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a egressos de escola pública", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000015"), true, "CAMPO_FORMULARIO:CONCORRER_PPI", "ESCALAR", "INTERNO", "CONCORRER_PPI", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a pretos, pardos e indígenas.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a pretos, pardos e indígenas", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000016"), true, "CAMPO_FORMULARIO:CONCORRER_Q", "ESCALAR", "INTERNO", "CONCORRER_Q", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a quilombolas.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a quilombolas", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000017"), true, "CAMPO_FORMULARIO:CONCORRER_RENDA", "ESCALAR", "INTERNO", "CONCORRER_RENDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas por renda familiar per capita.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas por renda familiar per capita", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000018"), true, "CAMPO_FORMULARIO:ENDERECO_RESIDENCIAL", "ESCALAR", "PESSOAL", "ENDERECO_RESIDENCIAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Endereço em que o candidato reside, usado para verificar os requisitos regionais do processo.", "ENDERECO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Endereço residencial", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000019"), true, "CAMPO_FORMULARIO:DATA_NASCIMENTO", "ESCALAR", "PESSOAL", "DATA_NASCIMENTO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Data de nascimento do candidato, usada para verificar a idade e os requisitos de participação.", "DATA", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Data de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000020"), true, "ATRIBUTO_CANDIDATO:UF_RESIDENCIA", "ESCALAR", "PESSOAL", "UF_RESIDENCIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new[] { "ENDERECO_RESIDENCIAL" }, "Unidade da Federação em que o candidato reside, derivada do endereço residencial.", "CATEGORICO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "GEO_UF", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de residência", "DERIVADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000021"), true, "ATRIBUTO_CANDIDATO:MUNICIPIO_RESIDENCIA", "ESCALAR", "PESSOAL", "MUNICIPIO_RESIDENCIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new[] { "ENDERECO_RESIDENCIAL" }, "Município em que o candidato reside, derivado do endereço residencial.", "CATEGORICO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "GEO_MUNICIPIO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Município de residência", "DERIVADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000022"), true, "CLASSIFICACAO:MODALIDADE_CONVOCACAO", "ESCALAR", "INTERNO", "MODALIDADE_CONVOCACAO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Grupo de vagas em que o candidato foi convocado, conhecido só depois do resultado final. Os documentos da habilitação seguem a convocação.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "MODALIDADE", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Modalidade da convocação", "DERIVADO", "RESULTADO_FINAL", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000023"), true, "CAMPO_FORMULARIO:MAIOR_IDADE", "ESCALAR", "PESSOAL", "MAIOR_IDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o membro da composição familiar é maior de idade.", "BOOLEANO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Maior de idade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000024"), true, "CAMPO_FORMULARIO:SEM_RENDA", "ESCALAR", "PESSOAL", "SEM_RENDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o membro da composição familiar não tem renda.", "BOOLEANO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sem renda", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000025"), true, "CAMPO_FORMULARIO:SOB_GUARDA", "ESCALAR", "PESSOAL", "SOB_GUARDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o membro da composição familiar está sob guarda.", "BOOLEANO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sob guarda", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000026"), true, "CAMPO_FORMULARIO:PARENTESCO", "ESCALAR", "PESSOAL", "PARENTESCO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Parentesco do membro da composição familiar com o candidato.", "CATEGORICO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Parentesco", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000027"), true, "CAMPO_FORMULARIO:NOME", "ESCALAR", "PESSOAL", "NOME", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome civil do candidato, como vem do login gov.br.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000028"), true, "CAMPO_FORMULARIO:DESEJA_NOME_SOCIAL", "ESCALAR", "PESSOAL", "DESEJA_NOME_SOCIAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato deseja ser identificado pelo nome social no processo seletivo.", "BOOLEANO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja usar nome social", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000029"), true, "CAMPO_FORMULARIO:NOME_SOCIAL", "ESCALAR", "PESSOAL", "NOME_SOCIAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome pelo qual o candidato prefere ser identificado e que aparece no lugar do nome civil.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome social", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000030"), true, "CAMPO_FORMULARIO:CPF", "ESCALAR", "IDENTIFICADOR", "CPF", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do CPF do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "CPF", "CUMPRIMENTO_OBRIGACAO_LEGAL", "CPF", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000031"), true, "CAMPO_FORMULARIO:RG_NUMERO", "ESCALAR", "IDENTIFICADOR", "RG_NUMERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do RG do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000032"), true, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR", "ESCALAR", "PESSOAL", "RG_ORGAO_EMISSOR", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Órgão que emitiu o RG do candidato, entre os de uma lista fechada.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Órgão emissor do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000033"), true, "CAMPO_FORMULARIO:RG_DATA_EMISSAO", "ESCALAR", "PESSOAL", "RG_DATA_EMISSAO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Data em que o RG do candidato foi emitido.", "DATA", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Data de emissão do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000036"), true, "CAMPO_FORMULARIO:NOME_MAE", "ESCALAR", "PESSOAL", "NOME_MAE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome da mãe do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome da mãe", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000037"), true, "CAMPO_FORMULARIO:NOME_PAI", "ESCALAR", "PESSOAL", "NOME_PAI", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome do pai do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome do pai", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000038"), true, "CAMPO_FORMULARIO:ESTADO_CIVIL", "ESCALAR", "PESSOAL", "ESTADO_CIVIL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Estado civil do candidato.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Estado civil", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000039"), true, "CAMPO_FORMULARIO:EMAIL", "ESCALAR", "PESSOAL", "EMAIL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Endereço de e-mail para comunicação com o candidato.", "TEXTO", "CANDIDATO", "Comunicação com o candidato sobre o processo seletivo.", null, "EMAIL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "E-mail", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000040"), true, "CAMPO_FORMULARIO:TELEFONE", "ESCALAR", "PESSOAL", "TELEFONE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Telefone, com DDD, para comunicação com o candidato.", "TEXTO", "CANDIDATO", "Comunicação com o candidato sobre o processo seletivo.", null, "TELEFONE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Telefone", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000041"), true, "CAMPO_FORMULARIO:RG_UF", "ESCALAR", "PESSOAL", "RG_UF", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Unidade da Federação que emitiu o RG do candidato.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GEO_UF", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de emissão do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000042"), true, "CAMPO_FORMULARIO:NATURALIDADE_UF", "ESCALAR", "PESSOAL", "NATURALIDADE_UF", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Unidade da Federação em que o candidato nasceu.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GEO_UF", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000043"), true, "CAMPO_FORMULARIO:NATURALIDADE_MUNICIPIO", "ESCALAR", "PESSOAL", "NATURALIDADE_MUNICIPIO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Município em que o candidato nasceu.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GEO_MUNICIPIO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Município de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000044"), true, "CAMPO_FORMULARIO:TIPO_ENDERECO", "ESCALAR", "PESSOAL", "TIPO_ENDERECO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Tipo de localidade em que o candidato reside, perguntado antes do endereço.", "CATEGORICO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Tipo de localidade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000045"), true, "CAMPO_FORMULARIO:NOME_COMUNIDADE", "ESCALAR", "PESSOAL", "NOME_COMUNIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome da aldeia, da comunidade tradicional ou do quilombo em que o candidato reside.", "TEXTO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome da aldeia, comunidade ou quilombo", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000046"), true, "CAMPO_FORMULARIO:FORMA_CONCLUSAO_EM", "ESCALAR", "PESSOAL", "FORMA_CONCLUSAO_EM", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Como o candidato concluiu o ensino médio: curso regular, Educação de Jovens e Adultos, ENCCEJA, exame de proficiência ou ENEM.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Forma de conclusão do ensino médio", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000047"), true, "CAMPO_FORMULARIO:ONDE_CURSOU_EM", "ESCALAR", "PESSOAL", "ONDE_CURSOU_EM", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Em que rede de ensino o candidato cursou o ensino médio, ou se o concluiu por certificação sem frequentá-lo.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Onde cursou o ensino médio", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000048"), true, "CAMPO_FORMULARIO:RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", "ESCALAR", "PESSOAL", "RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se a renda familiar per capita do candidato é igual ou inferior a um salário mínimo e meio, como ele declara no pedido de isenção da taxa.", "BOOLEANO", "CANDIDATO", "Análise do pedido de isenção da taxa de inscrição do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita de até um salário mínimo e meio", "DECLARADO", "SOLICITACAO_ISENCAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000049"), true, "CAMPO_FORMULARIO:EDICAO_ENEM", "ESCALAR", "PESSOAL", "EDICAO_ENEM", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Edição do ENEM cujas notas são importadas para a classificação do candidato.", "CATEGORICO", "CANDIDATO", "Importação das notas do ENEM da edição escolhida pelo candidato, na classificação do processo seletivo.", "PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Edição do ENEM", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000050"), true, "CAMPO_FORMULARIO:INDIGENA", "ESCALAR", "SENSIVEL", "INDIGENA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato se declara indígena. A condição exige declaração de pertencimento e passa só por análise de documentos.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Indígena", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000051"), true, "CAMPO_FORMULARIO:PASSAPORTE_NUMERO", "ESCALAR", "IDENTIFICADOR", "PASSAPORTE_NUMERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do passaporte do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do passaporte", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000052"), true, "CAMPO_FORMULARIO:RNM_NUMERO", "ESCALAR", "IDENTIFICADOR", "RNM_NUMERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do Registro Nacional Migratório (RNM) do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do Registro Nacional Migratório (RNM)", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000053"), true, "CAMPO_FORMULARIO:RECURSO_ACESSIBILIDADE", "MULTIVALORADO", "SENSIVEL", "RECURSO_ACESSIBILIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Recursos de acessibilidade de que o candidato precisa nas etapas, entre os que o processo oferece.", "CATEGORICO", "CANDIDATO", "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.", "PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Recurso de acessibilidade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000054"), true, "REGRA_DERIVACAO:OPTOU_PELAS_COTAS_DA_LEI", "ESCALAR", "PESSOAL", "OPTOU_PELAS_COTAS_DA_LEI", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato é egresso de escola pública e optou por concorrer às vagas de escola pública.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Optou pelas cotas da Lei nº 12.711/2012", "DERIVADO", "INSCRICAO", "[{\"contribui\":null,\"quando\":[[{\"fato\":\"EGRESSO_ESCOLA_PUBLICA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_EP\",\"operador\":\"IGUAL\",\"valor\":true}]]}]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000055"), true, "CAMPO_FORMULARIO:CONCORRER_VAGA_POR_ACRESCIMO", "ESCALAR", "INTERNO", "CONCORRER_VAGA_POR_ACRESCIMO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas por acréscimo para indígenas e quilombolas.", "BOOLEANO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas por acréscimo para indígenas e quilombolas", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000056"), true, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR_OUTRO", "ESCALAR", "PESSOAL", "RG_ORGAO_EMISSOR_OUTRO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "O órgão emissor do RG que não está na lista.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome do órgão emissor do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000057"), true, "CAMPO_FORMULARIO:SEXO_CIVIL", "ESCALAR", "PESSOAL", "SEXO_CIVIL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Sexo que consta do registro civil do candidato.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sexo civil", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000058"), true, "CAMPO_FORMULARIO:IDENTIDADE_GENERO", "ESCALAR", "SENSIVEL", "IDENTIDADE_GENERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Identidade de gênero que o candidato declara.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Identidade de gênero", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000059"), true, "CAMPO_FORMULARIO:ORIENTACAO_SEXUAL", "ESCALAR", "SENSIVEL", "ORIENTACAO_SEXUAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Orientação sexual que o candidato declara.", "CATEGORICO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "GLOBAL", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Orientação sexual", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000060"), true, "ATRIBUTO_CANDIDATO:MODALIDADE_SEGUNDA_OPCAO", "MULTIVALORADO", "INTERNO", "MODALIDADE_SEGUNDA_OPCAO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Modalidades de concorrência do candidato na segunda opção de curso, que tem distribuição de vagas própria.", "CATEGORICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "MODALIDADE", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Modalidade de concorrência da 2ª opção de curso", "DERIVADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null }
                });

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "esquema_args", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[,]
                {
                    { new Guid("d0a00000-0000-7000-8000-000000000001"), "Proposta CEPS + #56; média ponderada NOTA=Σ(nota×peso)/Σpeso", "FORMULA-MEDIA-PONDERADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fonte_pesos\":[\"etapa\",\"peso_area_enem\"]}", "a6fdc29bce533ef3cf8acb6cfdd7d67a99104541f6201d227f0d1c7051229ad6", "[\"divisor = Σ(peso das etapas classificatória∪ambas)\"]", "regra_calculo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000002"), "Portaria MEC 18/2012 art. 16 — SiSU federal (CEPS não calcula)", "CLASSIFICACAO-IMPORTADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "8e6f1f7b705c601991bf0617bbf881e39a3666c641abdfa4c1bd9ce2ecbcc85c", "[\"sem cálculo local; classificação federal por importação do listão\"]", "regra_calculo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000003"), "Decisão CEPS/PO (gaps 1.1) — truncamento 2 casas (default)", "PRECISAO-TRUNCAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"casas\":\"integer\"}", "66cbb932e61982f09fdd1d60a0db8411ea02dedc19bb72d1af23004f1e18360b", "[\"trunca na N-ésima casa, sem arredondar\"]", "regra_arredondamento", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000004"), "Reprodução de editais antigos (PSE/Convênios)", "PRECISAO-ARREDONDAR-CIMA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"casas\":\"integer\"}", "efc491bf6a242a870c7bf9969048c198db88116ba39e72a44bcf1d5d5aebc4fb", "[\"arredonda p/ cima se 3ª casa ≥ 5\"]", "regra_arredondamento", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000005"), "Edital por processo (nota mínima eliminatória)", "ELIM-NOTA-MINIMA-ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"etapa\":\"text\",\"nota_minima\":\"numeric\"}", "b64f643eba9744efc20bf19221bde85c645da32262a7d2ef616f18bd7c2ed5ac", "[\"nota < mínima na etapa → elimina\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000006"), "Res. 805/2024 art. 5º e Anexo I (ponto de corte por área do ENEM; Redação = 400)", "ELIM-CORTE-EM-AREA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"area_codigo\":\"text\",\"minimo\":\"numeric\"}", "2b664ed9b782ce9e4b47c70063fd2c2f5bc225c74301fcc801600956d856ad4a", "[\"nota na área < mínimo → elimina\",\"no máximo um corte por área\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000007"), "Res. 805/2024 art. 5º (zero em qualquer área elimina)", "ELIM-ZERO-EM-AREA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "75f4929b848138c8ed939a182e3664451ce38cd40a136adda438b62e1b6e3fb8", "[\"nota zero em qualquer área do ENEM → elimina\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000008"), "Bônus multiplicativo aplicado à nota final após os pesos das áreas do ENEM, sem teto por padrão", "BONUS-MULTIPLICATIVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fator\":\"numeric\",\"teto\":\"numeric|null\"}", "76a46eef115f27a52873a21eeee988a7d6978993ae535c026fa2c2d250d3acd1", "[\"nota_final × fator, após os pesos; teto opcional\"]", "regra_bonus", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000009"), "Lei 10.741/2003 art. 27 (Estatuto do Idoso)", "DESEMPATE-IDOSO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"idade_minima\":\"integer\"}", "6738b1a9ca4f063f7f215cabb837e055d8abafaf0835f0b3deeed1c97f0becd4", "[\"prioriza quem satisfaz FAIXA_ETARIA ≥ idade_minima\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000010"), "Edital (ordem de desempate)", "DESEMPATE-MAIOR-NOTA-ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"etapa\":\"text\"}", "1a988e6681b2970dc6c568c7372ceaf2f6e5ec0f7061ac47f656e1366a9ecad8", "[\"ordena por maior nota da etapa indicada\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000011"), "Edital (maior idade cronológica)", "DESEMPATE-MAIOR-IDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "1efa26eaeffc88baf31ce9e2030c05c9976fae125e9845fe0283168050dc1237", "[\"ordena por data de nascimento (nascido mais cedo vence)\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000012"), "Edital (critério adicional via fato do candidato — ex.: professor rural)", "DESEMPATE-PREDICADO-FATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fato\":\"text\",\"operador\":\"text\",\"valor\":\"any\"}", "d832d910826f25b6b50fd324f2f3cae472440c0e81e082be7c8d4fefe3de3f21", "[\"prioriza quem satisfaz predicado sobre FatoCandidato; fato deve estar no vocabulário e ser coletado\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000013"), "UNI-REQ-0045 — processamento da 1ª opção antes da 2ª", "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "2b70a91223c2d03b6e89ce77ae88699dbfa3ed6c9c3e84fae88b8dc3b4c9444b", "[\"processam-se todas as 1ªs opções de cada curso\",\"a vaga de modalidade não preenchida é remanejada dentro do curso, entre candidatos de 1ª opção\",\"só a vaga que sobra depois do remanejamento vai à 2ª opção, em ordem de nota, sem deslocar aprovado de 1ª opção\",\"quem não é classificado em nenhuma opção vai para a lista de espera\"]", "regra_ordem_alocacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000014"), "Lei 12.711/2012 art. 1º parágrafo único (red. Lei 14.723/2023) — renda familiar bruta per capita ≤ 1 SM (ensino superior); PN MEC 18/2012 art. 6º-7º — apuração da renda mensal per capita + exclusões obrigatórias", "RENDA-PER-CAPITA-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"limite_sm\":\"numeric\",\"sm_referencia\":\"numeric (SM congelado na data de início das inscrições)\",\"periodo_apuracao_meses\":\"integer (ex.: 3 últimos meses)\",\"criterio_media_mensal\":\"text (média mensal dos rendimentos brutos)\",\"exclusoes_renda\":\"lista (PN 18/2012 art. 7º)\",\"composicao_nucleo_familiar\":\"quem compõe o núcleo familiar\"}", "5a1ad80627e354c03e4d6ef776a45db695a1203cea574a288dbcdf706ca58899", "[\"média mensal da renda familiar bruta (últimos N meses, após exclusões) ÷ nº de membros do núcleo ≤ limite_sm × sm_referencia\"]", "regra_elegibilidade", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000017"), "Portaria MEC 18/2012 art. 11, parágrafo único (red. PN 2.027/2023) — cap nas vagas da oferta + prioridade do inciso III (LB) sobre o inciso IV (LI); art. 10 II (mínimo 50%, piso)", "RECONCILIACAO-VAGAS-ART11-PU", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"reconciliacao_federal\":\"CAP_VO + PRIORIDADE_LB (art. 11 §único, embutida em DISTRIB-VAGAS-LEI-12711)\",\"motores_nao_art10\":\"REDUZIR_DE | REDUZIR_PROPORCIONAL_EM (apenas distribuições institucionais; clamp >=0; VEDADOS à Lei 12.711)\"}", "ad2d8012ddc1f2ea4d763034899d07590c4a49901744b852dca2e70cede8b1e9", "[\"cap em VO: a reserva nunca excede as vagas da oferta (art. 11 §único I)\",\"prioridade LB>LI: na escassez a LI cede primeiro (art. 11 §único II)\",\"estouro sobre o nominal 50% (VR) e LEGAL — piso (art. 10 II), absorvido pela AC\",\"curso pequeno e determinístico por lei (capa, não bloqueia)\"]", "regra_ajuste_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000018"), "Lei 9.784/1999 art. 56 (cabimento do recurso administrativo) e art. 61 (efeito suspensivo por decisão fundamentada); prazo configurável por edital", "RECURSO-PRAZO-ANCORADO-EM-ATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"prazo_valor\":\"numeric (> 0) — inteiro quando a unidade é DIAS_UTEIS\",\"prazo_unidade\":\"DIAS_UTEIS|HORAS (sem default — dado do edital; DIAS não é declarável na interposição)\",\"suspensividade_primeira_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência na fase não bloqueia atos irreversíveis\",\"suspensividade_segunda_instancia\":\"{valor:numeric, unidade:HORAS|DIAS|DIAS_UTEIS} | null — null = a pendência em instância superior não bloqueia (via judicial, prazo indeterminado)\"}", "42cbba27f7e36789437ecb469c74476c99a37baaf66404769774eeff619b9e2d", "[\"o Uni+ gere apenas a 1ª instância — o julgamento em instância superior (administrativa ou judicial) corre FORA do sistema; a sua existência e o seu desfecho são REGISTRADOS como ato publicado\",\"a suspensividade é configurável por fase e por grau: null = a pendência não bloqueia atos irreversíveis\",\"a janela de suspensividade fecha no julgamento OU no fim do prazo, o que vier primeiro — recurso nunca julgado não trava o certame para sempre\",\"interposição só é aceita com a janela da fase de recurso aberta\",\"não cabe recurso contra resultado definitivo\",\"prazo ancorado no instante de publicação do ato âncora: se o ato atrasa, o prazo desliza junto, sem retificação\",\"a âncora é um produto PRELIMINAR publicado pela própria fase, referenciado pela identidade da publicação e não pelo código do tipo de ato — o mesmo tipo publicado por outra fase do cronograma não é âncora desta\",\"a âncora nunca é um tipo de ato que congela configuração\",\"o prazo de INTERPOSIÇÃO corre exclusivamente em dia útil e admite apenas DIAS_UTEIS em valor inteiro ou HORAS — DIAS corridos é recusado, e fração de dia útil também, cada um com causa própria; nunca aproximado em silêncio\",\"contagem sobre dia útil depende de duas declarações do processo, o calendário de dias úteis vigente da localidade regente e a convenção de contagem — vale para todo prazo de INTERPOSIÇÃO, nas duas unidades, e para a suspensividade em DIAS_UTEIS\",\"a suspensividade em HORAS ou em DIAS corridos não depende de calendário nem de convenção de contagem — conta todos os dias, sem distinguir úteis de não úteis\",\"append-only: julgamento e retificação são NOVO fato, não sobrescrevem o passado\"]", "regra_prazo_recurso", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000019"), "Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 2º e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 21/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", "REMANEJ-CASCATA-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fallbackCodigo\":\"AC\",\"ordens\":[\n  {\"origem\":\"LB_PPI\",\"destinos\":[\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_Q\",\"destinos\":[\"LB_PPI\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_PCD\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_EP\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_PPI\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_Q\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_PCD\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_EP\"]},\n  {\"origem\":\"LI_EP\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\"]}\n]}", "70f96f9fa5b2c5282adba8028070fdf0a82bef99408693d17d913c51d0c5078a", "[\"oito origens federais, ordem fixa por origem, terminal sempre AC — matriz não recalculada, só aplicada\"]", "criterio_remanejamento", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000020"), "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "fce95fc44b52a5a93a697b0309659a5af0085f9d39ceac1c3917c7b00b1c0be5", "[\"âncora fora da meia-noite: a hora da âncora não influencia o fechamento — o dia civil da âncora é excluído por inteiro e a contagem parte do primeiro dia útil seguinte (1 dia útil ancorado sexta 18h, sem feriado no intervalo, fecha no fim de segunda)\",\"âncora em dia não útil: o início desloca para o primeiro dia útil seguinte; o dia da âncora, útil ou não, nunca conta (1 dia útil ancorado domingo 18h, sem feriado no intervalo, fecha no fim de segunda)\",\"em dias úteis: N dias úteis inteiros contados após o dia excluído; a janela fecha na fronteira final do N-ésimo dia útil — dia civil fechado no início e aberto no fim, no fuso congelado\",\"em horas: a contagem começa no primeiro instante do primeiro dia útil seguinte ao dia da âncora e consome apenas horas situadas em dia útil (48h ancoradas sexta 18h, sem feriado no intervalo, começam segunda 00:00 e fecham quarta 00:00)\"]", "algoritmo_contagem_prazo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000021"), "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "49b637293aaaa71449dcb971fc548c59fc840545d2d1740ac352f858a291105b", "[\"âncora fora da meia-noite: a contagem parte do instante exato da âncora — a hora do fechamento deriva da hora da âncora, sem deslocamento para fronteira de dia (48h ancoradas sexta 18h, com sábado e domingo não úteis e sem feriado, fecham terça 18h)\",\"âncora em dia não útil: o início não desloca — o relógio não avança em instante situado em dia não útil e o primeiro avanço ocorre no primeiro instante útil seguinte (24h ancoradas domingo 18h, com segunda útil, só começam a consumir segunda 00:00 e fecham terça 00:00)\",\"em horas: consome exatamente o valor declarado em horas situadas em dia útil, atravessando a madrugada de dia útil normalmente; fecha no instante em que o saldo zera\",\"em dias úteis: N dias úteis equivalem a N×24 horas situadas em dia útil consumidas desde a âncora; dia civil de transição de fuso contribui com as horas que realmente tem, nunca um bloco presumido de 24 (1 dia útil ancorado sexta 18h, sem feriado no intervalo, fecha segunda 18h)\"]", "algoritmo_contagem_prazo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000022"), "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "CONTAGEM-PRAZO-AVANCA-DATA-UTIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "cd4c631492d02126c88a7ca5558992b3ed8a27c80692ecabfb73609293f2a9c8", "[\"âncora fora da meia-noite: mantém a hora da âncora — a contagem parte do instante exato, sem deslocamento para fronteira de dia (1 dia útil ancorado sexta 18h, com sábado e domingo não úteis e sem feriado, fecha segunda 18h)\",\"âncora em dia não útil: em dias úteis, desloca para o próximo dia útil na mesma hora (âncora domingo 18h conta como segunda 18h, e 1 dia útil fecha terça 18h); em horas não há deslocamento, apenas a não contagem dos instantes de dia não útil\",\"em dias úteis: fecha na mesma hora da âncora, N datas úteis adiante, pulando cada data não útil; se a hora da âncora não existir na data de fechamento por transição de fuso, fecha no primeiro instante válido seguinte\",\"em horas: consome horas situadas em dia útil desde a âncora, sem deslocar o início — nesta unidade a convenção coincide com CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA, e a diferença entre as duas está só na unidade dias úteis\"]", "algoritmo_contagem_prazo", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000023"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIB-VAGAS-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]}", "369e630525f26995a637d305851b4dfb0a713b8de8038a5874846ac512d7d375", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"INV-3a: LB_EP≥0 e LI_EP≥0\",\"INV-3b: AC≥0\",\"INV-3c: VR_final+RETIRADAS+AC=VO_base\",\"modalidade fora de modalidades_admitidas é recusada\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000024"), "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)", "DISTRIB-VAGAS-INSTITUCIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro institucional não nomeado por regra própria\",\"modalidades_admitidas\":null}", "a9f0f1cb005bf1fa891094b1271df22c0bf95605658659cd8a3f37f3e157e1dc", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"a soma das quantidades declaradas é o total publicado, e fecha no VO_base\",\"modalidades_admitidas nulo: rol aberto, para o certame institucional que ainda não tem regra própria\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000025"), "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "DISTRIB-VAGAS-PSIQ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"Processo Seletivo Indígena e Quilombola\",\"modalidades_admitidas\":[\"AC_I\",\"AC_Q\"]}", "010df079f624fb7a3306eed54285fa5ad1f9e3be8f43195527c67b99b1534398", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame exclusivo: não há ampla concorrência\",\"rol composto só de vagas por acréscimo — sem outro conjunto ao qual se somem, a soma delas é o total publicado\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000026"), "Res. Unifesspa 532/2021-CONSEPE, art. 1º (reserva de vaga para pessoa com deficiência); Portaria MEC 18/2012 art. 12", "DISTRIB-VAGAS-COM-AC-PCD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro fixo sem as cotas federais — AC_PCD como reserva de pessoa com deficiência fora do regime federal\",\"modalidades_admitidas\":[\"AC\",\"AC_PCD\"]}", "2bef4de324234a2d63164008d2e6dec6b4bb8901d816ee04f6824cbcc0027f80", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame sem as cotas da Lei 12.711\",\"AC_PCD retira de AC: o par fecha no VO_base\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000027"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIB-VAGAS-LEI-12711-COM-AC-PCD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\",\"AC_PCD\"]}", "a0097d270c2fcd478ff8fb41a419275b328d0e13b6124dc179b6d224b83c8d08", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"INV-3a: LB_EP≥0 e LI_EP≥0\",\"INV-3b: AC≥0\",\"INV-3c: VR_final+RETIRADAS+AC=VO_base\",\"AC_PCD retira de AC, como qualquer outra retirada federal\",\"modalidade fora de modalidades_admitidas é recusada\"]", "regra_distribuicao_vagas", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000028"), "Edital (ordem de desempate por nota de área do ENEM)", "DESEMPATE-MAIOR-NOTA-AREA-ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"areas\":\"text[]\"}", "81fd6b28df7be85dcfdeb6301d849029d1ac7066cd661e2e60e17fe817b74843", "[\"ordena por maior nota na primeira área da lista; o empate que resta passa à área seguinte\",\"cada área é citada uma única vez, pelo código, e existe em todos os grupos do quadro de pesos por área congelado na classificação\",\"exige classificação baseada em ENEM, calculada pela média ponderada\"]", "criterio_desempate", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000029"), "Edital (falta em dia de prova do ENEM)", "ELIM-FALTA-EM-DIA-DE-PROVA-ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "72c9dc358aaa3e81e2ccb08bc6d9ec3c0842c0a1aec5c001e2526c518bb3f3aa", "[\"falta em pelo menos um dia de prova da edição do ENEM usada no processo → elimina\",\"sem participação registrada na edição do ENEM indicada pelo candidato equivale a falta\"]", "regra_eliminacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000030"), "Decisão institucional da Unifesspa", "CONSOLIDACAO-AVALIACAO-UNICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "0fdac8b1bc6a5f9f7e4d2f257d695e5028210d6c8cb16876157755e36c6b05b0", "[\"um só avaliador lança a nota de cada candidato na etapa\",\"a nota lançada é a nota da etapa, sem combinação com outra\"]", "regra_consolidacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000031"), "UNI-REQ-0097 (dupla correção) e edital do processo seletivo", "CONSOLIDACAO-CORRECAO-INDEPENDENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "376e2e3ace2ba3302fb7d2a957e390e4eeeceb2bdc0102d65db688dd744a67c3", "[\"mais de um avaliador corrige cada candidato na etapa, cada um sem conhecer a nota do outro\",\"a nota da etapa é a combinação das correções independentes, na forma que o edital determina\"]", "regra_consolidacao", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000032"), "Edital (eliminação do candidato que faltar à etapa)", "ELIM-AUSENCIA-NA-ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"etapa_ref\":\"text\"}", "679d25bb9ac2d0948473cb183f5b8063a5caf3955a903980d6f3ed2af750035c", "[\"falta à etapa referenciada → elimina\",\"não se aplica à etapa de nota do ENEM\"]", "regra_eliminacao", null, "v1" }
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                columns: new[] { "id", "created_at", "ultimo_numero", "updated_at", "versao_vigente_id" },
                values: new object[] { new Guid("019a0000-0000-7000-8000-0000000000a1"), new DateTimeOffset(new DateTime(2026, 11, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipo_documento",
                columns: new[] { "id", "categoria", "classificacao_protecao", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "finalidade_tratamento", "formatos_aceitos", "hipotese_legal", "is_deleted", "nome", "tamanho_maximo_mb", "tipo_equivalente", "updated_at", "updated_by", "protecao_aceita_em", "protecao_aceita_por", "protecao_ciclo", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_justificativa", "protecao_manifestacao_desfavoravel", "protecao_manifestacoes_no_ciclo", "protecao_manifestada_em", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[,]
                {
                    { new Guid("d0c00000-0000-7000-8000-000000000001"), "IDENTIFICACAO", "IDENTIFICADOR", "RG", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "RG", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000002"), "IDENTIFICACAO", "IDENTIFICADOR", "CPF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "CPF", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000003"), "IDENTIFICACAO", "IDENTIFICADOR", "TITULO_ELEITOR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Título de eleitor", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000004"), "IDENTIFICACAO", "IDENTIFICADOR", "QUITACAO_ELEITORAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovante de quitação eleitoral", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000005"), "IDENTIFICACAO", "IDENTIFICADOR", "QUITACAO_SERVICO_MILITAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Quitação com o serviço militar", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000006"), "IDENTIFICACAO", "IDENTIFICADOR", "CERTIDAO_NASCIMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Certidão de nascimento", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000007"), "IDENTIFICACAO", "IDENTIFICADOR", "CERTIDAO_CASAMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Certidão de casamento", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000008"), "IDENTIFICACAO", "PESSOAL", "FOTO_3X4", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Foto 3x4", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000009"), "IDENTIFICACAO", "PESSOAL", "FOTO_FRENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Foto de frente", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000010"), "IDENTIFICACAO", "PESSOAL", "FOTO_PERFIL_DIREITO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Foto de perfil do lado direito", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000011"), "IDENTIFICACAO", "PESSOAL", "FOTO_PERFIL_ESQUERDO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Foto de perfil do lado esquerdo", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000012"), "ESCOLARIDADE", "PESSOAL", "HISTORICO_ENSINO_FUNDAMENTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Histórico do ensino fundamental", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000013"), "ESCOLARIDADE", "PESSOAL", "HISTORICO_ENSINO_MEDIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Histórico do ensino médio", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000014"), "ESCOLARIDADE", "PESSOAL", "CERTIFICADO_ENSINO_MEDIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Certificado de conclusão do ensino médio", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000015"), "ESCOLARIDADE", "PESSOAL", "HISTORICO_GRADUACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Histórico de graduação", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000016"), "ESCOLARIDADE", "PESSOAL", "DIPLOMA_GRADUACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Diploma de graduação", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000017"), "ESCOLARIDADE", "PESSOAL", "DIPLOMA_POS_GRADUACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Diploma de mestrado ou doutorado", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000018"), "ESCOLARIDADE", "PESSOAL", "MATRIZ_CURRICULAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Matriz curricular", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000019"), "ESCOLARIDADE", "PESSOAL", "DECLARACAO_VINCULO_INSTITUCIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de vínculo institucional", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000020"), "RENDA", "PESSOAL", "CONTRACHEQUE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Contracheque", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000021"), "RENDA", "PESSOAL", "CARTEIRA_TRABALHO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Carteira de trabalho", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000022"), "RENDA", "PESSOAL", "EXTRATO_FGTS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato do FGTS", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000023"), "RENDA", "PESSOAL", "EXTRATO_BANCARIO_PF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato bancário de pessoa física", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000024"), "RENDA", "PESSOAL", "EXTRATO_BANCARIO_PJ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato bancário de pessoa jurídica", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000025"), "RENDA", "PESSOAL", "EXTRATO_PAGAMENTO_BENEFICIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato de pagamento de benefício", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000026"), "RENDA", "PESSOAL", "NOTA_FISCAL_VENDAS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Nota fiscal de vendas", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000027"), "RENDA", "PESSOAL", "GUIA_RECOLHIMENTO_INSS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Guia de recolhimento ao INSS", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000028"), "RENDA", "PESSOAL", "CONTRATO_LOCACAO_ARRENDAMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Contrato de locação ou arrendamento", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000029"), "RENDA", "PESSOAL", "REGISTRATO_BACEN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Registrato do Banco Central", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000030"), "RENDA", "PESSOAL", "CADASTRO_UNICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Cadastro Único", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000031"), "RENDA", "PESSOAL", "DECLARACAO_IRPF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de IRPF", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000032"), "RENDA", "PESSOAL", "DECLARACAO_ISENCAO_IRPF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de isenção de IRPF", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000033"), "RENDA", "PESSOAL", "DECLARACAO_TRIBUTARIA_PJ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração tributária de pessoa jurídica", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000034"), "RENDA", "PESSOAL", "DECLARACAO_TRABALHADOR_RURAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de trabalhador rural", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000035"), "RENDA", "PESSOAL", "DECLARACAO_TRABALHADOR_AUTONOMO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de trabalhador autônomo", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000036"), "RENDA", "PESSOAL", "DECLARACAO_ATIVIDADE_DO_LAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de atividade do lar", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000037"), "RENDA", "PESSOAL", "DECLARACAO_AUSENCIA_RENDIMENTOS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de ausência de rendimentos", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000038"), "RENDA", "PESSOAL", "DECLARACAO_PENSAO_ALIMENTICIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de recebimento de pensão alimentícia", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000039"), "RENDA", "PESSOAL", "DECLARACAO_RENDIMENTO_ALUGUEL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de rendimentos de aluguel", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000040"), "RENDA", "PESSOAL", "DECLARACAO_SEM_CONTA_BANCARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de que não possui conta bancária", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000041"), "RENDA", "PESSOAL", "COMPROVANTE_PAGAMENTO_TAXA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovante de pagamento da taxa de inscrição", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000042"), "RACA_ETNIA", "SENSIVEL", "AUTODECLARACAO_ETNICO_RACIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Autodeclaração étnico-racial justificada", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000043"), "RACA_ETNIA", "SENSIVEL", "AUTODECLARACAO_INDIGENA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Autodeclaração indígena", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000044"), "RACA_ETNIA", "SENSIVEL", "AUTODECLARACAO_QUILOMBOLA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Autodeclaração quilombola", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000045"), "RACA_ETNIA", "SENSIVEL", "DECLARACAO_PERTENCIMENTO_INDIGENA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de pertencimento indígena", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000046"), "RACA_ETNIA", "SENSIVEL", "DECLARACAO_PERTENCIMENTO_QUILOMBOLA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de pertencimento quilombola", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000047"), "SAUDE", "SENSIVEL", "LAUDO_MEDICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Laudo médico", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000048"), "SAUDE", "SENSIVEL", "COMPROVANTE_VACINACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovante de vacinação", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000049"), "RESIDENCIA", "PESSOAL", "COMPROVANTE_RESIDENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovante de residência", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000050"), "RESIDENCIA", "PESSOAL", "DECLARACAO_RESIDENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de residência", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000051"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "REQUERIMENTO_INSCRICAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Requerimento de inscrição", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000052"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "REQUERIMENTO_NOME_SOCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Requerimento de inclusão de nome social", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000053"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "REQUERIMENTO_DESISTENCIA_VAGA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Requerimento de desistência de vaga", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000054"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "RECURSO_ADMINISTRATIVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Recurso administrativo", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000055"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "PROCURACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Procuração", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000056"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "TERMO_ACEITE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Termo de aceite", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000057"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "TERMO_COMPROMISSO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Termo de compromisso", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000058"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "DECLARACAO_AUTENTICIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de autenticidade dos documentos", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000059"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "DECLARACAO_DISPONIBILIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de disponibilidade", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000060"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "FORMULARIO_OPCAO_ENTREVISTA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Formulário de escolha do formato de entrevista", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000061"), "TITULACAO_EXPERIENCIA", "PESSOAL", "DECLARACAO_FUNCIONAL_SIG", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração funcional emitida pelo SIG", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000062"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_EXPERIENCIA_PROFISSIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de experiência profissional", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000063"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_EXPERIENCIA_ESCOLA_CAMPO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de experiência profissional em escola do campo", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000064"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_ATIVIDADE_POVOS_TRADICIONAIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de atividade junto a povos tradicionais", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000065"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_VINCULO_DOCENTE_REDE_PUBLICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de vínculo docente na rede pública", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000066"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_ATUACAO_APOIO_EDUCACAO_ESPECIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de atuação como profissional de apoio da educação especial", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000067"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_LATTES_PESQUISA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de pesquisa concluída no currículo Lattes", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000068"), "TITULACAO_EXPERIENCIA", "PESSOAL", "COMPROVACAO_LATTES_EXTENSAO_ENSINO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Comprovação de extensão e ensino no currículo Lattes", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000069"), "PRODUCAO_AVALIATIVA", "PESSOAL", "CARTA_INTENCAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Carta de intenção", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000070"), "PRODUCAO_AVALIATIVA", "PESSOAL", "RELATO_HISTORIA_VIDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Relato de história de vida", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000071"), "IDENTIFICACAO", "IDENTIFICADOR", "CIN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Carteira de Identidade Nacional", null, "RG", null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000072"), "IDENTIFICACAO", "IDENTIFICADOR", "CNH", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Carteira Nacional de Habilitação", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000073"), "IDENTIFICACAO", "IDENTIFICADOR", "PASSAPORTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Passaporte", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000074"), "IDENTIFICACAO", "IDENTIFICADOR", "RNM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Registro Nacional Migratório", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000075"), "IDENTIFICACAO", "SENSIVEL", "BOLETIM_OCORRENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Boletim de ocorrência", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000076"), "IDENTIFICACAO", "SENSIVEL", "DOCUMENTO_JUDICIAL_GUARDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Documento judicial de guarda", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000077"), "ESCOLARIDADE", "PESSOAL", "DECLARACAO_CONCLUSAO_ENSINO_MEDIO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Atestado ou declaração de conclusão do ensino médio", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000078"), "RENDA", "PESSOAL", "CNIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato do Cadastro Nacional de Informações Sociais (CNIS)", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000079"), "RENDA", "PESSOAL", "DECLARACAO_IRPJ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de IRPJ", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000080"), "RENDA", "PESSOAL", "ECF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Escrituração Contábil Fiscal (ECF)", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000081"), "RENDA", "PESSOAL", "DECORE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração Comprobatória de Percepção de Rendimentos (DECORE)", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000082"), "RENDA", "PESSOAL", "DEFIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de Informações Socioeconômicas e Fiscais (DEFIS)", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000083"), "RENDA", "PESSOAL", "DASN_SIMEI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração Anual do Simples Nacional do MEI (DASN-SIMEI)", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000084"), "RENDA", "PESSOAL", "DECLARACAO_INATIVIDADE_EMPRESA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de inatividade da empresa", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000085"), "RENDA", "PESSOAL", "DECLARACAO_RENDIMENTOS_AUFERIDOS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de rendimentos auferidos", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000086"), "RENDA", "PESSOAL", "DECLARACAO_AUSENCIA_RENDIMENTOS_ALUGUEL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Declaração de ausência de rendimentos de aluguel", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000087"), "RENDA", "PESSOAL", "EXTRATO_PREVIDENCIA_PRIVADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Extrato de benefício de previdência privada", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000088"), "RENDA", "PESSOAL", "TRCT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Termo de rescisão do contrato de trabalho (TRCT)", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000089"), "SAUDE", "SENSIVEL", "LAUDO_PSICOLOGICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Laudo psicológico", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000090"), "SAUDE", "SENSIVEL", "EXAME_AUDIOMETRIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Exame de audiometria", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000091"), "SAUDE", "SENSIVEL", "EXAME_OFTALMOLOGICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Exame oftalmológico com acuidade visual", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("d0c00000-0000-7000-8000-000000000092"), "DOCUMENTO_PROCESSUAL", "PESSOAL", "TERMO_RESPONSABILIDADE_CERTIFICADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, "Comprovação documental, no processo seletivo, dos requisitos e das condições de participação do candidato.", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", false, "Termo de responsabilidade pela entrega do certificado de conclusão", null, null, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "id", "ativo", "codigo", "created_at", "descricao", "fato_candidato_id", "ordem", "orientacao", "updated_at" },
                values: new object[,]
                {
                    { new Guid("fa70d000-0000-7000-8000-000000000001"), true, "BRANCA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Branca", new Guid("fa700000-0000-7000-8000-000000000001"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000002"), true, "PRETA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Preta", new Guid("fa700000-0000-7000-8000-000000000001"), 1, "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação da Unifesspa (Lei 12.711/2012).", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000003"), true, "PARDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parda", new Guid("fa700000-0000-7000-8000-000000000001"), 2, "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação da Unifesspa (Lei 12.711/2012).", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000004"), true, "AMARELA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Amarela", new Guid("fa700000-0000-7000-8000-000000000001"), 3, "Pessoa de origem asiática.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000005"), true, "INDIGENA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Indígena", new Guid("fa700000-0000-7000-8000-000000000001"), 4, "Pessoa que se reconhece como pertencente a um povo indígena.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000006"), true, "NAO_INFORMADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Prefiro não informar", new Guid("fa700000-0000-7000-8000-000000000001"), 5, "Sem essa informação, você não concorre às vagas reservadas por cor ou raça.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000007"), true, "FEMININO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Feminino", new Guid("fa700000-0000-7000-8000-000000000007"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000008"), true, "MASCULINO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Masculino", new Guid("fa700000-0000-7000-8000-000000000007"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000009"), true, "INTERSEXO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Intersexo", new Guid("fa700000-0000-7000-8000-000000000007"), 2, "Pessoa com variação natural das características sexuais.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000010"), true, "NATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Brasileiro nato", new Guid("fa700000-0000-7000-8000-000000000010"), 0, "Nascido no Brasil ou nas condições previstas pela Constituição.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000011"), true, "NATURALIZADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Brasileiro naturalizado", new Guid("fa700000-0000-7000-8000-000000000010"), 1, "Com processo de naturalização reconhecido.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000012"), true, "ESTRANGEIRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Estrangeiro", new Guid("fa700000-0000-7000-8000-000000000010"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000013"), true, "PROPRIO_CANDIDATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "O próprio candidato", new Guid("fa700000-0000-7000-8000-000000000026"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000014"), true, "CONJUGE_OU_COMPANHEIRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cônjuge ou companheiro(a)", new Guid("fa700000-0000-7000-8000-000000000026"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000015"), true, "FILHO_OU_ENTEADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Filho(a) ou enteado(a)", new Guid("fa700000-0000-7000-8000-000000000026"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000016"), true, "PAI_OU_MAE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pai ou mãe", new Guid("fa700000-0000-7000-8000-000000000026"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000017"), true, "PADRASTO_OU_MADRASTA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Padrasto ou madrasta", new Guid("fa700000-0000-7000-8000-000000000026"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000018"), true, "IRMAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Irmão ou irmã", new Guid("fa700000-0000-7000-8000-000000000026"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000019"), true, "AVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Avô ou avó", new Guid("fa700000-0000-7000-8000-000000000026"), 6, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000020"), true, "NETO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Neto(a)", new Guid("fa700000-0000-7000-8000-000000000026"), 7, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000021"), true, "SOGRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sogro ou sogra", new Guid("fa700000-0000-7000-8000-000000000026"), 8, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000022"), true, "GENRO_OU_NORA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Genro ou nora", new Guid("fa700000-0000-7000-8000-000000000026"), 9, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000023"), true, "OUTRO_PARENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Outro parente", new Guid("fa700000-0000-7000-8000-000000000026"), 10, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000024"), true, "NAO_PARENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sem parentesco", new Guid("fa700000-0000-7000-8000-000000000026"), 11, "Pessoa sem parentesco com o candidato que integra o grupo familiar.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000027"), true, "SOLTEIRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solteiro(a)", new Guid("fa700000-0000-7000-8000-000000000038"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000028"), true, "CASADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Casado(a)", new Guid("fa700000-0000-7000-8000-000000000038"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000029"), true, "UNIAO_ESTAVEL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Em união estável", new Guid("fa700000-0000-7000-8000-000000000038"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000030"), true, "SEPARADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Separado(a) judicialmente", new Guid("fa700000-0000-7000-8000-000000000038"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000031"), true, "DIVORCIADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Divorciado(a)", new Guid("fa700000-0000-7000-8000-000000000038"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000032"), true, "VIUVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Viúvo(a)", new Guid("fa700000-0000-7000-8000-000000000038"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000033"), true, "URBANO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Zona urbana", new Guid("fa700000-0000-7000-8000-000000000044"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000034"), true, "RURAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Zona rural", new Guid("fa700000-0000-7000-8000-000000000044"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000035"), true, "ALDEIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aldeia indígena", new Guid("fa700000-0000-7000-8000-000000000044"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000036"), true, "COMUNIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Comunidade tradicional", new Guid("fa700000-0000-7000-8000-000000000044"), 3, "Comunidade ribeirinha, extrativista, de pescadores ou outra comunidade tradicional.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000037"), true, "QUILOMBO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Quilombo", new Guid("fa700000-0000-7000-8000-000000000044"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000038"), true, "VILA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Vila", new Guid("fa700000-0000-7000-8000-000000000044"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000039"), true, "OUTRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Outro", new Guid("fa700000-0000-7000-8000-000000000044"), 6, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000040"), true, "REGULAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ensino médio regular", new Guid("fa700000-0000-7000-8000-000000000046"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000041"), true, "EJA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Educação de Jovens e Adultos (EJA)", new Guid("fa700000-0000-7000-8000-000000000046"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000042"), true, "ENCCEJA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Certificação pelo ENCCEJA", new Guid("fa700000-0000-7000-8000-000000000046"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000043"), true, "PROFICIENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Exame de proficiência dos sistemas estaduais de ensino", new Guid("fa700000-0000-7000-8000-000000000046"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000044"), true, "ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Certificação pelo ENEM", new Guid("fa700000-0000-7000-8000-000000000046"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000045"), true, "SOMENTE_PUBLICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Somente em escola pública", new Guid("fa700000-0000-7000-8000-000000000047"), 0, "Todos os anos que você cursou, mesmo que tenha concluído por certificação sem cursar todos.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000046"), true, "COMUNITARIA_CAMPO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola comunitária do campo conveniada com o poder público", new Guid("fa700000-0000-7000-8000-000000000047"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000047"), true, "PRIVADA_BOLSA_INTEGRAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola privada com bolsa integral", new Guid("fa700000-0000-7000-8000-000000000047"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000048"), true, "PRIVADA_BOLSA_PARCIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola privada com bolsa parcial", new Guid("fa700000-0000-7000-8000-000000000047"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000049"), true, "PRIVADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Escola privada", new Guid("fa700000-0000-7000-8000-000000000047"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000050"), true, "PUBLICA_E_PRIVADA_BOLSA_INTEGRAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parte em escola pública e parte em escola privada com bolsa integral", new Guid("fa700000-0000-7000-8000-000000000047"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000051"), true, "PUBLICA_E_FORA_DA_REDE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parte na rede pública (escola pública ou comunitária do campo conveniada) e parte fora dela, nos demais casos", new Guid("fa700000-0000-7000-8000-000000000047"), 6, "A parte fora da rede pública em escola privada sem bolsa integral, no Sistema S ou no exterior.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000052"), true, "PUBLICA_E_COMUNITARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parte em escola pública e parte em escola comunitária do campo conveniada", new Guid("fa700000-0000-7000-8000-000000000047"), 7, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000053"), true, "SISTEMA_S", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sistema S", new Guid("fa700000-0000-7000-8000-000000000047"), 8, "Escolas do SESI, SENAI, SESC, SENAC e demais serviços sociais autônomos.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000054"), true, "EXTERIOR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Exterior", new Guid("fa700000-0000-7000-8000-000000000047"), 9, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000055"), true, "MAIS_DE_UM_FORA_DA_REDE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Mais de um tipo de escola fora da rede pública", new Guid("fa700000-0000-7000-8000-000000000047"), 10, "Escola privada, Sistema S ou exterior, sem passar pela rede pública.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000056"), true, "CERTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Concluí por certificação", new Guid("fa700000-0000-7000-8000-000000000047"), 11, "Para quem concluiu pelo ENCCEJA, por exame de proficiência ou pelo ENEM sem frequentar o ensino médio.", null },
                    { new Guid("fa70d000-0000-7000-8000-000000000057"), true, "SSP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Secretaria de Segurança Pública (SSP)", new Guid("fa700000-0000-7000-8000-000000000032"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000058"), true, "PC", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Polícia Civil (PC)", new Guid("fa700000-0000-7000-8000-000000000032"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000059"), true, "DETRAN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Departamento Estadual de Trânsito (DETRAN)", new Guid("fa700000-0000-7000-8000-000000000032"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000060"), true, "IFP", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Instituto Félix Pacheco (IFP)", new Guid("fa700000-0000-7000-8000-000000000032"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000061"), true, "PM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Polícia Militar (PM)", new Guid("fa700000-0000-7000-8000-000000000032"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000062"), true, "CBM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Corpo de Bombeiros Militar (CBM)", new Guid("fa700000-0000-7000-8000-000000000032"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000063"), true, "EB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Exército Brasileiro (EB)", new Guid("fa700000-0000-7000-8000-000000000032"), 6, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000064"), true, "MB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Marinha do Brasil (MB)", new Guid("fa700000-0000-7000-8000-000000000032"), 7, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000065"), true, "FAB", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Força Aérea Brasileira (FAB)", new Guid("fa700000-0000-7000-8000-000000000032"), 8, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000066"), true, "DPF", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Departamento de Polícia Federal (DPF)", new Guid("fa700000-0000-7000-8000-000000000032"), 9, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000067"), true, "MRE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ministério das Relações Exteriores (MRE)", new Guid("fa700000-0000-7000-8000-000000000032"), 10, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000068"), true, "OUTRO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Outro", new Guid("fa700000-0000-7000-8000-000000000032"), 11, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000069"), true, "FEMININO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Feminino", new Guid("fa700000-0000-7000-8000-000000000057"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000070"), true, "MASCULINO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Masculino", new Guid("fa700000-0000-7000-8000-000000000057"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000071"), true, "PESSOA_CIS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa cisgênero", new Guid("fa700000-0000-7000-8000-000000000058"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000072"), true, "PESSOA_TRANSGENERO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa transgênero", new Guid("fa700000-0000-7000-8000-000000000058"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000073"), true, "TRAVESTI", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Travesti", new Guid("fa700000-0000-7000-8000-000000000058"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000074"), true, "TRANSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Transexual", new Guid("fa700000-0000-7000-8000-000000000058"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000075"), true, "PESSOA_NAO_BINARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pessoa não binária", new Guid("fa700000-0000-7000-8000-000000000058"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000076"), true, "PREFIRO_NAO_INFORMAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Prefiro não informar", new Guid("fa700000-0000-7000-8000-000000000058"), 5, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000077"), true, "HETEROSSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Heterossexual", new Guid("fa700000-0000-7000-8000-000000000059"), 0, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000078"), true, "HOMOSSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Homossexual", new Guid("fa700000-0000-7000-8000-000000000059"), 1, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000079"), true, "BISSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bissexual", new Guid("fa700000-0000-7000-8000-000000000059"), 2, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000080"), true, "ASSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Assexual", new Guid("fa700000-0000-7000-8000-000000000059"), 3, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000081"), true, "PANSEXUAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pansexual", new Guid("fa700000-0000-7000-8000-000000000059"), 4, null, null },
                    { new Guid("fa70d000-0000-7000-8000-000000000082"), true, "PREFIRO_NAO_INFORMAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Prefiro não informar", new Guid("fa700000-0000-7000-8000-000000000059"), 5, null, null }
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfil_permissoes",
                columns: new[] { "codigo", "perfil_id" },
                values: new object[,]
                {
                    { "autorizacao:concessoes:administrar", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:pedidos:aprovar-funcional", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:perfis:aprovar-funcional", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:perfis:manter", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:revisoes:rever-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:termos:publicar", new Guid("019a0000-0000-7000-8000-000000000001") },
                    { "autorizacao:concessoes:administrar", new Guid("019a0000-0000-7000-8000-000000000002") },
                    { "autorizacao:pedidos:aprovar-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000002") },
                    { "autorizacao:perfis:aprovar-protecao-de-dados", new Guid("019a0000-0000-7000-8000-000000000002") },
                    { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000002") },
                    { "autorizacao:revisoes:conduzir", new Guid("019a0000-0000-7000-8000-000000000002") },
                    { "configuracao:protecao-do-catalogo:aprovar", new Guid("019a0000-0000-7000-8000-000000000002") },
                    { "autorizacao:concessoes:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:pedidos:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:perfis:consultar-versoes", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:permissoes:consultar-catalogo", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:providencias:solicitar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "autorizacao:revisoes:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "configuracao:protecao-do-catalogo:consultar", new Guid("019a0000-0000-7000-8000-000000000003") },
                    { "configuracao:protecao-do-catalogo:manifestar", new Guid("019a0000-0000-7000-8000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "ux_aceites_da_inscricao_ordem",
                schema: "selecao",
                table: "aceites_da_inscricao",
                columns: new[] { "inscricao_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_aceites_do_termo_versao_do_termo_id",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "versao_do_termo_id");

            migrationBuilder.CreateIndex(
                name: "ux_aceites_do_termo_pedido",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "pedido_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anexo_do_ato_nome",
                schema: "publicacoes",
                table: "anexo_do_ato",
                columns: new[] { "ato_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anexo_do_ato_ordem",
                schema: "publicacoes",
                table: "anexo_do_ato",
                columns: new[] { "ato_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_anexos_do_ato_do_processo_documento_a_publicar_id",
                schema: "selecao",
                table: "anexos_do_ato_do_processo",
                column: "documento_a_publicar_id");

            migrationBuilder.CreateIndex(
                name: "ux_anexos_do_ato_do_processo_ato_documento",
                schema: "selecao",
                table: "anexos_do_ato_do_processo",
                columns: new[] { "ato_do_processo_id", "documento_a_publicar_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anexos_do_ato_do_processo_ordem",
                schema: "selecao",
                table: "anexos_do_ato_do_processo",
                columns: new[] { "ato_do_processo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_apresentacoes_de_documento_inscricao_exigencia",
                schema: "selecao",
                table: "apresentacoes_de_documento",
                columns: new[] { "inscricao_id", "exigencia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_peso_area_enem_congeladas_grupo_peso_area_enem_congel",
                schema: "selecao",
                table: "areas_peso_area_enem_congeladas",
                columns: new[] { "grupo_peso_area_enem_congelado_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_apresentacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "apresentacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_em_verificacao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "proxima_publicacao_em",
                filter: "situacao = 1");

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_inscricao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_ato_normativo_numeracao",
                schema: "publicacoes",
                table: "ato_normativo",
                columns: new[] { "orgao", "serie", "ano", "numero" });

            migrationBuilder.CreateIndex(
                name: "ix_ato_normativo_publicado_em",
                schema: "publicacoes",
                table: "ato_normativo",
                columns: new[] { "publicado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_ato_normativo_retificado",
                schema: "publicacoes",
                table: "ato_normativo",
                column: "ato_retificado_id",
                unique: true,
                filter: "ato_retificado_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_documento_principal_id",
                schema: "selecao",
                table: "atos_do_processo",
                column: "documento_principal_id");

            migrationBuilder.CreateIndex(
                name: "ix_atos_do_processo_processo_recentes",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "publicado_em", "ordem_de_registro" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_ato_corrigido",
                schema: "selecao",
                table: "atos_do_processo",
                column: "ato_corrigido_id",
                unique: true,
                filter: "ato_corrigido_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_item_original",
                schema: "selecao",
                table: "atos_do_processo",
                columns: new[] { "processo_seletivo_id", "item_do_edital" },
                unique: true,
                filter: "item_do_edital IS NOT NULL AND ato_corrigido_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_atos_do_processo_ordem_de_registro",
                schema: "selecao",
                table: "atos_do_processo",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_da_etapa_inscricao_etapa",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "inscricao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_da_etapa_lote_id_etapa_processo_id",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "lote_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_avaliacoes_da_etapa_lote_inscricao_oferta",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "lote_id", "inscricao_id", "oferta_curso_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_avisos_da_reclassificacao_fato",
                schema: "autorizacao",
                table: "avisos_da_reclassificacao",
                columns: new[] { "origem", "origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_avisos_emitidos_tipo_fato",
                schema: "autorizacao",
                table: "avisos_emitidos",
                columns: new[] { "tipo", "fato_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bancas_da_etapa_codigo",
                schema: "selecao",
                table: "bancas_da_etapa",
                columns: new[] { "etapa_processo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bancas_requeridas_fase_cronograma_id",
                schema: "selecao",
                table: "bancas_requeridas",
                column: "fase_cronograma_id");

            migrationBuilder.CreateIndex(
                name: "IX_base_legal_bonus_regional_tipo_instrumento",
                schema: "configuracao",
                table: "base_legal_bonus_regional",
                column: "tipo_instrumento");

            migrationBuilder.CreateIndex(
                name: "ix_bases_da_permissao_concessao_codigo",
                schema: "autorizacao",
                table: "bases_da_permissao_na_concessao",
                columns: new[] { "concessao_id", "codigo", "ordem_na_trilha" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_bases_da_permissao_na_concessao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "bases_da_permissao_na_concessao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bases_da_permissao_ordem",
                schema: "autorizacao",
                table: "bases_da_permissao_na_concessao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendario_dias_uteis_versao_dataset",
                schema: "configuracao",
                table: "calendario_dias_uteis",
                column: "versao_dataset",
                filter: "is_deleted = false");

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
                name: "ix_categoria_documento_codigo_vivo",
                schema: "configuracao",
                table: "categoria_documento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_categoria_documento_ordem_codigo",
                schema: "configuracao",
                table: "categoria_documento",
                columns: new[] { "ordem", "codigo" });

            migrationBuilder.CreateIndex(
                name: "ux_categorias_julgadas_codigo",
                schema: "selecao",
                table: "categorias_julgadas",
                columns: new[] { "banca_requerida_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_modalidades",
                schema: "selecao",
                table: "certames_divulgados",
                column: "modalidades_ofertadas")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_nome_ordenacao",
                schema: "selecao",
                table: "certames_divulgados",
                columns: new[] { "nome_ordenacao", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_prazo",
                schema: "selecao",
                table: "certames_divulgados",
                columns: new[] { "inscricoes_ate", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_revisao",
                schema: "selecao",
                table: "certames_divulgados",
                column: "divulgado_em");

            migrationBuilder.CreateIndex(
                name: "ux_certames_divulgados_ato_criador",
                schema: "selecao",
                table: "certames_divulgados",
                column: "ato_criador_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_certames_divulgados_identificador_legivel",
                schema: "selecao",
                table: "certames_divulgados",
                column: "identificador_legivel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chamadas_edital_id_numero",
                schema: "ingresso",
                table: "chamadas",
                columns: new[] { "edital_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chaves_a_limpar_limpar_apos",
                schema: "selecao",
                table: "chaves_a_limpar",
                column: "limpar_apos");

            migrationBuilder.CreateIndex(
                name: "ix_comprovantes_inscricao_inscricao_numero",
                schema: "selecao",
                table: "comprovantes_inscricao",
                columns: new[] { "inscricao_id", "numero_inscricao" });

            migrationBuilder.CreateIndex(
                name: "ix_concessoes_perfil_ativas",
                schema: "autorizacao",
                table: "concessoes",
                column: "perfil_id",
                filter: "revogada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_concessoes_perfil_versao",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "perfil_id", "versao_id" },
                filter: "revogada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_concessoes_revogada_pela_janela_id_perfil_id",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "revogada_pela_janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ux_concessoes_ativa_unica",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "beneficiario_emissor", "beneficiario_sub", "perfil_id", "escopo_tipo", "unidade_id" },
                unique: true,
                filter: "revogada_em IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_concessoes_origem",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "origem_tipo", "origem_id", "beneficiario_emissor", "beneficiario_sub" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_condicao_atendimento_especializado_codigo_vivo",
                schema: "configuracao",
                table: "condicao_atendimento_especializado",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_gatilho_documento_exigido_id",
                schema: "selecao",
                table: "condicoes_gatilho",
                column: "documento_exigido_id");

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_precondicao_fato_fato_coletado_id",
                schema: "selecao",
                table: "condicoes_precondicao_fato",
                column: "fato_coletado_id");

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_regra_derivacao_regra_derivacao_configurada_id",
                schema: "selecao",
                table: "condicoes_regra_derivacao",
                column: "regra_derivacao_configurada_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_bonus_regional_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_bonus_regional",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_bonus_regional_municipio_configuracao_bonus_r",
                schema: "selecao",
                table: "configuracoes_bonus_regional_municipio",
                column: "configuracao_bonus_regional_id");

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_cascata_remanejamento_processo",
                schema: "selecao",
                table: "configuracoes_cascata_remanejamento",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_classificacao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_classificacao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_derivacao_fato_processo_fato",
                schema: "selecao",
                table: "configuracoes_derivacao_fato",
                columns: new[] { "processo_seletivo_id", "codigo_fato" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_distribuicao_vagas_processo_oferta",
                schema: "selecao",
                table: "configuracoes_distribuicao_vagas",
                columns: new[] { "processo_seletivo_id", "oferta_curso_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_divulgacao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_divulgacao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_taxa_inscricao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_taxa_inscricao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_convocacoes_chamada_id",
                schema: "ingresso",
                table: "convocacoes",
                column: "chamada_id");

            migrationBuilder.CreateIndex(
                name: "ix_convocacoes_protocolo",
                schema: "ingresso",
                table: "convocacoes",
                column: "protocolo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_criterios_desempate_processo_ordem",
                schema: "selecao",
                table: "criterios_desempate",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curso_codigo_vivo",
                schema: "configuracao",
                table: "curso",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_curso_ordenacao_alfabetica",
                schema: "configuracao",
                table: "curso",
                columns: new[] { "nome_ordenacao", "codigo", "id" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_janela_id_envio_id_envio_etapa",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "janela_id", "envio_id", "envio_etapa" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_janela_id_lote_de_origem_id_lote_de_or",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "janela_id", "lote_de_origem_id", "lote_de_origem_etapa" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_janela_id_perfil_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_perfil_id_versao_avaliada_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "perfil_id", "versao_avaliada_id" });

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_migracao_ordem_de_registro",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_retomada_etapa",
                schema: "autorizacao",
                table: "decisoes_da_retomada",
                columns: new[] { "suspensao_id", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_retomada_ordem",
                schema: "autorizacao",
                table: "decisoes_da_retomada",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_versao_etapa",
                schema: "autorizacao",
                table: "decisoes_da_versao",
                columns: new[] { "versao_id", "ciclo", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_desativacoes_do_perfil_aberta",
                schema: "autorizacao",
                table: "desativacoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "reativada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_desativacoes_do_perfil_ordem",
                schema: "autorizacao",
                table: "desativacoes_do_perfil",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_destinos_remanejamento_cascata_origem_destino",
                schema: "selecao",
                table: "destinos_remanejamento",
                columns: new[] { "configuracao_cascata_remanejamento_id", "modalidade_origem_codigo", "modalidade_destino_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_destinos_remanejamento_cascata_origem_ordem",
                schema: "selecao",
                table: "destinos_remanejamento",
                columns: new[] { "configuracao_cascata_remanejamento_id", "modalidade_origem_codigo", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dia_nao_util_calendario_data",
                schema: "configuracao",
                table: "dia_nao_util",
                columns: new[] { "calendario_dias_uteis_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_divergencias_da_governanca_implantacao_id",
                schema: "autorizacao",
                table: "divergencias_da_governanca",
                column: "implantacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_divergencias_da_governanca_perfil_id",
                schema: "autorizacao",
                table: "divergencias_da_governanca",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "ux_divergencias_da_governanca_ordem",
                schema: "autorizacao",
                table: "divergencias_da_governanca",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documentos_a_publicar_processo_recentes",
                schema: "selecao",
                table: "documentos_a_publicar",
                columns: new[] { "processo_seletivo_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_da_versao_perfil_id_versao_id",
                schema: "autorizacao",
                table: "documentos_da_versao",
                columns: new[] { "perfil_id", "versao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_documentos_da_versao_vivo",
                schema: "autorizacao",
                table: "documentos_da_versao",
                column: "versao_id",
                unique: true,
                filter: "situacao <> 'recusado'");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_exigido_na_etapa_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "exigido_na_etapa_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_exigido_na_fase_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "exigido_na_fase_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_processo_seletivo_id",
                schema: "selecao",
                table: "documentos_exigidos",
                column: "processo_seletivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_exigidos_base_legal_documento_exigido_id",
                schema: "selecao",
                table: "documentos_exigidos_base_legal",
                column: "documento_exigido_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_matricula_matricula_id",
                schema: "ingresso",
                table: "documentos_matricula",
                column: "matricula_id");

            migrationBuilder.CreateIndex(
                name: "ix_enderecos_da_inscricao_ocorrencia_id",
                schema: "selecao",
                table: "enderecos_da_inscricao",
                column: "ocorrencia_id");

            migrationBuilder.CreateIndex(
                name: "ux_enderecos_da_inscricao_fato",
                schema: "selecao",
                table: "enderecos_da_inscricao",
                columns: new[] { "inscricao_id", "ocorrencia_id", "fato_codigo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_etapas_formulario_formulario_codigo",
                schema: "selecao",
                table: "etapas_formulario",
                columns: new[] { "formulario_processo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_etapas_formulario_formulario_ordem",
                schema: "selecao",
                table: "etapas_formulario",
                columns: new[] { "formulario_processo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_etapas_processo_processo_seletivo_id_ordem",
                schema: "selecao",
                table: "etapas_processo",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_decisao_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "decisao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_desativacao_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "desativacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_perfil",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                columns: new[] { "perfil_id", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_suspensao_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "suspensao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_versao_que_entrou_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "versao_que_entrou_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_da_contencao_versao_vigente_id",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "versao_vigente_id");

            migrationBuilder.CreateIndex(
                name: "ux_eventos_da_contencao_ordem",
                schema: "autorizacao",
                table: "eventos_da_contencao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fase_canonica_codigo_vivo",
                schema: "configuracao",
                table: "fase_canonica",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_fase_canonica",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "fase_canonica_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_ordem",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fato_valor_dominio_fato_codigo",
                schema: "configuracao",
                table: "fato_valor_dominio",
                columns: new[] { "fato_candidato_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_grupo_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "grupo_coletado_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_fato",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "fato_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fatos_coletados_processo_finalidade_ordem",
                schema: "selecao",
                table: "fatos_coletados",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true,
                filter: "grupo_coletado_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_formularios_processo_processo_finalidade",
                schema: "selecao",
                table: "formularios_processo",
                columns: new[] { "processo_seletivo_id", "finalidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_geracoes_de_envio_ao_inep_escopo",
                schema: "selecao",
                table: "geracoes_de_envio_ao_inep",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "gerado_em" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_grupos_coletados_processo_codigo",
                schema: "selecao",
                table: "grupos_coletados",
                columns: new[] { "processo_seletivo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grupos_peso_area_enem_congelados_configuracao_classificacao",
                schema: "selecao",
                table: "grupos_peso_area_enem_congelados",
                column: "configuracao_classificacao_id");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_expires_at",
                schema: "plataforma",
                table: "idempotency_cache",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_lookup",
                schema: "plataforma",
                table: "idempotency_cache",
                columns: new[] { "scope", "endpoint", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_implantacoes_das_permissoes_sequencia",
                schema: "autorizacao",
                table: "implantacoes_das_permissoes",
                column: "sequencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                column: "cpf_indice");

            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_processo_sub",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "sub" });

            migrationBuilder.CreateIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "cpf_indice" },
                unique: true,
                filter: "situacao <> 3");

            migrationBuilder.CreateIndex(
                name: "ix_inscritos_do_lote_de_notas_inscricao",
                schema: "selecao",
                table: "inscritos_do_lote_de_notas",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_instalacao_tentativas_instalacao",
                schema: "autorizacao",
                table: "instalacao_tentativas",
                column: "instalacao_id");

            migrationBuilder.CreateIndex(
                name: "ux_instalacoes_executor_pendente",
                schema: "autorizacao",
                table: "instalacoes",
                columns: new[] { "executor_emissor", "executor_sub" },
                unique: true,
                filter: "situacao IN ('aguardando_documento', 'documento_em_verificacao', 'documento_verificado', 'aguardando_codigo')");

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
                name: "ix_itens_de_avaliacao_da_etapa_etapa_processo_id",
                schema: "selecao",
                table: "itens_de_avaliacao_da_etapa",
                column: "etapa_processo_id");

            migrationBuilder.CreateIndex(
                name: "ix_janelas_de_migracao_perfil_id_versao_alvo_id",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                columns: new[] { "perfil_id", "versao_alvo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_janelas_de_migracao_perfil_id_versao_nova_id",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                columns: new[] { "perfil_id", "versao_nova_id" });

            migrationBuilder.CreateIndex(
                name: "ux_janelas_de_migracao_aberta",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                column: "perfil_id",
                unique: true,
                filter: "encerrada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_janelas_de_migracao_ordem_de_registro",
                schema: "autorizacao",
                table: "janelas_de_migracao",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_de_nota_inscricao_etapa",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "inscricao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_de_nota_lote_id_etapa_processo_id",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "lote_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_de_nota_lote_inscricao_item",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "lote_id", "inscricao_id", "item_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leituras_do_documento_da_versao_documento",
                schema: "autorizacao",
                table: "leituras_do_documento_da_versao",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_liberacoes_da_reclassificacao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "liberacoes_da_reclassificacao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_liberacoes_da_reclassificacao_etapa",
                schema: "autorizacao",
                table: "liberacoes_da_reclassificacao",
                columns: new[] { "reclassificacao_id", "concessao_id", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_liberacoes_da_reclassificacao_ordem",
                schema: "autorizacao",
                table: "liberacoes_da_reclassificacao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_linhagem_unica_por_objeto_ato_id",
                schema: "publicacoes",
                table: "linhagem_unica_por_objeto",
                column: "ato_id");

            migrationBuilder.CreateIndex(
                name: "ix_linhagem_unica_por_objeto_raiz_id",
                schema: "publicacoes",
                table: "linhagem_unica_por_objeto",
                column: "raiz_id");

            migrationBuilder.CreateIndex(
                name: "ux_linhagem_unica_por_objeto",
                schema: "publicacoes",
                table: "linhagem_unica_por_objeto",
                columns: new[] { "entidade_tipo", "entidade_id", "tipo_codigo" },
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
                name: "ix_lotes_da_migracao_janela_id_perfil_id",
                schema: "autorizacao",
                table: "lotes_da_migracao",
                columns: new[] { "janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ux_lotes_da_migracao_gestao",
                schema: "autorizacao",
                table: "lotes_da_migracao",
                column: "janela_id",
                unique: true,
                filter: "etapa = 'gestao_de_acessos'");

            migrationBuilder.CreateIndex(
                name: "ix_lotes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "importado_em", "ordem_de_registro" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_lotes_de_notas_do_enem_versao_configuracao_id",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                column: "versao_configuracao_id");

            migrationBuilder.CreateIndex(
                name: "ux_lotes_de_notas_do_enem_ordem_de_registro",
                schema: "selecao",
                table: "lotes_de_notas_do_enem",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_migracoes_da_concessao_janela_id_perfil_id",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "janela_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ix_migracoes_da_concessao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_migracoes_da_concessao_janela_concessao",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "janela_id", "concessao_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modalidade_codigo_vivo",
                schema: "configuracao",
                table: "modalidade",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_modalidades_selecionadas_configuracao_distribuicao_vagas_id",
                schema: "selecao",
                table: "modalidades_selecionadas",
                column: "configuracao_distribuicao_vagas_id");

            migrationBuilder.CreateIndex(
                name: "ix_modelos_de_documento_processo",
                schema: "selecao",
                table: "modelos_de_documento",
                column: "processo_seletivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_modelos_formulario_codigo",
                schema: "configuracao",
                table: "modelos_formulario",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modelos_formulario_tipo_finalidade_ativo",
                schema: "configuracao",
                table: "modelos_formulario",
                columns: new[] { "tipo_processo_codigo", "finalidade", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ix_motivos_de_decisao_tipo_subclassificacao_ativo",
                schema: "selecao",
                table: "motivos_de_decisao",
                columns: new[] { "tipo", "subclassificacao", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ux_motivos_de_decisao_tipo_codigo",
                schema: "selecao",
                table: "motivos_de_decisao",
                columns: new[] { "tipo", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_motivos_do_processo_processo_origem",
                schema: "selecao",
                table: "motivos_do_processo",
                columns: new[] { "processo_seletivo_id", "motivo_de_decisao_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nos_exigencia_no_pai_id",
                schema: "selecao",
                table: "nos_exigencia",
                column: "no_pai_id");

            migrationBuilder.CreateIndex(
                name: "ix_nos_exigencia_processo_seletivo_id",
                schema: "selecao",
                table: "nos_exigencia",
                column: "processo_seletivo_id");

            migrationBuilder.CreateIndex(
                name: "ux_nos_exigencia_documento_exigido_id",
                schema: "selecao",
                table: "nos_exigencia",
                column: "documento_exigido_id",
                unique: true,
                filter: "documento_exigido_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_nos_exigencia_base_legal_no_exigencia_id",
                schema: "selecao",
                table: "nos_exigencia_base_legal",
                column: "no_exigencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_obrigatoriedade_legal_historico_regra_snapshot_at",
                schema: "selecao",
                table: "obrigatoriedade_legal_historico",
                columns: new[] { "regra_id", "snapshot_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_obrigatoriedades_legais_hash_ativos",
                schema: "selecao",
                table: "obrigatoriedades_legais",
                column: "hash",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_obrigatoriedades_legais_regra_codigo_ativos",
                schema: "selecao",
                table: "obrigatoriedades_legais",
                column: "regra_codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_ocorrencias_de_grupo_da_inscricao_membro",
                schema: "selecao",
                table: "ocorrencias_de_grupo_da_inscricao",
                columns: new[] { "inscricao_id", "grupo_codigo", "membro_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_do_lote_de_notas_inscricao_id",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_do_lote_de_notas_lote",
                schema: "selecao",
                table: "ocorrencias_do_lote_de_notas",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_oferta_curso_curso_id",
                schema: "configuracao",
                table: "oferta_curso",
                columns: new[] { "curso_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_oferta_curso_local_oferta_id",
                schema: "configuracao",
                table: "oferta_curso",
                column: "local_oferta_id");

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

            migrationBuilder.CreateIndex(
                name: "ux_opcoes_declaradas_fato_processo_fato_codigo",
                schema: "selecao",
                table: "opcoes_declaradas_fato",
                columns: new[] { "processo_seletivo_id", "fato_codigo", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedido_anexo_leituras_anexo_id",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                column: "anexo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_anexo_leituras_pedido",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_anexos_pedido",
                schema: "autorizacao",
                table: "pedido_anexos",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedido_decisoes_pedido",
                schema: "autorizacao",
                table: "pedido_decisoes",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ux_pedido_decisoes_ordem_de_registro",
                schema: "autorizacao",
                table: "pedido_decisoes",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_de_concessao_perfil_id_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "perfil_id", "versao_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_de_concessao_termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "termo_versao_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_situacao",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "situacao");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_solicitante",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "solicitante_emissor", "solicitante_sub" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('aguardando_documento', 'aguardando_beneficiario', 'aguardando_gestor', 'aguardando_protecao_de_dados')")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_perfis_governanca",
                schema: "autorizacao",
                table: "perfis",
                column: "governanca",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_perfis_nome",
                schema: "autorizacao",
                table: "perfis",
                column: "nome_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_peso_area_enem_resolucao_grupo_vivo",
                schema: "configuracao",
                table: "peso_area_enem",
                columns: new[] { "resolucao", "grupo_curso" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_precedencia_fase_par_vivo",
                schema: "configuracao",
                table: "precedencia_fase",
                columns: new[] { "antecessora_codigo", "sucessora_codigo" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_processos_seletivos_identificador_legivel",
                schema: "selecao",
                table: "processos_seletivos",
                column: "identificador_legivel",
                unique: true,
                filter: "identificador_legivel IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_etapa_ato",
                schema: "selecao",
                table: "produtos_da_etapa",
                columns: new[] { "etapa_processo_id", "ato_codigo", "papel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_produtos_da_fase_ato",
                schema: "selecao",
                table: "produtos_da_fase",
                columns: new[] { "fase_cronograma_id", "ato_codigo", "papel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_atos_ato",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                columns: new[] { "ato", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_atos_item",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                columns: new[] { "cadastro", "item_id", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_atos_item_ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                columns: new[] { "cadastro", "item_id", "ciclo", "instante" });

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_atos_ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_atos",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_protecao_do_catalogo_manifestacoes_item_ciclo",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                columns: new[] { "cadastro", "item_id", "ciclo", "instante" });

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_inicial_do_autor",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                columns: new[] { "cadastro", "item_id", "ciclo", "autor_emissor", "autor_sub" },
                unique: true,
                filter: "substitui_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_ordem_de_registro",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_protecao_do_catalogo_manifestacoes_substitui",
                schema: "configuracao",
                table: "protecao_do_catalogo_manifestacoes",
                column: "substitui_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_providencias_concessao",
                schema: "autorizacao",
                table: "providencias",
                column: "concessao_id");

            migrationBuilder.CreateIndex(
                name: "ix_providencias_estado_aberta_em_id",
                schema: "autorizacao",
                table: "providencias",
                columns: new[] { "estado", "aberta_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_providencias_concessao_aberta",
                schema: "autorizacao",
                table: "providencias",
                column: "concessao_id",
                unique: true,
                filter: "estado = 'aberta'");

            migrationBuilder.CreateIndex(
                name: "ix_rascunhos_publicacao_expira_em",
                schema: "selecao",
                table: "rascunhos_publicacao",
                column: "expira_em");

            migrationBuilder.CreateIndex(
                name: "ux_rascunhos_publicacao_processo_operador",
                schema: "selecao",
                table: "rascunhos_publicacao",
                columns: new[] { "processo_seletivo_id", "usuario_sub" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rascunhos_retificacao_processo",
                schema: "selecao",
                table: "rascunhos_retificacao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recalculos_da_aprovacao_versao_id",
                schema: "autorizacao",
                table: "recalculos_da_aprovacao",
                column: "versao_id");

            migrationBuilder.CreateIndex(
                name: "ix_reclassificacoes_da_permissao_perfil_codigo",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                columns: new[] { "perfil_id", "codigo", "ordem_na_trilha" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_reclassificacoes_da_permissao_fato",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                columns: new[] { "implantacao_id", "perfil_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_reclassificacoes_da_permissao_ordem",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurso_acessibilidade_codigo_vivo",
                schema: "configuracao",
                table: "recurso_acessibilidade",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_recurso_acessibilidade_nome_vivo",
                schema: "configuracao",
                table: "recurso_acessibilidade",
                column: "nome",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_recursos_da_etapa_etapa_processo_id",
                schema: "selecao",
                table: "recursos_da_etapa",
                column: "etapa_processo_id");

            migrationBuilder.CreateIndex(
                name: "ix_referencia_reserva_demografica_censo_vivo",
                schema: "configuracao",
                table: "referencia_reserva_demografica",
                column: "censo_referencia",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_regras_derivacao_configuradas_config_ordem",
                schema: "selecao",
                table: "regras_derivacao_configuradas",
                columns: new[] { "configuracao_derivacao_fato_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regras_eliminacao_configuracao_classificacao_id",
                schema: "selecao",
                table: "regras_eliminacao",
                column: "configuracao_classificacao_id");

            migrationBuilder.CreateIndex(
                name: "ux_regras_recurso_fase_fase_cronograma",
                schema: "selecao",
                table: "regras_recurso_fase",
                column: "fase_cronograma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_booleano",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_booleano" },
                filter: "valor_booleano IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_codigo",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_codigo" },
                filter: "valor_codigo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_data",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_data" },
                filter: "valor_data IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_fato_numero",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "fato_codigo", "valor_numero" },
                filter: "valor_numero IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_ocorrencia_id",
                schema: "selecao",
                table: "respostas_da_inscricao",
                column: "ocorrencia_id");

            migrationBuilder.CreateIndex(
                name: "ux_respostas_da_inscricao_valor",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "inscricao_id", "ocorrencia_id", "fato_codigo", "posicao" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_revisao_itens_concessao",
                schema: "autorizacao",
                table: "revisao_itens",
                column: "concessao_id");

            migrationBuilder.CreateIndex(
                name: "ux_revisao_itens_revisao_concessao",
                schema: "autorizacao",
                table: "revisao_itens",
                columns: new[] { "revisao_id", "concessao_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rol_de_fatos_candidato_codigo",
                schema: "configuracao",
                table: "rol_de_fatos_candidato",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rol_de_regras_hash",
                schema: "selecao",
                table: "rol_de_regras",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ux_rol_de_regras_codigo_versao",
                schema: "selecao",
                table: "rol_de_regras",
                columns: new[] { "codigo", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_suspensoes_do_perfil_aberta",
                schema: "autorizacao",
                table: "suspensoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "retomada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_suspensoes_do_perfil_ordem",
                schema: "autorizacao",
                table: "suspensoes_do_perfil",
                column: "ordem_na_trilha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sync_run_data_de_referencia",
                schema: "discentes",
                table: "sync_run",
                column: "data_de_referencia");

            migrationBuilder.CreateIndex(
                name: "ix_sync_run_started_at",
                schema: "discentes",
                table: "sync_run",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_termo_consentimento_nome",
                schema: "configuracao",
                table: "termo_consentimento",
                column: "nome",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_termo_consentimento_versao_termo_promovida_em",
                schema: "configuracao",
                table: "termo_consentimento_versao",
                columns: new[] { "termo_consentimento_id", "promovida_em" });

            migrationBuilder.CreateIndex(
                name: "ix_termos_de_responsabilidade_versao_vigente_id",
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                column: "versao_vigente_id");

            migrationBuilder.CreateIndex(
                name: "ux_termos_exigidos_formulario_processo_finalidade_codigo",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                columns: new[] { "processo_seletivo_id", "finalidade", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_termos_exigidos_formulario_processo_finalidade_ordem",
                schema: "selecao",
                table: "termos_exigidos_formulario",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_termos_na_instalacao_versao_do_termo_id",
                schema: "autorizacao",
                table: "termos_na_instalacao",
                column: "versao_do_termo_id");

            migrationBuilder.CreateIndex(
                name: "ux_termos_na_instalacao_instalacao",
                schema: "autorizacao",
                table: "termos_na_instalacao",
                column: "instalacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipo_ato_publicado_codigo_vivo",
                schema: "publicacoes",
                table: "tipo_ato_publicado",
                column: "codigo",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_banca_codigo_vivo",
                schema: "configuracao",
                table: "tipo_banca",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_deficiencia_codigo_vivo",
                schema: "configuracao",
                table: "tipo_deficiencia",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_deficiencia_nome_vivo",
                schema: "configuracao",
                table: "tipo_deficiencia",
                column: "nome",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_documento_categoria",
                schema: "configuracao",
                table: "tipo_documento",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "ix_tipo_documento_codigo_vivo",
                schema: "configuracao",
                table: "tipo_documento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_etapa_ativo",
                schema: "configuracao",
                table: "tipos_etapa",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_etapa_codigo",
                schema: "configuracao",
                table: "tipos_etapa",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipos_processo_ativo",
                schema: "configuracao",
                table: "tipos_processo",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_processo_codigo",
                schema: "configuracao",
                table: "tipos_processo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trocas_de_versao_do_pedido_pedido",
                schema: "autorizacao",
                table: "trocas_de_versao_do_pedido",
                column: "pedido_id");

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

            migrationBuilder.CreateIndex(
                name: "ux_vagas_ofertadas_configuracao_modalidade",
                schema: "selecao",
                table: "vagas_ofertadas",
                columns: new[] { "configuracao_distribuicao_vagas_id", "modalidade_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versoes_configuracao_processo_vigencia",
                schema: "selecao",
                table: "versoes_configuracao",
                columns: new[] { "processo_seletivo_id", "vigente_a_partir_de", "numero_versao" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_versoes_configuracao_ato_criador",
                schema: "selecao",
                table: "versoes_configuracao",
                column: "ato_criador_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_versoes_configuracao_processo_numero",
                schema: "selecao",
                table: "versoes_configuracao",
                columns: new[] { "processo_seletivo_id", "numero_versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_versoes_do_perfil_perfil_id_versao_do_documento",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                columns: new[] { "perfil_id", "versao_do_documento" });

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_perfil_numero",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                columns: new[] { "perfil_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_perfil_pendente",
                schema: "autorizacao",
                table: "versoes_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "situacao = 'pendente'");

            migrationBuilder.CreateIndex(
                name: "ix_versoes_do_termo_resumo",
                schema: "autorizacao",
                table: "versoes_do_termo",
                column: "resumo");

            migrationBuilder.CreateIndex(
                name: "ix_versoes_do_termo_termo_id",
                schema: "autorizacao",
                table: "versoes_do_termo",
                column: "termo_id");

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_termo_numero",
                schema: "autorizacao",
                table: "versoes_do_termo",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vigencias_do_perfil_perfil_id_versao_id",
                schema: "autorizacao",
                table: "vigencias_do_perfil",
                columns: new[] { "perfil_id", "versao_id" });

            migrationBuilder.CreateIndex(
                name: "ux_vigencias_do_perfil_aberta",
                schema: "autorizacao",
                table: "vigencias_do_perfil",
                column: "perfil_id",
                unique: true,
                filter: "ate IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_ato_entidade_objeto",
                schema: "publicacoes",
                table: "vinculo_ato_entidade",
                columns: new[] { "entidade_tipo", "entidade_id", "ato_id" });

            migrationBuilder.CreateIndex(
                name: "ux_vinculo_ato_entidade_trio",
                schema: "publicacoes",
                table: "vinculo_ato_entidade",
                columns: new[] { "ato_id", "entidade_tipo", "entidade_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_discente_id_discente_sigaa",
                schema: "discentes",
                table: "vinculo_discente",
                column: "id_discente_sigaa",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_aceites_do_termo_pedidos_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "pedido_id",
                principalSchema: "autorizacao",
                principalTable: "pedidos_de_concessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_aceites_do_termo_versao_do_termo_versao_do_termo_id",
                schema: "autorizacao",
                table: "aceites_do_termo",
                column: "versao_do_termo_id",
                principalSchema: "autorizacao",
                principalTable: "versoes_do_termo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_anexo_leituras_pedido_anexos_anexo_id",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                column: "anexo_id",
                principalSchema: "autorizacao",
                principalTable: "pedido_anexos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_anexo_leituras_pedido_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "pedido_anexo_leituras",
                column: "pedido_id",
                principalSchema: "autorizacao",
                principalTable: "pedidos_de_concessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_anexos_pedidos_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "pedido_anexos",
                column: "pedido_id",
                principalSchema: "autorizacao",
                principalTable: "pedidos_de_concessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedido_decisoes_pedidos_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "pedido_decisoes",
                column: "pedido_id",
                principalSchema: "autorizacao",
                principalTable: "pedidos_de_concessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_pedidos_de_concessao_versao_do_termo_termo_versao_id",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                column: "termo_versao_id",
                principalSchema: "autorizacao",
                principalTable: "versoes_do_termo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_termos_de_responsabilidade_versao_do_termo_versao_vigente_id",
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                column: "versao_vigente_id",
                principalSchema: "autorizacao",
                principalTable: "versoes_do_termo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_termos_de_responsabilidade_versao_do_termo_versao_vigente_id",
                schema: "autorizacao",
                table: "termos_de_responsabilidade");

            migrationBuilder.DropTable(
                name: "aceites_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "aceites_do_termo",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "anexo_do_ato",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "anexos_do_ato_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "areas_peso_area_enem_congeladas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "avaliacoes_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "avisos_da_reclassificacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "avisos_emitidos",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "bancas_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "base_legal_bonus_regional_municipio",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "bases_da_permissao_na_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "categoria_documento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "categorias_julgadas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "certames_divulgados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "chaves_a_limpar",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "comprovantes_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicao_atendimento_especializado",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "condicoes_gatilho",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicoes_precondicao_fato",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicoes_regra_derivacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_bonus_regional_municipio",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_divulgacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_taxa_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "convocacoes",
                schema: "ingresso");

            migrationBuilder.DropTable(
                name: "criterios_desempate",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "decisoes_da_migracao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "decisoes_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "destinos_remanejamento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "dia_nao_util",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "divergencias_da_governanca",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "documentos_exigidos_base_legal",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "documentos_matricula",
                schema: "ingresso");

            migrationBuilder.DropTable(
                name: "enderecos_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "etapas_formulario",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "eventos_da_contencao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "fase_canonica",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "fato_valor_dominio",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "geracoes_de_envio_ao_inep",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "idempotency_cache",
                schema: "plataforma");

            migrationBuilder.DropTable(
                name: "instalacao_gestores",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "instalacao_tentativas",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "instituicao",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "itens_de_avaliacao_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "lancamentos_de_nota",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "leituras_do_documento_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "liberacoes_da_reclassificacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "linhagem_unica_por_objeto",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "migracoes_da_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "modalidade",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "modalidades_selecionadas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "modelos_de_documento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "modelos_formulario",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "motivos_de_decisao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "motivos_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "nos_exigencia_base_legal",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "obrigatoriedade_legal_historico",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ocorrencias_do_lote_de_notas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "oferta_curso",
                schema: "configuracao");

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
                name: "opcoes_declaradas_fato",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "pedido_anexo_leituras",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "pedido_decisoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "perfil_permissoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "permissoes_implantadas",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "peso_area_enem_area",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "precedencia_fase",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "produtos_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "produtos_da_fase",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "protecao_do_catalogo_atos",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "protecao_do_catalogo_manifestacoes",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "providencias",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "rascunhos_publicacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "rascunhos_retificacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "recalculos_da_aprovacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "recurso_acessibilidade",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "recursos_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "referencia_reserva_demografica",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "regras_eliminacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_recurso_fase",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "respostas_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "revisao_itens",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "rol_de_regras",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "sync_run",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "termo_consentimento_versao",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "termos_exigidos_formulario",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "termos_na_instalacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "tipo_ato_publicado",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "tipo_banca",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipo_deficiencia",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipo_documento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_etapa",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_processo",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "trocas_de_versao_do_pedido",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "unidade_identificador_historico",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "vagas_ofertadas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "versao_do_perfil_permissoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "vigencias_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "vinculo_ato_entidade",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "vinculo_discente",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "atos_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "grupos_peso_area_enem_congelados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "apresentacoes_de_documento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "base_legal_bonus_regional",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "bancas_requeridas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "fatos_coletados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_derivacao_configuradas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_bonus_regional",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "chamadas",
                schema: "ingresso");

            migrationBuilder.DropTable(
                name: "lotes_da_migracao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "configuracoes_cascata_remanejamento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "calendario_dias_uteis",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "matriculas",
                schema: "ingresso");

            migrationBuilder.DropTable(
                name: "formularios_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "decisoes_da_retomada",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "desativacoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "rol_de_fatos_candidato",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "inscritos_do_lote_de_notas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "documentos_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "reclassificacoes_da_permissao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "nos_exigencia",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "obrigatoriedades_legais",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "curso",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "local_oferta",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "ofertas_atendimento_especializado",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "pedido_anexos",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "peso_area_enem",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "ocorrencias_de_grupo_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "concessoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "revisoes_de_acessos",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "termo_consentimento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "instalacoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "unidade",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "configuracoes_distribuicao_vagas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ato_normativo",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "documentos_a_publicar",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_classificacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "grupos_coletados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_derivacao_fato",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "suspensoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "lotes_de_notas_do_enem",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "implantacoes_das_permissoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "documentos_exigidos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "campus",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "pedidos_de_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "inscricoes",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "janelas_de_migracao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "versoes_configuracao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "etapas_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "fases_cronograma",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "versoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "processos_seletivos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "perfis",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "versoes_do_termo",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "termos_de_responsabilidade",
                schema: "autorizacao");

            migrationBuilder.DropSequence(
                name: "numero_inscricao_seq",
                schema: "selecao");

            migrationBuilder.DropSequence(
                name: "ordem_da_trilha_de_contencao",
                schema: "autorizacao");
        }
    }
}
