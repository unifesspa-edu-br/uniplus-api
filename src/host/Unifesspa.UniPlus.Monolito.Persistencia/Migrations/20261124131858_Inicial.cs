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
                name: "ordem_do_historico_de_acesso",
                schema: "autorizacao");

            migrationBuilder.CreateTable(
                name: "atos_da_protecao_do_catalogo",
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
                    ator_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ator_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atos_da_protecao_do_catalogo", x => x.id);
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_ajuste_com_recomendacao", "ato <> 'AJUSTE_RECOMENDADO' OR classificacao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_ato", "ato IN ('SOLICITACAO', 'APROVACAO', 'AJUSTE_RECOMENDADO', 'REPROVACAO', 'ACEITE')");
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_cadastro", "cadastro IN ('TIPO_DOCUMENTO', 'FATO')");
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_ciclo_positivo", "ciclo >= 1");
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_diante_so_na_decisao", "ato IN ('APROVACAO', 'AJUSTE_RECOMENDADO', 'REPROVACAO') OR NOT diante_de_manifestacao_desfavoravel");
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_justificativa_se_desfavoravel", "ato NOT IN ('APROVACAO', 'AJUSTE_RECOMENDADO') OR NOT diante_de_manifestacao_desfavoravel OR justificativa IS NOT NULL");
                    table.CheckConstraint("ck_atos_da_protecao_do_catalogo_reprovacao_com_justificativa", "ato <> 'REPROVACAO' OR justificativa IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "atos_normativos",
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
                    versao_do_objeto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    versao_do_objeto_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ato_retificado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_retificacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atos_normativos", x => x.id);
                    table.CheckConstraint("ck_atos_normativos_ano_positivo", "ano > 0");
                    table.CheckConstraint("ck_atos_normativos_documento_hash", "documento_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_atos_normativos_nao_autorretifica", "ato_retificado_id IS NULL OR ato_retificado_id <> id");
                    table.CheckConstraint("ck_atos_normativos_retificacao_completa", "(ato_retificado_id IS NULL AND motivo_retificacao IS NULL) OR (ato_retificado_id IS NOT NULL AND motivo_retificacao IS NOT NULL AND btrim(motivo_retificacao) <> '')");
                    table.CheckConstraint("ck_atos_normativos_versao_do_objeto_completa", "(versao_do_objeto_id IS NULL AND versao_do_objeto_hash IS NULL) OR (versao_do_objeto_id IS NOT NULL AND versao_do_objeto_hash IS NOT NULL)");
                    table.CheckConstraint("ck_atos_normativos_versao_do_objeto_hash", "versao_do_objeto_hash IS NULL OR versao_do_objeto_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_atos_normativos_versao_do_objeto_id_nao_zero", "versao_do_objeto_id IS NULL OR versao_do_objeto_id <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_atos_normativos_ato_retificado",
                        column: x => x.ato_retificado_id,
                        principalSchema: "publicacoes",
                        principalTable: "atos_normativos",
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
                name: "bases_legais_do_fada",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Código da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    tipo_instrumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Tipo do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA): LEI, DECRETO, PORTARIA, RESOLUCAO, INSTRUCAO_NORMATIVA, PARECER ou EDITAL."),
                    identificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Identificação do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, comment: "Descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bases_legais_do_fada", x => x.id);
                    table.CheckConstraint("ck_bases_legais_do_fada_tipo_instrumento", "tipo_instrumento IN ('LEI','DECRETO','PORTARIA','RESOLUCAO','INSTRUCAO_NORMATIVA','PARECER','EDITAL')");
                },
                comment: "Bases legais do Fator de Atenuação das Desigualdades de Acesso (FADA).");

            migrationBuilder.CreateTable(
                name: "calendarios_de_dias_uteis",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_dataset = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendarios_de_dias_uteis", x => x.id);
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
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campus", x => x.id);
                    table.CheckConstraint("ck_campus_endereco_cidade_coerente", "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
                    table.CheckConstraint("ck_campus_endereco_completo", "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL AND endereco_logradouro IS NULL AND endereco_numero IS NULL AND endereco_complemento IS NULL AND endereco_bairro IS NULL AND endereco_distrito IS NULL AND endereco_latitude IS NULL AND endereco_longitude IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "categorias_de_documento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias_de_documento", x => x.id);
                    table.CheckConstraint("ck_categorias_de_documento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_categorias_de_documento_ordem_nao_negativa", "ordem >= 0");
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
                    documento_publico = table.Column<string>(type: "jsonb", nullable: false),
                    atos_divulgados = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb", comment: "Os documentos de cada ato divulgado do processo — o edital de cada versão e o ato que não cria versão —, com o ato e o endereço no acervo público."),
                    divulgado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nome_ordenacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(nome)", stored: true, collation: "C"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes.")
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
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Situação da chamada. Códigos: AGENDADA, EM_ANDAMENTO, CONCLUIDA, CANCELADA."),
                    data_publicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prazo_manifestacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chamadas", x => x.id);
                    table.CheckConstraint("ck_chamadas_status", "status IN ('AGENDADA', 'EM_ANDAMENTO', 'CONCLUIDA', 'CANCELADA')");
                });

            migrationBuilder.CreateTable(
                name: "chaves_a_limpar",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "A chave do objeto; leva só o id do arquivo."),
                    limpar_apos = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "A partir de quando a chave pode ser apagada."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chaves_a_limpar", x => x.id);
                },
                comment: "Chaves do armazenamento a apagar depois de uma hora: a URL de envio recria o objeto em claro até vencer.");

            migrationBuilder.CreateTable(
                name: "chaves_de_idempotencia",
                schema: "plataforma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da chave de idempotência."),
                    escopo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Quem agiu: a conta autenticada, no formato user:{conta}. A chave só vale dentro desse escopo."),
                    rota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Operação pedida, no formato MÉTODO /modelo/da/rota (modelo da rota, não o endereço com os valores)."),
                    chave = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Chave de idempotência enviada pelo cliente no cabeçalho Idempotency-Key, sem alteração."),
                    resumo_do_corpo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo SHA-256 do corpo do pedido, em hexadecimal; serve para detectar a mesma chave usada com outro corpo."),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Situação da chave: EM_PROCESSAMENTO enquanto a operação executa; CONCLUIDA quando a resposta está guardada para repetição."),
                    situacao_http_da_resposta = table.Column<int>(type: "integer", nullable: true, comment: "Código de situação HTTP da resposta guardada; vazio enquanto a operação ainda executa."),
                    cabecalhos_da_resposta = table.Column<string>(type: "jsonb", nullable: true, comment: "Cabeçalhos da resposta guardada (Content-Type e Location), em JSON; vazio enquanto a operação executa."),
                    corpo_da_resposta_cifrado = table.Column<byte[]>(type: "bytea", nullable: true, comment: "Corpo da resposta guardado cifrado, devolvido igual quando o pedido se repete; vazio enquanto a operação executa."),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante a partir do qual a chave deixa de valer e pode ser descartada."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chaves_de_idempotencia", x => x.id);
                    table.CheckConstraint("ck_chaves_de_idempotencia_status", "status IN ('EM_PROCESSAMENTO', 'CONCLUIDA')");
                });

            migrationBuilder.CreateTable(
                name: "condicoes_de_atendimento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_de_atendimento", x => x.id);
                    table.CheckConstraint("ck_condicoes_de_atendimento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                });

            migrationBuilder.CreateTable(
                name: "cursos",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grau = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nivel_ensino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    grupo_area_enem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_cursos_grupo_area_enem. O rótulo fica em grupo_area_enem_rotulo. Nulo quando o curso não declara grupo."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    nome_ordenacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(nome)", stored: true, collation: "C"),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Rótulo do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código em grupo_area_enem. Nulo quando o curso não declara grupo."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cursos", x => x.id);
                    table.CheckConstraint("ck_cursos_grupo_area_enem", "grupo_area_enem IS NULL OR grupo_area_enem IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')");
                });

            migrationBuilder.CreateTable(
                name: "dados_do_candidato",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do dado do candidato."),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Código único do dado, como RENDA_PER_CAPITA."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome apresentado para o dado."),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, comment: "Descrição do significado e uso do dado."),
                    componente_exibicao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Apresentação sugerida: MOEDA ou SITUACAO; nula quando não definida."),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Tipo do dado: SIM_OU_NAO, OPCAO, NUMERICO, TEXTO, DATA ou ENDERECO."),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Origem do dado: CALCULADO, DECLARADO ou INTEGRACAO."),
                    quantidade_de_valores = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Quantidade de valores: VALOR_UNICO ou VARIOS_VALORES."),
                    fonte_das_opcoes = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Origem das opções: CATALOGO, OPCOES_DO_PROCESSO, MODALIDADE, MUNICIPIOS_DO_FADA, MUNICIPIOS_PROVA_PRESENCIAL, UNIDADES_FEDERATIVAS, MUNICIPIOS ou TIPOS_DE_DEFICIENCIA; nula para outros tipos."),
                    fase_de_disponibilidade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Código da fase em que o valor fica disponível."),
                    fonte_do_valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Vínculo que produz o valor, no formato MECANISMO:REFERENCIA; o mecanismo é CAMPO_FORMULARIO, ATRIBUTO_CANDIDATO, REGRA_DERIVACAO, CLASSIFICACAO, INTEGRACAO ou RESUMO_DE_GRUPO."),
                    formato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Formato do texto: LIVRE, CPF, EMAIL, TELEFONE, CEP ou NOME_PESSOA; nulo para outros tipos."),
                    escopo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Pessoa a que se refere: CANDIDATO ou MEMBRO_GRUPO."),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Classificação de proteção: PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR ou SENSIVEL."),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Finalidade informada para o tratamento do dado."),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "Hipótese legal: CONSENTIMENTO, CUMPRIMENTO_OBRIGACAO_LEGAL, EXECUCAO_POLITICAS_PUBLICAS, ESTUDOS_POR_ORGAO_DE_PESQUISA, EXECUCAO_CONTRATO, EXERCICIO_REGULAR_DE_DIREITOS, PROTECAO_DA_VIDA, TUTELA_DA_SAUDE, INTERESSE_LEGITIMO, PROTECAO_DO_CREDITO, PREVENCAO_A_FRAUDE."),
                    semeado_pelo_sistema = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o dado foi fornecido pelo sistema."),
                    protecao_situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Situação da análise: PENDENTE, SOLICITADA, APROVADA, AJUSTE_RECOMENDADO ou REPROVADA."),
                    protecao_ciclo = table.Column<int>(type: "integer", nullable: false, comment: "Número do ciclo de análise da proteção, iniciado em 1."),
                    protecao_solicitada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da solicitação de análise, quando houver."),
                    protecao_solicitada_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador de quem solicitou a análise, quando houver."),
                    protecao_decidida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da decisão, quando houver."),
                    protecao_decidida_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador de quem decidiu, quando houver."),
                    protecao_recomendada = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Classificação recomendada: PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR ou SENSIVEL; nula quando não houver."),
                    protecao_justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Justificativa da decisão, quando informada."),
                    protecao_aceita_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da aceitação, quando houver."),
                    protecao_aceita_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador de quem aceitou, quando houver."),
                    protecao_manifestada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última manifestação no ciclo, quando houver."),
                    protecao_manifestacao_desfavoravel = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se há manifestação desfavorável vigente."),
                    protecao_manifestacoes_no_ciclo = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de manifestações no ciclo atual."),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o dado está disponível para uso."),
                    regras_padrao = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb", comment: "Regras de cálculo padrão do dado, em documento JSON."),
                    dependencias = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]", comment: "Códigos dos dados necessários ao cálculo pelo sistema."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dados_do_candidato", x => x.id);
                    table.CheckConstraint("ck_dados_do_candidato_classificacao_minima_do_tipo", "(tipo NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') OR (tipo = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (tipo IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))");
                    table.CheckConstraint("ck_dados_do_candidato_classificacao_protecao", "classificacao_protecao IN ('PUBLICO', 'INTERNO', 'PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')");
                    table.CheckConstraint("ck_dados_do_candidato_componente_exibicao", "componente_exibicao IS NULL OR componente_exibicao IN ('MOEDA', 'SITUACAO')");
                    table.CheckConstraint("ck_dados_do_candidato_escopo", "escopo IN ('CANDIDATO', 'MEMBRO_GRUPO')");
                    table.CheckConstraint("ck_dados_do_candidato_fonte_das_opcoes_coerente", "(tipo = 'OPCAO' AND fonte_das_opcoes IS NOT NULL AND fonte_das_opcoes IN ('CATALOGO', 'OPCOES_DO_PROCESSO', 'MODALIDADE', 'MUNICIPIOS_DO_FADA', 'UNIDADES_FEDERATIVAS', 'MUNICIPIOS', 'TIPOS_DE_DEFICIENCIA', 'MUNICIPIOS_PROVA_PRESENCIAL')) OR (tipo <> 'OPCAO' AND fonte_das_opcoes IS NULL)");
                    table.CheckConstraint("ck_dados_do_candidato_formato_coerente", "(tipo = 'TEXTO' AND formato IS NOT NULL AND formato IN ('LIVRE', 'CPF', 'EMAIL', 'TELEFONE', 'CEP', 'NOME_PESSOA')) OR (tipo <> 'TEXTO' AND formato IS NULL)");
                    table.CheckConstraint("ck_dados_do_candidato_hipotese_legal", "hipotese_legal IN ('CONSENTIMENTO', 'CUMPRIMENTO_OBRIGACAO_LEGAL', 'EXECUCAO_POLITICAS_PUBLICAS', 'ESTUDOS_POR_ORGAO_DE_PESQUISA', 'EXECUCAO_CONTRATO', 'EXERCICIO_REGULAR_DE_DIREITOS', 'PROTECAO_DA_VIDA', 'TUTELA_DA_SAUDE', 'INTERESSE_LEGITIMO', 'PROTECAO_DO_CREDITO', 'PREVENCAO_A_FRAUDE')");
                    table.CheckConstraint("ck_dados_do_candidato_mecanismo_do_vinculo", "split_part(fonte_do_valor, ':', 1) IN ('CAMPO_FORMULARIO', 'ATRIBUTO_CANDIDATO', 'REGRA_DERIVACAO', 'CLASSIFICACAO', 'INTEGRACAO', 'RESUMO_DE_GRUPO')");
                    table.CheckConstraint("ck_dados_do_candidato_origem", "origem IN ('CALCULADO', 'DECLARADO', 'INTEGRACAO')");
                    table.CheckConstraint("ck_dados_do_candidato_protecao_ajuste_com_recomendacao", "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_dados_do_candidato_protecao_ciclo_positivo", "protecao_ciclo >= 1");
                    table.CheckConstraint("ck_dados_do_candidato_protecao_desfavoravel_manifestada", "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");
                    table.CheckConstraint("ck_dados_do_candidato_protecao_manifestacoes_contadas", "protecao_manifestacoes_no_ciclo >= 0 AND (protecao_manifestacoes_no_ciclo = 0) = (protecao_manifestada_em IS NULL)");
                    table.CheckConstraint("ck_dados_do_candidato_protecao_reprovada_com_justificativa", "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");
                    table.CheckConstraint("ck_dados_do_candidato_protecao_situacao", "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'AJUSTE_RECOMENDADO', 'REPROVADA')");
                    table.CheckConstraint("ck_dados_do_candidato_quantidade_de_valores", "quantidade_de_valores IN ('VALOR_UNICO', 'VARIOS_VALORES')");
                    table.CheckConstraint("ck_dados_do_candidato_tipo", "tipo IN ('SIM_OU_NAO', 'OPCAO', 'NUMERICO', 'TEXTO', 'DATA', 'ENDERECO')");
                },
                comment: "Catálogo de dados solicitados ou calculados para candidatos e suas regras de uso.");

            migrationBuilder.CreateTable(
                name: "documentos_a_publicar",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false, comment: "Finalidade do documento. Códigos: DOCUMENTO_DO_EDITAL, ANEXO_DO_EDITAL, DOCUMENTO_DE_ATO."),
                    nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    formato = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false, comment: "Formato do documento. Códigos: DOCX, ODT, PDF."),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "Situação do envio. Códigos: PENDENTE, CONFIRMADO."),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    object_key_confirmado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_a_publicar", x => x.id);
                    table.CheckConstraint("ck_documentos_a_publicar_finalidade", "finalidade IN ('DOCUMENTO_DO_EDITAL', 'ANEXO_DO_EDITAL', 'DOCUMENTO_DE_ATO')");
                    table.CheckConstraint("ck_documentos_a_publicar_formato", "formato IN ('DOCX', 'ODT', 'PDF')");
                    table.CheckConstraint("ck_documentos_a_publicar_status", "status IN ('PENDENTE', 'CONFIRMADO')");
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
                    conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "A conta do token de quem iniciou a inscrição — nunca troca de dono."),
                    emissor_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "O emissor (issuer) do token de quem iniciou a inscrição."),
                    situacao = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, comment: "Situação da inscrição."),
                    versao_formulario_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A versão do formulário da inscrição (UNI-REQ-0084): a divulgada que o rascunho adotou por último; o primeiro envio a congela."),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    oferta_curso_segunda_opcao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_inscricao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Gerado na primeira submissão (sequência do banco, formatada); nulo em rascunho."),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "O instante em que o candidato cancelou a inscrição enviada; nulo se não foi cancelada."),
                    cancelada_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "A conta do token de quem cancelou; nulo se não foi cancelada."),
                    motivo_do_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "O motivo do cancelamento; 'a pedido do candidato' quando o candidato não o disse. Nulo se não foi cancelada."),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscricoes", x => x.id);
                    table.CheckConstraint("ck_inscricoes_situacao", "situacao IN ('RASCUNHO', 'SUBMETIDA', 'CANCELADA')");
                },
                comment: "A inscrição de um candidato num processo seletivo — uma por CPF e processo. As respostas ficam em respostas_da_inscricao e nas tabelas vizinhas.");

            migrationBuilder.CreateTable(
                name: "instalacoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    executor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    executor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instalacoes", x => x.id);
                    table.CheckConstraint("ck_instalacoes_codigo_completo", "(codigo_enviado_em IS NULL) = (codigo_expira_em IS NULL)");
                    table.CheckConstraint("ck_instalacoes_tentativas", "tentativas >= 0 AND tentativas <= 5");
                });

            migrationBuilder.CreateTable(
                name: "manifestacoes_da_protecao",
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
                    autor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cadastro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manifestacoes_da_protecao", x => x.id);
                    table.CheckConstraint("ck_manifestacoes_da_protecao_cadastro", "cadastro IN ('TIPO_DOCUMENTO', 'FATO')");
                    table.CheckConstraint("ck_manifestacoes_da_protecao_ciclo_positivo", "ciclo >= 1");
                    table.CheckConstraint("ck_manifestacoes_da_protecao_parecer", "parecer IN ('ADEQUADA', 'RECOMENDA_AJUSTE', 'INADEQUADA')");
                    table.CheckConstraint("ck_manifestacoes_da_protecao_substitui_outra", "substitui_id IS NULL OR substitui_id <> id");
                    table.CheckConstraint("ck_manifestacoes_da_protecao_texto_no_desfavoravel", "parecer = 'ADEQUADA' OR texto IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_manifestacoes_da_protecao_substitui",
                        column: x => x.substitui_id,
                        principalSchema: "configuracao",
                        principalTable: "manifestacoes_da_protecao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "matriculas",
                schema: "ingresso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    convocacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Situação da matrícula. Códigos: PENDENTE, DOCUMENTACAO_ENVIADA, EM_ANALISE, DEFERIDA, INDEFERIDA, EFETIVADA, CANCELADA."),
                    codigo_curso = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matriculas", x => x.id);
                    table.CheckConstraint("ck_matriculas_status", "status IN ('PENDENTE', 'DOCUMENTACAO_ENVIADA', 'EM_ANALISE', 'DEFERIDA', 'INDEFERIDA', 'EFETIVADA', 'CANCELADA')");
                });

            migrationBuilder.CreateTable(
                name: "modalidades",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da modalidade."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código que identifica a modalidade. Exemplos: AC, AC_PCD."),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, comment: "Descrição da modalidade para apresentação."),
                    natureza_legal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Natureza legal da modalidade. Códigos: COTA_RESERVADA, AMPLA, ACAO_AFIRMATIVA."),
                    composicao_vagas = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Forma de composição das vagas. Códigos: RESTANTE_DAS_VAGAS_OFERTADAS, DENTRO_DA_RESERVA_LEGAL, RETIRADA_DE_OUTRA_MODALIDADE, SUPLEMENTAR_AO_TOTAL."),
                    composicao_origem = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade de origem das vagas, quando retiradas de outra modalidade."),
                    regra_remanejamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Regra aplicada ao remanejamento das vagas, quando definida. Códigos: SEGUE_ORDEM_DE_REMANEJAMENTO, DESTINO_UNICO, CRUZADO."),
                    remanejamento_destino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade que recebe as vagas no destino único."),
                    remanejamento_par = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade que participa do remanejamento cruzado."),
                    remanejamento_destino_final = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade que recebe as vagas restantes ao fim do remanejamento cruzado."),
                    regra_de_aplicabilidade = table.Column<string>(type: "jsonb", nullable: false, comment: "Condições que determinam a aplicação da modalidade, em JSON; lista vazia indica aplicação incondicional."),
                    acao_quando_indeferido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Ação aplicada quando a candidatura é indeferida, quando definida. Códigos: RECLASSIFICA_AC, RECLASSIFICA_CONFORME_EDITAL."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Fundamento legal da modalidade, quando informado."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modalidades", x => x.id);
                    table.CheckConstraint("ck_modalidades_acao_quando_indeferido", "acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ('RECLASSIFICA_AC', 'RECLASSIFICA_CONFORME_EDITAL')");
                    table.CheckConstraint("ck_modalidades_codigo_formato", "codigo ~ '^[A-Z0-9_]+$'");
                    table.CheckConstraint("ck_modalidades_composicao_vagas", "composicao_vagas IN ('RESTANTE_DAS_VAGAS_OFERTADAS', 'DENTRO_DA_RESERVA_LEGAL', 'RETIRADA_DE_OUTRA_MODALIDADE', 'SUPLEMENTAR_AO_TOTAL')");
                    table.CheckConstraint("ck_modalidades_natureza_legal", "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'ACAO_AFIRMATIVA')");
                    table.CheckConstraint("ck_modalidades_regra_remanejamento", "regra_remanejamento IS NULL OR regra_remanejamento IN ('SEGUE_ORDEM_DE_REMANEJAMENTO', 'DESTINO_UNICO', 'CRUZADO')");
                    table.CheckConstraint("ck_modalidades_remanejamento_cruzado", "regra_remanejamento IS DISTINCT FROM 'CRUZADO' OR (remanejamento_par IS NOT NULL AND remanejamento_destino IS NULL)");
                    table.CheckConstraint("ck_modalidades_remanejamento_destino_unico", "regra_remanejamento IS DISTINCT FROM 'DESTINO_UNICO' OR (remanejamento_destino IS NOT NULL AND remanejamento_par IS NULL AND remanejamento_destino_final IS NULL)");
                    table.CheckConstraint("ck_modalidades_remanejamento_sem_parametros", "(regra_remanejamento IS NOT NULL AND regra_remanejamento IN ('DESTINO_UNICO', 'CRUZADO')) OR (remanejamento_destino IS NULL AND remanejamento_par IS NULL AND remanejamento_destino_final IS NULL)");
                    table.CheckConstraint("ck_modalidades_retirada_de_outra_modalidade_origem", "(composicao_vagas = 'RETIRADA_DE_OUTRA_MODALIDADE') = (composicao_origem IS NOT NULL)");
                },
                comment: "Modalidades de concorrência e suas regras de composição e remanejamento de vagas.");

            migrationBuilder.CreateTable(
                name: "modelos_formulario",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do modelo de formulário."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código que identifica o modelo de formulário."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do modelo de formulário."),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Descrição do modelo de formulário."),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Finalidade do formulário: INSCRICAO, ISENCAO_TAXA ou HABILITACAO."),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Código do tipo de processo ao qual o modelo se aplica."),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false, comment: "Conteúdo do modelo de formulário, com etapas, itens, termos e pressupostos."),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o modelo está disponível para uso."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos_formulario", x => x.id);
                    table.CheckConstraint("ck_modelos_formulario_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                },
                comment: "Modelos de formulário e seu conteúdo para processos seletivos.");

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
                    sentido = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_de_decisao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "motivos_de_decisao_do_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da escolha do motivo no processo."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo que escolheu o motivo."),
                    motivo_de_decisao_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do motivo no cadastro de origem."),
                    tipo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Tipo de decisão: ISENCAO_DA_TAXA, HOMOLOGACAO_DAS_INSCRICOES, ANALISE_DOCUMENTAL, HETEROIDENTIFICACAO, AVALIACAO_BIOPSICOSSOCIAL, CLASSIFICACAO, GABARITO_DA_PROVA ou NOTA_DA_ETAPA."),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Código do motivo copiado do cadastro na escolha."),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Descrição do motivo copiada do cadastro na escolha."),
                    subclassificacao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Subclassificação do motivo copiada do cadastro, quando houver."),
                    sentido = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, comment: "Sentido da decisão associado ao motivo: FAVORAVEL ou DESFAVORAVEL."),
                    tipo_fase_origem_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da fase à qual a escolha se aplica, quando a decisão é de uma fase."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da etapa à qual a escolha se aplica, quando a decisão é de uma etapa."),
                    versao_do_edital_de_entrada = table.Column<int>(type: "integer", nullable: false, comment: "Número da primeira versão do edital em que a escolha vale."),
                    versao_do_edital_de_saida = table.Column<int>(type: "integer", nullable: true, comment: "Número da versão do edital a partir da qual a escolha deixa de valer; vazio enquanto vigente."),
                    justificativa_da_inclusao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Justificativa da inclusão após a publicação, vazia para escolhas anteriores."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_motivos_de_decisao_do_processo", x => x.id);
                    table.CheckConstraint("ck_motivos_de_decisao_do_processo_lugar", "num_nonnulls(tipo_fase_origem_id, etapa_processo_id) = 1");
                    table.CheckConstraint("ck_motivos_de_decisao_do_processo_vigencia", "versao_do_edital_de_entrada >= 1 AND (versao_do_edital_de_saida IS NULL OR versao_do_edital_de_saida > versao_do_edital_de_entrada)");
                },
                comment: "Motivos escolhidos para decisões do processo seletivo, com os dados do cadastro no momento da escolha e sua vigência no edital.");

            migrationBuilder.CreateTable(
                name: "obrigatoriedades_legais",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Código do tipo de processo ao qual a regra se aplica."),
                    categoria = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Categoria da regra: etapa, modalidade, desempate, documento, fada, atendimento ou outros."),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código que identifica a regra legal."),
                    condicoes = table.Column<string>(type: "jsonb", nullable: false, comment: "Condições de aplicação da regra legal, em JSON."),
                    descricao_humana = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, comment: "Descrição da regra para leitura por pessoas."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Fundamento legal da regra."),
                    ato_normativo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Endereço do ato normativo que fundamenta a regra, quando houver."),
                    portaria_interna_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Código da portaria interna correspondente, quando houver."),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false, comment: "Data inicial de vigência da regra."),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true, comment: "Data final de vigência da regra, quando houver."),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo usado para identificar o conteúdo da regra."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obrigatoriedades_legais", x => x.id);
                    table.CheckConstraint("ck_obrigatoriedades_legais_categoria", "categoria IN ('ETAPA', 'MODALIDADE', 'DESEMPATE', 'DOCUMENTO', 'FADA', 'ATENDIMENTO', 'OUTROS')");
                },
                comment: "Regras legais aplicáveis à configuração de processos seletivos.");

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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pesos_por_area_do_enem",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolucao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    grupo_curso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), sem abreviação e sem acento, restrito aos quatro grupos pelo CHECK ck_pesos_por_area_do_enem_grupo_curso; com a resolução, forma a chave de negócio da linha. O rótulo fica em grupo_curso_rotulo."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    grupo_curso_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Rótulo do grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código em grupo_curso."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pesos_por_area_do_enem", x => x.id);
                    table.CheckConstraint("ck_pesos_por_area_do_enem_grupo_curso", "grupo_curso IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')");
                });

            migrationBuilder.CreateTable(
                name: "precedencias_entre_fases",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    antecessora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sucessora_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    permite_sobreposicao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precedencias_entre_fases", x => x.id);
                    table.CheckConstraint("ck_precedencias_entre_fases_antecessora_canonica", "antecessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO_INSCRICOES', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_precedencias_entre_fases_antecessora_formato", "antecessora_codigo ~ '^[A-Z_]+$'");
                    table.CheckConstraint("ck_precedencias_entre_fases_sem_self_loop", "antecessora_codigo <> sucessora_codigo");
                    table.CheckConstraint("ck_precedencias_entre_fases_sucessora_canonica", "sucessora_codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO_INSCRICOES', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_precedencias_entre_fases_sucessora_formato", "sucessora_codigo ~ '^[A-Z_]+$'");
                });

            migrationBuilder.CreateTable(
                name: "processos_seletivos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo."),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Nome do processo seletivo informado no cadastro."),
                    identificador_legivel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Identificador legível escolhido no cadastro; dele derivam o endereço público do certame e a chave no acervo. Ausência = ainda não declarado."),
                    tipo_processo_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do tipo de processo no cadastro de origem, copiado na criação."),
                    tipo_processo_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Código do tipo de processo copiado do cadastro na criação."),
                    tipo_processo_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de processo copiado do cadastro na criação."),
                    status = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, comment: "Situação do processo: rascunho, publicado, encerrado ou cancelado."),
                    origem_candidatos = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Como os candidatos chegam ao processo: INSCRICAO_PROPRIA ou IMPORTACAO_EXTERNA."),
                    unidade_administradora_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da unidade administradora no cadastro de origem, copiado na criação e mantido sem alteração."),
                    unidade_administradora_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Cópia da sigla da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Cópia do código da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Cópia do identificador usado no endereço da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, comment: "Cópia do nome da Unidade administradora no momento da criação — não reflete edições posteriores no cadastro de origem."),
                    unidade_administradora_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Tipo da unidade administradora copiado na criação: Reitoria, ProReitoria, Centro, Instituto, Faculdade, Departamento, Coordenacao, Diretoria, Divisao, Nucleo ou Outro."),
                    unidade_administradora_cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true, comment: "Cópia do código IBGE da cidade da Unidade administradora no momento da criação, quando a unidade a informa."),
                    unidade_administradora_cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true, comment: "Cópia do nome de exibição da cidade da Unidade administradora no momento da criação."),
                    unidade_administradora_cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, comment: "Cópia da UF da cidade da Unidade administradora no momento da criação."),
                    localidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false, comment: "Código IBGE do município cujo calendário rege a contagem dos prazos — o único valor normativo da localidade."),
                    localidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Nome do município da localidade regente — guardado para exibição, não entra em cálculo de prazo."),
                    localidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false, comment: "UF da localidade regente — guardado para exibição; a UF que vale é a derivada do prefixo do código."),
                    algoritmo_contagem_prazo_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Código da entrada de algoritmo de contagem do catálogo de regras que o certame declarou."),
                    algoritmo_contagem_prazo_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "Versão da entrada declarada — evolução da convenção é versão nova, nunca alteração da vigente."),
                    algoritmo_contagem_prazo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true, comment: "Resumo da definição resolvida no catálogo de regras — é o que prova que a convenção aplicada não mudou depois."),
                    referencia_temporal_dados_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Referência para apurar os fatos: FIM_INSCRICAO, INICIO_FASE, FIM_FASE ou DATA_ESPECIFICA. Ausente quando não declarada."),
                    referencia_temporal_dados_data = table.Column<DateOnly>(type: "date", nullable: true, comment: "Data escolhida quando a referência para apurar os fatos é uma data específica."),
                    referencia_temporal_dados_fase_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da fase escolhida quando a referência para apurar os fatos é o início ou o fim de uma fase."),
                    tem_prova_presencial = table.Column<bool>(type: "boolean", nullable: true, comment: "Declara se o processo tem prova presencial. Nulo = ainda não declarado."),
                    aplica_fada = table.Column<bool>(type: "boolean", nullable: true, comment: "Indica se o processo aplica o Fator de Atenuação das Desigualdades de Acesso (FADA); ausente quando ainda não declarado."),
                    numero_de_opcoes_de_curso = table.Column<int>(type: "integer", nullable: true, comment: "Quantos cursos o candidato escolhe na inscrição (1 ou 2). Nulo = ainda não declarado."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processos_seletivos", x => x.id);
                    table.CheckConstraint("ck_processos_seletivos_numero_de_opcoes_de_curso", "numero_de_opcoes_de_curso IS NULL OR numero_de_opcoes_de_curso IN (1, 2)");
                    table.CheckConstraint("ck_processos_seletivos_origem_candidatos", "origem_candidatos IN ('INSCRICAO_PROPRIA', 'IMPORTACAO_EXTERNA')");
                    table.CheckConstraint("ck_processos_seletivos_unidade_administradora_cidade_completa", "(unidade_administradora_cidade_codigo_ibge IS NULL AND unidade_administradora_cidade_nome IS NULL AND unidade_administradora_cidade_uf IS NULL) OR (unidade_administradora_cidade_codigo_ibge IS NOT NULL AND unidade_administradora_cidade_nome IS NOT NULL AND unidade_administradora_cidade_uf IS NOT NULL)");
                },
                comment: "Processos seletivos e suas configurações declaradas para a seleção de candidatos.");

            migrationBuilder.CreateTable(
                name: "rascunhos_processos_seletivos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    unidade_administradora_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id da Unidade administradora em Organização Institucional (ADR-0061, sem FK cross-schema): decide quem lê, grava, exclui e publica o rascunho."),
                    documento = table.Column<string>(type: "jsonb", nullable: false, comment: "Documento da configuração. Traz o identificador da conta de quem assina (dado pessoal pseudonimizado); o formato não prevê nome, CPF nem e-mail, mas o texto livre de campos como o dos assinantes é guardado como veio."),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rascunhos_processos_seletivos", x => x.id);
                    table.CheckConstraint("ck_rascunhos_processos_seletivos_documento_objeto", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_rascunhos_processos_seletivos_revisao", "revisao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "recursos_de_acessibilidade",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recursos_de_acessibilidade", x => x.id);
                    table.CheckConstraint("ck_recursos_de_acessibilidade_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                });

            migrationBuilder.CreateTable(
                name: "referencias_demograficas",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    censo_referencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ppi_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    quilombola_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    pcd_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referencias_demograficas", x => x.id);
                    table.CheckConstraint("ck_referencias_demograficas_pcd_percentual", "pcd_percentual >= 0 AND pcd_percentual <= 100");
                    table.CheckConstraint("ck_referencias_demograficas_ppi_percentual", "ppi_percentual >= 0 AND ppi_percentual <= 100");
                    table.CheckConstraint("ck_referencias_demograficas_quilombola_percentual", "quilombola_percentual >= 0 AND quilombola_percentual <= 100");
                });

            migrationBuilder.CreateTable(
                name: "regras_do_catalogo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tipo = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, comment: "Tipo da regra no catálogo."),
                    declaracao_da_regra = table.Column<string>(type: "jsonb", nullable: false),
                    invariantes = table.Column<string>(type: "jsonb", nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_do_catalogo", x => x.id);
                    table.CheckConstraint("ck_regras_do_catalogo_tipo", "tipo IN ('REGRA_CALCULO', 'REGRA_ARREDONDAMENTO', 'REGRA_ELIMINACAO', 'REGRA_FADA', 'CRITERIO_DESEMPATE', 'CRITERIO_REMANEJAMENTO', 'REGRA_ORDEM_ALOCACAO', 'REGRA_ELEGIBILIDADE', 'REGRA_DISTRIBUICAO_VAGAS', 'REGRA_AJUSTE_DISTRIBUICAO_VAGAS', 'REGRA_PRAZO_RECURSO', 'CONVENCAO_CONTAGEM_PRAZO', 'REGRA_APURACAO_AVALIACAO', 'REGRA_PROCEDIMENTO_RECURSO')");
                });

            migrationBuilder.CreateTable(
                name: "revisoes_de_acessos",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aberta_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aberta_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aberta_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisoes_de_acessos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sincronizacoes_com_o_sigaa",
                schema: "discentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) da execução."),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Situação da execução. Códigos: EM_ANDAMENTO, CONCLUIDA, PARCIAL, FALHOU."),
                    data_de_referencia = table.Column<DateOnly>(type: "date", nullable: false, comment: "Dia a que a execução se refere — não o instante em que rodou. Uma execução disparada de madrugada refere-se ao dia que começou."),
                    total_items = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade total de itens previstos para esta execução."),
                    processed_items = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens já processados."),
                    success_count = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens processados com sucesso."),
                    error_count = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de itens que falharam durante o processamento."),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Instante de início da execução."),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Instante de conclusão da execução — nulo enquanto o status é EM_ANDAMENTO."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sincronizacoes_com_o_sigaa", x => x.id);
                    table.CheckConstraint("ck_sincronizacoes_com_o_sigaa_status", "status IN ('EM_ANDAMENTO', 'CONCLUIDA', 'PARCIAL', 'FALHOU')");
                },
                comment: "Controle de execução das rotinas de sincronização de dados discentes com o SIGAA — uma linha por execução.");

            migrationBuilder.CreateTable(
                name: "termos_de_consentimento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    texto_rascunho = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    base_legal_rascunho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    forma_aceite_rascunho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    revisado = table.Column<bool>(type: "boolean", nullable: false),
                    revisado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revisado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_de_consentimento", x => x.id);
                    table.CheckConstraint("ck_termos_de_consentimento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,99}$'");
                    table.CheckConstraint("ck_termos_de_consentimento_forma_aceite_rascunho", "forma_aceite_rascunho IN ('A_DEFINIR', 'REGISTRO_SEM_ENDERECO_IP', 'REGISTRO_COM_ENDERECO_IP')");
                });

            migrationBuilder.CreateTable(
                name: "tipos_conceito",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do tipo de conceito (UUIDv7)."),
                    codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Chave natural do tipo, em NFC e imutável; único entre todos os tipos, inclusive os desativados."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de conceito, em NFC."),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Descrição livre do tipo, em NFC. Nula quando não informada."),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, comment: "Falso quando o tipo foi desativado: deixa de ser oferecido, e o código continua reservado."),
                    forma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Forma do conceito: NUMERICO, medido na escala desta linha, ou POR_OPCOES, escolhido entre os valores de opcoes_do_tipo_de_conceito."),
                    escala_minima = table.Column<decimal>(type: "numeric", nullable: true, comment: "Menor valor da escala. Só no tipo numérico."),
                    escala_maxima = table.Column<decimal>(type: "numeric", nullable: true, comment: "Maior valor da escala, sempre acima do menor. Só no tipo numérico."),
                    casas_decimais = table.Column<int>(type: "integer", nullable: true, comment: "Número de casas decimais da escala, de 0 a 4. Só no tipo numérico."),
                    created_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_conceito", x => x.id);
                    table.CheckConstraint("ck_tipos_conceito_forma", "forma IN ('NUMERICO', 'POR_OPCOES')");
                    table.CheckConstraint("ck_tipos_conceito_parametros_da_forma", "(forma = 'NUMERICO' AND escala_minima IS NOT NULL AND escala_maxima IS NOT NULL AND casas_decimais IS NOT NULL AND escala_minima < escala_maxima AND casas_decimais BETWEEN 0 AND 4) OR (forma = 'POR_OPCOES' AND escala_minima IS NULL AND escala_maxima IS NULL AND casas_decimais IS NULL)");
                },
                comment: "Tipos de conceito com que uma etapa avalia: numéricos, medidos numa escala, ou categóricos, escolhidos entre valores ordenados.");

            migrationBuilder.CreateTable(
                name: "tipos_de_ato_publicado",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    congela_configuracao = table.Column<bool>(type: "boolean", nullable: false),
                    unico_por_objeto = table.Column<bool>(type: "boolean", nullable: false),
                    efeito_irreversivel = table.Column<bool>(type: "boolean", nullable: false),
                    determina_resultado = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o ato deste tipo determina a situação do candidato (é um resultado)."),
                    publicavel_como_avulso = table.Column<bool>(type: "boolean", nullable: false),
                    exige_ato_corrigido = table.Column<bool>(type: "boolean", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_ato_publicado", x => x.id);
                    table.CheckConstraint("ck_tipos_de_ato_publicado_codigo_formato", "codigo ~ '^[A-Z]+(_[A-Z]+)*$'");
                    table.CheckConstraint("ck_tipos_de_ato_publicado_vigencia", "vigencia_fim IS NULL OR vigencia_fim > vigencia_inicio");
                });

            migrationBuilder.CreateTable(
                name: "tipos_de_banca",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fase_tipica = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    papel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    exige_duas_ou_mais_bancas = table.Column<bool>(type: "boolean", nullable: false),
                    encerra_recurso_na_primeira_instancia = table.Column<bool>(type: "boolean", nullable: false),
                    tipos_de_decisao_julgados = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]", comment: "Tipos de decisão que o tipo de banca julga, em ordem alfabética ordinal do código e sem repetição; só o papel que avalia o mérito os declara."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_banca", x => x.id);
                    table.CheckConstraint("ck_tipos_de_banca_codigo_canonico", "codigo IN ('BANCA_ANALISE_DOCUMENTAL', 'BANCA_ENTREVISTA', 'BANCA_CORRECAO_REDACOES', 'BANCA_ANALISE_RECURSOS', 'BANCA_HETEROIDENTIFICACAO', 'BANCA_BIOPSICOSSOCIAL', 'BANCA_HOMOLOGACAO_INSCRICOES', 'BANCA_IMPORTACAO_NOTAS_INEP', 'BANCA_IMPORTACAO_CANDIDATOS', 'BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA')");
                    table.CheckConstraint("ck_tipos_de_banca_decisoes", "tipos_de_decisao_julgados <@ ARRAY['ISENCAO_DA_TAXA', 'HOMOLOGACAO_DAS_INSCRICOES', 'ANALISE_DOCUMENTAL', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'CLASSIFICACAO', 'GABARITO_DA_PROVA', 'NOTA_DA_ETAPA']::text[]");
                    table.CheckConstraint("ck_tipos_de_banca_papel", "papel IN ('AVALIA_O_MERITO', 'JULGA_A_PRIMEIRA_INSTANCIA', 'IMPORTA_AS_NOTAS_DO_INEP', 'IMPORTA_CANDIDATOS', 'JULGA_A_SEGUNDA_INSTANCIA')");
                });

            migrationBuilder.CreateTable(
                name: "tipos_de_deficiencia",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    permanente = table.Column<bool>(type: "boolean", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_deficiencia", x => x.id);
                    table.CheckConstraint("ck_tipos_de_deficiencia_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                });

            migrationBuilder.CreateTable(
                name: "tipos_de_documento",
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
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_documento", x => x.id);
                    table.CheckConstraint("ck_tipos_de_documento_categoria_formato", "categoria ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_tipos_de_documento_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_tipos_de_documento_equivalente_diferente_codigo", "tipo_equivalente IS NULL OR tipo_equivalente <> codigo");
                    table.CheckConstraint("ck_tipos_de_documento_protecao_ajuste_com_recomendacao", "protecao_situacao <> 'AJUSTE_RECOMENDADO' OR protecao_recomendada IS NOT NULL");
                    table.CheckConstraint("ck_tipos_de_documento_protecao_ciclo_positivo", "protecao_ciclo >= 1");
                    table.CheckConstraint("ck_tipos_de_documento_protecao_desfavoravel_manifestada", "NOT protecao_manifestacao_desfavoravel OR protecao_manifestada_em IS NOT NULL");
                    table.CheckConstraint("ck_tipos_de_documento_protecao_manifestacoes_contadas", "protecao_manifestacoes_no_ciclo >= 0 AND (protecao_manifestacoes_no_ciclo = 0) = (protecao_manifestada_em IS NULL)");
                    table.CheckConstraint("ck_tipos_de_documento_protecao_reprovada_com_justificativa", "protecao_situacao <> 'REPROVADA' OR protecao_justificativa IS NOT NULL");
                    table.CheckConstraint("ck_tipos_de_documento_protecao_situacao", "protecao_situacao IN ('PENDENTE', 'SOLICITADA', 'APROVADA', 'AJUSTE_RECOMENDADO', 'REPROVADA')");
                    table.CheckConstraint("ck_tipos_de_documento_tamanho_maximo_mb_positivo", "tamanho_maximo_mb IS NULL OR tamanho_maximo_mb > 0");
                });

            migrationBuilder.CreateTable(
                name: "tipos_de_fase",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do tipo de fase."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código único do tipo de fase."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de fase."),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, comment: "Descrição do propósito da fase, quando informada."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Fundamento legal da fase, quando informado."),
                    agrupa_etapas = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indica se a fase agrupa etapas de avaliação."),
                    permite_complementacao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indica se a fase permite complementação."),
                    coleta_inscricao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indica se a fase coleta inscrições."),
                    coleta_solicitacao_isencao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indica se a fase coleta solicitações de isenção."),
                    origem_data = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Origem das datas: NO_EDITAL ou NA_EXECUCAO."),
                    unidade_sugerida_origem_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da unidade sugerida, quando houver."),
                    unidade_sugerida_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Sigla da unidade sugerida, quando houver."),
                    unidade_sugerida_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true, comment: "Nome da unidade sugerida, quando houver."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_fase", x => x.id);
                    table.CheckConstraint("ck_tipos_de_fase_agrupa_etapas", "agrupa_etapas = false OR codigo = 'AVALIACAO'");
                    table.CheckConstraint("ck_tipos_de_fase_codigo_canonico", "codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO_INSCRICOES', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')");
                    table.CheckConstraint("ck_tipos_de_fase_codigo_formato", "codigo ~ '^[A-Z_]+$'");
                    table.CheckConstraint("ck_tipos_de_fase_complementacao", "permite_complementacao = false OR codigo IN ('HOMOLOGACAO_INSCRICOES')");
                    table.CheckConstraint("ck_tipos_de_fase_inscricao_e_isencao_exclusivas", "NOT (coleta_inscricao AND coleta_solicitacao_isencao)");
                    table.CheckConstraint("ck_tipos_de_fase_origem_data", "origem_data IN ('NO_EDITAL', 'NA_EXECUCAO')");
                    table.CheckConstraint("ck_tipos_de_fase_solicitacao_isencao", "coleta_solicitacao_isencao = (codigo = 'SOLICITACAO_ISENCAO')");
                },
                comment: "Tipos de fase disponíveis para organizar o cronograma de seleção.");

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
                    exige_correcao_independente = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_etapa", x => x.id);
                    table.CheckConstraint("ck_tipos_etapa_carater_admitido", "admite_pontuacao OR admite_eliminacao");
                    table.CheckConstraint("ck_tipos_etapa_correcao_independente_sem_nota_do_enem", "NOT (exige_correcao_independente AND nota_de_origem_no_enem)");
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
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_processo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidades",
                schema: "organizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    apelido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Nome pelo qual a unidade também é conhecida; opcional e pode se repetir entre unidades."),
                    identificador_no_endereco_web = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Texto que identifica a unidade no endereço de página da web; único entre as unidades ativas."),
                    sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unidade_superior_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Tipo da unidade na estrutura da universidade. Códigos: REITORIA, PRO_REITORIA, CENTRO, INSTITUTO, FACULDADE, DEPARTAMENTO, COORDENACAO, DIRETORIA, DIVISAO, NUCLEO, OUTRO."),
                    unidade_academica = table.Column<bool>(type: "boolean", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    cidade_codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: true, comment: "Código IBGE (7 dígitos) da cidade da Unidade — referência ao Geo, sem FK cross-banco; opcional all-or-nothing com nome/UF."),
                    cidade_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true, comment: "Nome de exibição da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização."),
                    cidade_uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, comment: "UF da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    apelido_para_busca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, computedColumnSql: "plataforma.normalizar_para_comparacao(apelido)", stored: true, comment: "Forma normalizada de apelido (sem acento e em minúsculas, pela função da plataforma), gerada pelo banco; a busca textual da Unidade compara contra ela.", collation: "C"),
                    codigo_para_busca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(codigo)", stored: true, comment: "Forma normalizada de codigo (sem acento e em minúsculas, pela função da plataforma), gerada pelo banco; a busca textual da Unidade compara contra ela.", collation: "C"),
                    identificador_no_endereco_web_para_busca = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(identificador_no_endereco_web)", stored: true, comment: "Forma normalizada de identificador_no_endereco_web (sem acento e em minúsculas, pela função da plataforma), gerada pelo banco; a busca textual da Unidade compara contra ela.", collation: "C"),
                    nome_para_busca = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(nome)", stored: true, comment: "Forma normalizada de nome (sem acento e em minúsculas, pela função da plataforma), gerada pelo banco; a busca textual da Unidade compara contra ela.", collation: "C"),
                    sigla_para_busca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, computedColumnSql: "plataforma.normalizar_para_comparacao(sigla)", stored: true, comment: "Forma normalizada de sigla (sem acento e em minúsculas, pela função da plataforma), gerada pelo banco; a busca textual da Unidade compara contra ela.", collation: "C"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidades", x => x.id);
                    table.CheckConstraint("ck_unidades_cidade_completa", "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)");
                    table.CheckConstraint("ck_unidades_tipo", "tipo IN ('REITORIA', 'PRO_REITORIA', 'CENTRO', 'INSTITUTO', 'FACULDADE', 'DEPARTAMENTO', 'COORDENACAO', 'DIRETORIA', 'DIVISAO', 'NUCLEO', 'OUTRO')");
                    table.ForeignKey(
                        name: "fk_unidades_unidades_unidade_superior_id",
                        column: x => x.unidade_superior_id,
                        principalSchema: "organizacao",
                        principalTable: "unidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_de_discentes",
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
                    table.PrimaryKey("pk_vinculos_de_discentes", x => x.id);
                },
                comment: "Réplica local dos vínculos de discentes sincronizados do SIGAA (ADR-0121) — dados copiados, sem consulta automática a outras tabelas.");

            migrationBuilder.CreateTable(
                name: "anexos_do_ato",
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
                    table.PrimaryKey("pk_anexos_do_ato", x => x.id);
                    table.CheckConstraint("ck_anexos_do_ato_hash", "hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_anexos_do_ato_nome", "btrim(nome) <> ''");
                    table.CheckConstraint("ck_anexos_do_ato_ordem", "ordem >= 0");
                    table.CheckConstraint("ck_anexos_do_ato_tipo_midia", "tipo_midia ~ '^[a-z0-9][a-z0-9.+_-]*/[a-z0-9][a-z0-9.+_-]*$'");
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "atos_normativos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exclusividades_de_atos",
                schema: "publicacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da exclusividade de atos."),
                    entidade_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Tipo da entidade tratada, opaco para o módulo (por exemplo, PROCESSO_SELETIVO)."),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da entidade tratada, opaco para o módulo."),
                    tipo_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do tipo de ato, copiado do ato que abriu a exclusividade."),
                    ato_inicial_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Ato inicial da cadeia de retificação que detém a exclusividade: identifica a cadeia inteira."),
                    ato_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Ato que abriu a exclusividade. Pode ser uma retificação, quando o ato inicial foi publicado sem vínculo com o objeto.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exclusividades_de_atos", x => x.id);
                    table.CheckConstraint("ck_exclusividades_de_atos_ato_inicial_nao_zero", "ato_inicial_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_exclusividades_de_atos_entidade_id_nao_zero", "entidade_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_exclusividades_de_atos_entidade_tipo_formato", "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_exclusividades_de_atos_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "atos_normativos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exclusividades_de_atos_ato_inicial",
                        column: x => x.ato_inicial_id,
                        principalSchema: "publicacoes",
                        principalTable: "atos_normativos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Exclusividade de atos: reserva que um objeto faz para uma única cadeia de retificação de atos de um tipo único por objeto.");

            migrationBuilder.CreateTable(
                name: "vinculos_do_ato_com_entidade",
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
                    table.PrimaryKey("pk_vinculos_do_ato_com_entidade", x => x.id);
                    table.CheckConstraint("ck_vinculos_do_ato_com_entidade_id_nao_zero", "entidade_id <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_vinculos_do_ato_com_entidade_tipo_formato", "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_vinculos_do_ato_com_entidade_ato",
                        column: x => x.ato_id,
                        principalSchema: "publicacoes",
                        principalTable: "atos_normativos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "municipios_da_base_legal_fada",
                schema: "configuracao",
                columns: table => new
                {
                    base_legal_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador do município na base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, comment: "Código IBGE do município abrangido pelo Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Nome do município abrangido pelo Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, comment: "Sigla da unidade federativa do município abrangido pelo Fator de Atenuação das Desigualdades de Acesso (FADA).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios_da_base_legal_fada", x => new { x.base_legal_id, x.id });
                    table.CheckConstraint("ck_municipios_da_base_legal_fada_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'");
                    table.ForeignKey(
                        name: "fk_municipios_da_base_legal_fada_base_legal_id",
                        column: x => x.base_legal_id,
                        principalSchema: "configuracao",
                        principalTable: "bases_legais_do_fada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Municípios abrangidos pela base legal do Fator de Atenuação das Desigualdades de Acesso (FADA).");

            migrationBuilder.CreateTable(
                name: "dias_nao_uteis",
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dias_nao_uteis", x => x.id);
                    table.CheckConstraint("ck_dias_nao_uteis_municipio_coerente", "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)");
                    table.CheckConstraint("ck_dias_nao_uteis_uf_coerente", "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_dias_nao_uteis_calendario_de_dias_uteis",
                        column: x => x.calendario_dias_uteis_id,
                        principalSchema: "configuracao",
                        principalTable: "calendarios_de_dias_uteis",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "locais_de_oferta",
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
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locais_de_oferta", x => x.id);
                    table.CheckConstraint("ck_locais_de_oferta_endereco_cidade_coerente", "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
                    table.CheckConstraint("ck_locais_de_oferta_endereco_completo", "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL AND endereco_logradouro IS NULL AND endereco_numero IS NULL AND endereco_complemento IS NULL AND endereco_bairro IS NULL AND endereco_distrito IS NULL AND endereco_latitude IS NULL AND endereco_longitude IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
                    table.CheckConstraint("ck_locais_de_oferta_tipo", "tipo IN ('CAMPUS_SEDE', 'CAMPUS_FORA_DE_SEDE', 'CURSO_FORA_DE_SEDE', 'POLO_DE_EDUCACAO_A_DISTANCIA', 'CONVENIO_DE_INTERIORIZACAO', 'OUTRO')");
                    table.ForeignKey(
                        name: "fk_locais_de_oferta_campus_campus_responsavel_id",
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
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Situação da convocação. Códigos: PENDENTE, ACEITA, RECUSADA, NAO_COMPARECEU, EXPIRADA."),
                    posicao = table.Column<int>(type: "integer", nullable: false),
                    codigo_curso = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_manifestacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convocacoes", x => x.id);
                    table.CheckConstraint("ck_convocacoes_status", "status IN ('PENDENTE', 'ACEITA', 'RECUSADA', 'NAO_COMPARECEU', 'EXPIRADA')");
                    table.ForeignKey(
                        name: "fk_convocacoes_chamadas_chamada_id",
                        column: x => x.chamada_id,
                        principalSchema: "ingresso",
                        principalTable: "chamadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opcoes_dos_dados",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da opção."),
                    fato_candidato_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do dado ao qual a opção pertence."),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Código da opção, único no dado."),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Descrição da opção, quando informada."),
                    orientacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Orientação para a escolha, quando informada."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição de apresentação da opção."),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a opção está disponível para escolha."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opcoes_dos_dados", x => x.id);
                    table.ForeignKey(
                        name: "fk_opcoes_dos_dados_dados_do_candidato",
                        column: x => x.fato_candidato_id,
                        principalSchema: "configuracao",
                        principalTable: "dados_do_candidato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Opções declaradas para dados categóricos do candidato.");

            migrationBuilder.CreateTable(
                name: "permissoes_implantadas",
                schema: "autorizacao",
                columns: table => new
                {
                    implantacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
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
                    exigencia_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "O DocumentoExigido.Id da configuração congelada da versão da inscrição. Sem chave estrangeira para a configuração viva (ADR-0070)."),
                    ocorrencia_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "A ocorrência do grupo repetível a que o documento se refere; nulo quando a exigência não se repete."),
                    nome_do_documento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "O nome do documento como a exigência o chamava no envio; dá nome ao arquivo baixado."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
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
                comment: "Comprovante de uma submissão da inscrição — append-only, sem nenhum número de documento; uma correção soma uma linha nova, nunca muda a anterior.");

            migrationBuilder.CreateTable(
                name: "grupos_da_inscricao",
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
                    table.PrimaryKey("pk_grupos_da_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Uma ocorrência de grupo repetível da inscrição, como cada dependente de um grupo de dependentes.");

            migrationBuilder.CreateTable(
                name: "gestores_da_instalacao",
                schema: "autorizacao",
                columns: table => new
                {
                    emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gestores_da_instalacao", x => new { x.instalacao_id, x.emissor, x.conta_id });
                    table.ForeignKey(
                        name: "fk_gestores_da_instalacao_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tentativas_de_instalacao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instalacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    autor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tentativas_de_instalacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_tentativas_de_instalacao_instalacoes_instalacao_id",
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
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
                name: "historicos_da_obrigatoriedade",
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
                    table.PrimaryKey("pk_historicos_da_obrigatoriedade", x => x.id);
                    table.ForeignKey(
                        name: "fk_historicos_da_obrigatoriedade_regra_id",
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
                    desativada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    desativada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reativada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reativada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reativada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reativada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_da_reativacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_desativacoes_do_perfil", x => x.id);
                    table.CheckConstraint("ck_desativacoes_do_perfil_reativacao", "(reativada_em IS NULL AND reativada_por_emissor IS NULL AND reativada_por_conta_id IS NULL AND reativada_por_nome IS NULL AND motivo_da_reativacao IS NULL) OR (reativada_em IS NOT NULL AND reativada_por_emissor IS NOT NULL AND reativada_por_conta_id IS NOT NULL AND motivo_da_reativacao IS NOT NULL)");
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
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_divergencias_da_governanca", x => x.id);
                    table.CheckConstraint("ck_divergencias_da_governanca_tipo", "tipo IN ('COMPOSICAO_FALTANTE', 'COMPOSICAO_EXCEDENTE', 'CLASSIFICACAO', 'ESCOPO')");
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
                name: "permissoes_do_perfil",
                schema: "autorizacao",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissoes_do_perfil", x => new { x.perfil_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_permissoes_do_perfil_perfis_perfil_id",
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
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false)
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
                    suspensa_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    suspensa_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    papel_de_quem_suspendeu = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    retomada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suspensoes_do_perfil", x => x.id);
                    table.CheckConstraint("ck_suspensoes_do_perfil_papel", "papel_de_quem_suspendeu IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS')");
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
                    criada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    criada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    criada_por_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    motivo_da_remocao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    categoria_da_remocao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    versao_do_documento = table.Column<Guid>(type: "uuid", nullable: true),
                    forma_do_cancelamento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    motivo_do_cancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expirada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_do_perfil", x => x.id);
                    table.UniqueConstraint("ak_versoes_do_perfil_perfil_versao", x => new { x.perfil_id, x.id });
                    table.CheckConstraint("ck_versoes_do_perfil_cancelamento", "(situacao = 'CANCELADA') = (forma_do_cancelamento IS NOT NULL AND cancelada_em IS NOT NULL) AND (forma_do_cancelamento IS NULL) = (cancelada_em IS NULL)");
                    table.CheckConstraint("ck_versoes_do_perfil_expiracao", "(situacao = 'EXPIRADA') = (expirada_em IS NOT NULL)");
                    table.CheckConstraint("ck_versoes_do_perfil_motivo_do_cancelamento", "forma_do_cancelamento IS DISTINCT FROM 'PESSOA' OR motivo_do_cancelamento IS NOT NULL");
                    table.CheckConstraint("ck_versoes_do_perfil_origem", "(origem = 'REMOCAO' AND motivo_da_remocao IS NOT NULL AND versao_do_documento IS NOT NULL AND expira_em IS NULL) OR (origem = 'PROPOSTA' AND expira_em IS NOT NULL AND motivo_da_remocao IS NULL AND versao_do_documento IS NULL)");
                    table.ForeignKey(
                        name: "fk_versoes_do_perfil_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_versoes_do_perfil_versao_do_perfil_do_documento",
                        columns: x => new { x.perfil_id, x.versao_do_documento },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "areas_dos_pesos_do_enem",
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
                    table.PrimaryKey("pk_areas_dos_pesos_do_enem", x => new { x.peso_area_enem_id, x.codigo });
                    table.CheckConstraint("ck_areas_dos_pesos_do_enem_codigo", "codigo IN ('REDACAO', 'CIENCIAS_DA_NATUREZA', 'CIENCIAS_HUMANAS', 'LINGUAGENS', 'MATEMATICA')");
                    table.CheckConstraint("ck_areas_dos_pesos_do_enem_corte", "corte IS NULL OR (corte >= 0 AND corte <= 1000)");
                    table.CheckConstraint("ck_areas_dos_pesos_do_enem_peso", "peso >= 0");
                    table.ForeignKey(
                        name: "fk_areas_dos_pesos_do_enem_peso_por_area_do_enem",
                        column: x => x.peso_area_enem_id,
                        principalSchema: "configuracao",
                        principalTable: "pesos_por_area_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peso e corte de cada uma das cinco áreas do ENEM numa linha de Pesos por Área. Código e rótulo são postos pelo sistema; o operador edita só peso e corte.");

            migrationBuilder.CreateTable(
                name: "atendimentos_especializados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atendimentos_especializados", x => x.id);
                    table.ForeignKey(
                        name: "fk_atendimentos_especializados_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
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
                    slug_da_unidade_administradora = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    identificador_legivel_do_processo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    item_do_edital = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    documento_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ato_corrigido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_da_correcao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    politica_de_divulgacao_confirmada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    politica_de_divulgacao_confirmada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atos_do_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_atos_do_processo_documento_a_publicar_principal",
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
                name: "calculos_dos_dados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual pertence."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado calculado. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calculos_dos_dados", x => x.id);
                    table.ForeignKey(
                        name: "fk_calculos_dos_dados_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configurações dos cálculos dos dados de candidatos em um processo.");

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
                    baseado_em_enem = table.Column<bool>(type: "boolean", nullable: false, comment: "A classificação usa a estrutura de pontuação por área do ENEM — sinal explícito do qual as regras de eliminação do ENEM dependem, substituindo a ramificação por TipoProcesso."),
                    resolucao_peso_area_enem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true, comment: "Resolução de Pesos por Área declarada pela classificação baseada em ENEM com cálculo local; o vínculo com o cadastro é pelo valor, e o quadro fica congelado em grupos_do_quadro_de_pesos. Nulo nas demais classificações."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_classificacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_classificacao_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_de_remanejamento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da configuração de remanejamento."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual a configuração pertence."),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código da regra de remanejamento."),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Versão da regra de remanejamento."),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo do conteúdo da regra de remanejamento."),
                    destino_final_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código da modalidade de destino final das vagas."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_de_remanejamento", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_de_remanejamento_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Regra e destino final do remanejamento de vagas do processo.");

            migrationBuilder.CreateTable(
                name: "configuracoes_de_vagas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oferta_curso_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vagas_ofertadas_base = table.Column<int>(type: "integer", nullable: false),
                    percentual_da_reserva_legal = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
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
                    ajuste_do_quadro = table.Column<string>(type: "jsonb", nullable: true),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Código do grupo de área do ENEM do curso da oferta, sem abreviação e sem acento, congelado por valor do cadastro de cursos na definição da distribuição; casa a oferta com a linha de Pesos por Área. Nulo quando o curso não declara grupo."),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Rótulo do grupo de área do ENEM do curso da oferta, congelado por valor junto do código na definição da distribuição. Nulo quando o curso não declara grupo."),
                    oferta_curso_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Código da oferta de curso, congelado por valor do cadastro na definição da distribuição."),
                    oferta_curso_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "Nome do curso da oferta, congelado por valor do cadastro na definição da distribuição."),
                    oferta_curso_grau = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Grau do curso da oferta, congelado por valor do cadastro na definição da distribuição."),
                    oferta_unidade_ofertante_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true, comment: "Nome da unidade ofertante, congelado por valor do cadastro na definição da distribuição."),
                    oferta_turnos = table.Column<string[]>(type: "text[]", nullable: true, comment: "Turnos que a oferta ocupa (tokens), congelados por valor do cadastro na definição da distribuição."),
                    reserva_legal_nominal = table.Column<int>(type: "integer", nullable: true),
                    reserva_legal_efetiva = table.Column<int>(type: "integer", nullable: true),
                    excedente_sobre_reserva_legal = table.Column<int>(type: "integer", nullable: true),
                    vagas_cortadas_por_escassez = table.Column<int>(type: "integer", nullable: true),
                    vagas_retiradas_no_ajuste = table.Column<string>(type: "jsonb", nullable: true),
                    total_de_vagas_do_edital = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_de_vagas", x => x.id);
                    table.CheckConstraint("ck_configuracoes_de_vagas_excedente_sobre_reserva_legal", "excedente_sobre_reserva_legal IS NULL OR excedente_sobre_reserva_legal = GREATEST(0, reserva_legal_efetiva - reserva_legal_nominal)");
                    table.CheckConstraint("ck_configuracoes_de_vagas_percentual_da_reserva_legal", "percentual_da_reserva_legal IS NULL OR (percentual_da_reserva_legal >= 0.5 AND percentual_da_reserva_legal <= 1)");
                    table.CheckConstraint("ck_configuracoes_de_vagas_ramo_do_quadro", "reserva_legal_nominal IS NULL OR (vagas_retiradas_no_ajuste IS NULL AND ajuste_do_quadro IS NULL)");
                    table.CheckConstraint("ck_configuracoes_de_vagas_reserva_legal_completa", "(percentual_da_reserva_legal IS NULL AND reserva_legal_nominal IS NULL AND reserva_legal_efetiva IS NULL AND excedente_sobre_reserva_legal IS NULL AND vagas_cortadas_por_escassez IS NULL) OR (percentual_da_reserva_legal IS NOT NULL AND reserva_legal_nominal IS NOT NULL AND reserva_legal_efetiva IS NOT NULL AND excedente_sobre_reserva_legal IS NOT NULL AND vagas_cortadas_por_escassez IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_configuracoes_de_vagas_processo_seletivo",
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_divulgacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_divulgacao_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_do_fada",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da configuração do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo que aplica o Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código da regra do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Versão da regra do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo que identifica o conteúdo da regra do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    fator = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false, comment: "Multiplicador do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    teto = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true, comment: "Limite da nota após o Fator de Atenuação das Desigualdades de Acesso (FADA), quando definido."),
                    fato_elegibilidade_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código do dado que decide a elegibilidade ao Fator de Atenuação das Desigualdades de Acesso (FADA), quando definido."),
                    base_legal_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) copiado na configuração."),
                    base_legal_codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Código da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) copiado na configuração."),
                    tipo_instrumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Tipo do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA): LEI, DECRETO, PORTARIA, RESOLUCAO, INSTRUCAO_NORMATIVA, PARECER ou EDITAL; copiado da base legal."),
                    identificacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Identificação do instrumento do Fator de Atenuação das Desigualdades de Acesso (FADA), copiada da base legal."),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, comment: "Descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA), copiada na configuração."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_do_fada", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_do_fada_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configurações do Fator de Atenuação das Desigualdades de Acesso (FADA) nos processos seletivos.");

            migrationBuilder.CreateTable(
                name: "configuracoes_taxa_inscricao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) — não confundir com o Id do processo seletivo, a FK."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id do processo seletivo dono desta configuração (FK 1:1, cascade delete)."),
                    cobra = table.Column<bool>(type: "boolean", nullable: false, comment: "Declaração explícita de cobrança de taxa — nunca inferida pela ausência da linha (CA-01)."),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, comment: "Valor da taxa em reais, positivo quando cobra=true; sempre nulo quando cobra=false (CA-02/CA-03)."),
                    fundamentos = table.Column<string[]>(type: "text[]", nullable: false, comment: "Fundamentos de isenção."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_taxa_inscricao", x => x.id);
                    table.ForeignKey(
                        name: "fk_configuracoes_taxa_inscricao_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configuração de taxa de inscrição e fundamentos de isenção do processo seletivo — entidade dependente 1:1 de processos_seletivos.");

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
                    parametros = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
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
                name: "etapas_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da etapa."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único no processo."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Processo seletivo ao qual a etapa pertence."),
                    fase_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da fase do cronograma em que a etapa acontece."),
                    nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Nome da etapa no processo seletivo."),
                    carater = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Finalidade da etapa na avaliação: classificatoria, eliminatoria ou ambas."),
                    tipo_etapa_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do tipo de etapa de origem, copiado na definição."),
                    tipo_etapa_codigo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Código do tipo de etapa copiado na definição."),
                    tipo_etapa_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de etapa copiado na definição."),
                    tipo_etapa_admite_pontuacao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o tipo de etapa admite pontuação, conforme a definição copiada."),
                    tipo_etapa_admite_eliminacao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o tipo de etapa admite eliminação, conforme a definição copiada."),
                    tipo_etapa_nota_de_origem_no_enem = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a nota da etapa vem do Enem, conforme a definição do tipo."),
                    tipo_etapa_exige_correcao_independente = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a etapa exige correções independentes por mais de um corretor, conforme a definição do tipo."),
                    peso = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "Peso da etapa no cálculo da nota final, quando aplicável."),
                    nota_minima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "Nota mínima exigida na etapa, quando definida."),
                    nota_maxima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "Nota máxima da escala da etapa com nota; ausente na etapa sem nota e na do ENEM."),
                    passo_do_lancamento = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "De quanto em quanto a nota pode ser lançada; vale só para a nota digitada."),
                    forma_de_avaliacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Como a nota da etapa é obtida: OBJETIVA ou HUMANA; ausente quando não definida."),
                    apuracao_avaliacao_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Código da regra de consolidação das notas, quando definida."),
                    apuracao_avaliacao_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "Versão da regra de consolidação das notas, quando definida."),
                    apuracao_avaliacao_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true, comment: "Resumo que identifica o conteúdo da regra de consolidação, quando definida."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição da etapa no processo, a partir de um."),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Início do período próprio da etapa; ausente quando usa o período da fase."),
                    fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Fim do período próprio da etapa; ausente quando usa o período da fase."),
                    emite_parecer_individual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indica se a etapa emite parecer individual para cada candidato."),
                    tipos_de_decisao = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]", comment: "Tipos de decisão que a fase ou a etapa declara produzir, sem repetição. Códigos: ISENCAO_DA_TAXA, HOMOLOGACAO_DAS_INSCRICOES, ANALISE_DOCUMENTAL, HETEROIDENTIFICACAO, AVALIACAO_BIOPSICOSSOCIAL, CLASSIFICACAO, GABARITO_DA_PROVA, NOTA_DA_ETAPA."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas_processo", x => x.id);
                    table.UniqueConstraint("ak_etapas_processo_id_processo_seletivo_id", x => new { x.id, x.processo_seletivo_id });
                    table.CheckConstraint("ck_etapas_processo_carater_valido", "carater IN ('CLASSIFICATORIA', 'ELIMINATORIA', 'AMBAS')");
                    table.CheckConstraint("ck_etapas_processo_forma_de_avaliacao_valida", "forma_de_avaliacao IS NULL OR forma_de_avaliacao IN ('OBJETIVA', 'HUMANA')");
                    table.CheckConstraint("ck_etapas_processo_nota_maxima_positiva", "nota_maxima IS NULL OR nota_maxima > 0");
                    table.CheckConstraint("ck_etapas_processo_nota_minima_nao_negativa", "nota_minima IS NULL OR nota_minima >= 0");
                    table.CheckConstraint("ck_etapas_processo_passo_positivo", "passo_do_lancamento IS NULL OR passo_do_lancamento > 0");
                    table.CheckConstraint("ck_etapas_processo_periodo_em_ordem", "inicio IS NULL OR fim IS NULL OR inicio <= fim");
                    table.CheckConstraint("ck_etapas_processo_peso_positivo", "peso IS NULL OR peso > 0");
                    table.ForeignKey(
                        name: "fk_etapas_processo_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Etapas de avaliação e suas definições em cada processo seletivo.");

            migrationBuilder.CreateTable(
                name: "fases_cronograma",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da fase no cronograma."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único no processo."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Processo seletivo ao qual a fase pertence."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição da fase no cronograma."),
                    no_mesmo_momento_da_anterior = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indica se a fase ocorre no mesmo momento da fase anterior."),
                    tipo_fase_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da fase de origem copiado ao definir o cronograma."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código da fase de origem copiado ao definir o cronograma. Exemplo: HOMOLOGACAO_INSCRICOES."),
                    tipo_fase_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de fase copiado do cadastro ao definir o cronograma."),
                    unidade_responsavel_origem_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da unidade responsável, copiado quando definida."),
                    unidade_responsavel_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Código da unidade responsável no cadastro, copiado ao salvar a fase."),
                    unidade_responsavel_sigla = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Sigla da unidade responsável, copiada quando definida."),
                    unidade_responsavel_nome = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true, comment: "Nome da unidade responsável, copiado quando definida."),
                    origem_data = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Momento em que as datas da fase são definidas: NO_EDITAL ou NA_EXECUCAO."),
                    agrupa_etapas = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a fase agrupa etapas de avaliação."),
                    permite_complementacao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a fase permite complementar informações."),
                    coleta_inscricao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a fase recebe inscrições."),
                    coleta_solicitacao_isencao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a fase recebe pedidos de isenção da taxa."),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Início do período da fase, quando definido."),
                    fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Fim do período da fase, quando definido."),
                    emite_parecer_individual = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a fase emite parecer individual para cada candidato."),
                    regra_de_homologacao = table.Column<string>(type: "jsonb", nullable: true, comment: "Regra de homologação das inscrições que a fase declara: o resultado que publica a lista e as alternativas de condições, na ordem declarada. Nula quando a fase não homologa."),
                    tipos_de_decisao = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]", comment: "Tipos de decisão que a fase ou a etapa declara produzir, sem repetição. Códigos: ISENCAO_DA_TAXA, HOMOLOGACAO_DAS_INSCRICOES, ANALISE_DOCUMENTAL, HETEROIDENTIFICACAO, AVALIACAO_BIOPSICOSSOCIAL, CLASSIFICACAO, GABARITO_DA_PROVA, NOTA_DA_ETAPA."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fases_cronograma", x => x.id);
                    table.UniqueConstraint("ak_fases_cronograma_id_processo_seletivo_id", x => new { x.id, x.processo_seletivo_id });
                    table.CheckConstraint("ck_fases_cronograma_origem_data", "origem_data IN ('NO_EDITAL', 'NA_EXECUCAO')");
                    table.CheckConstraint("ck_fases_cronograma_periodo_em_ordem", "inicio IS NULL OR fim IS NULL OR inicio <= fim");
                    table.CheckConstraint("ck_fases_cronograma_unidade_completa", "(unidade_responsavel_origem_id IS NULL AND unidade_responsavel_codigo IS NULL AND unidade_responsavel_sigla IS NULL AND unidade_responsavel_nome IS NULL) OR (unidade_responsavel_origem_id IS NOT NULL AND unidade_responsavel_codigo IS NOT NULL AND unidade_responsavel_sigla IS NOT NULL AND unidade_responsavel_nome IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_fases_cronograma_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Fases e períodos do cronograma de cada processo seletivo.");

            migrationBuilder.CreateTable(
                name: "formularios_processo",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Finalidade do formulário. Códigos: INSCRICAO, ISENCAO_TAXA, HABILITACAO."),
                    fase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    modelo_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modelo_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    modelo_origem_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "Nome copiado do modelo de formulário de origem, nulo quando não há modelo."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formularios_processo", x => x.id);
                    table.CheckConstraint("ck_formularios_processo_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                    table.ForeignKey(
                        name: "fk_formularios_processo_processo_seletivo",
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
                    gerado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "A conta do token de quem gerou."),
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
                        name: "fk_geracoes_de_envio_ao_inep_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "O registro de que o arquivo de envio ao INEP foi gerado para uma edição do ENEM. Append-only; o arquivo e os CPFs não são guardados.");

            migrationBuilder.CreateTable(
                name: "grupos_dos_formularios",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual pertence."),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Finalidade do formulário. Códigos: INSCRICAO, ISENCAO_TAXA, HABILITACAO."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código que identifica o item no processo."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição de apresentação ou avaliação no conjunto."),
                    etapa_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da etapa do formulário, quando houver."),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Texto apresentado à pessoa candidata."),
                    minimo = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade mínima de ocorrências do grupo."),
                    maximo = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade máxima de ocorrências do grupo, quando limitada."),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true, comment: "Condições para exibir o grupo, em JSON, quando houver."),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false, comment: "Regra que determina quando a resposta é obrigatória, em JSON."),
                    inclui_candidato = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o grupo inclui a própria pessoa candidata."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_dos_formularios", x => x.id);
                    table.CheckConstraint("ck_grupos_dos_formularios_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                    table.ForeignKey(
                        name: "fk_grupos_dos_formularios_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Grupos repetíveis de dados solicitados nos formulários.");

            migrationBuilder.CreateTable(
                name: "modelos_de_documento",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    formato = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false, comment: "Formato do documento. Códigos: DOCX, ODT, PDF."),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "Situação do envio. Códigos: PENDENTE, CONFIRMADO."),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    object_key_confirmado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos_de_documento", x => x.id);
                    table.CheckConstraint("ck_modelos_de_documento_formato", "formato IN ('DOCX', 'ODT', 'PDF')");
                    table.CheckConstraint("ck_modelos_de_documento_status", "status IN ('PENDENTE', 'CONFIRMADO')");
                    table.ForeignKey(
                        name: "fk_modelos_de_documento_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "municipios_de_prova",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador técnico da linha, substituída com a declaração.")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_ibge = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false, comment: "Código do IBGE do município de prova da prova."),
                    uf = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false, comment: "UF do município de prova da prova."),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true, comment: "Nome copiado do Geo na última publicação ou retificação; nulo antes da primeira."),
                    posicao = table.Column<int>(type: "integer", nullable: false, comment: "Posição do município na declaração, a partir de 1."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Processo seletivo que declara o município de prova.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios_de_prova", x => x.id);
                    table.CheckConstraint("ck_municipios_de_prova_codigo_ibge", "codigo_ibge ~ '^[0-9]{7}$'");
                    table.CheckConstraint("ck_municipios_de_prova_posicao", "posicao >= 1");
                    table.ForeignKey(
                        name: "fk_municipios_de_prova_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Municípios de prova da prova presencial, na ordem declarada pelo processo seletivo.");

            migrationBuilder.CreateTable(
                name: "opcoes_declaradas_fato",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual pertence."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código da opção de resposta."),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Texto apresentado à pessoa candidata."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição de apresentação ou avaliação no conjunto."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opcoes_declaradas_fato", x => x.id);
                    table.ForeignKey(
                        name: "fk_opcoes_declaradas_fato_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Opções de resposta declaradas pelo processo para um dado.");

            migrationBuilder.CreateTable(
                name: "termos_exigidos_formulario",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Finalidade do formulário. Códigos: INSCRICAO, ISENCAO_TAXA, HABILITACAO."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    termo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    termo_codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    versao_numero = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    texto = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    forma_aceite = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hash_versao = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termos_exigidos_formulario", x => x.id);
                    table.CheckConstraint("ck_termos_exigidos_formulario_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                    table.ForeignKey(
                        name: "fk_termos_exigidos_formulario_processo_seletivo",
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
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da versão da configuração."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual pertence a versão."),
                    numero_versao = table.Column<int>(type: "integer", nullable: false, comment: "Número sequencial da versão do processo."),
                    vigente_a_partir_de = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora a partir da qual a versão vigora."),
                    versao_do_formato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Versão do formato do conteúdo da configuração."),
                    algoritmo_hash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Algoritmo usado para calcular o resumo da configuração."),
                    bytes_da_configuracao = table.Column<byte[]>(type: "bytea", nullable: false, comment: "Bytes da configuração publicada usados no cálculo do resumo."),
                    conteudo_da_configuracao = table.Column<string>(type: "jsonb", nullable: false, comment: "Conteúdo da configuração publicada em JSON para consulta."),
                    hash_configuracao = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo SHA-256 do conteúdo da configuração."),
                    ato_criador_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do ato que criou a versão."),
                    ato_criador_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo SHA-256 do ato que criou a versão."),
                    ato_criador_retifica_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador do ato retificado, quando houver."),
                    ator_usuario_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Identificador da conta responsável pela publicação.")
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
                },
                comment: "Versões publicadas da configuração do processo seletivo.");

            migrationBuilder.CreateTable(
                name: "versoes_do_termo_consentimento",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    termo_consentimento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    base_legal = table.Column<string>(type: "text", nullable: false),
                    forma_aceite = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    promovida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    promovida_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versoes_do_termo_consentimento", x => x.id);
                    table.CheckConstraint("ck_versoes_do_termo_consentimento_forma_aceite", "forma_aceite IN ('A_DEFINIR', 'REGISTRO_SEM_ENDERECO_IP', 'REGISTRO_COM_ENDERECO_IP')");
                    table.CheckConstraint("ck_versoes_do_termo_consentimento_numero", "numero >= 1");
                    table.ForeignKey(
                        name: "fk_versoes_do_termo_consentimento_termo_de_consentimento",
                        column: x => x.termo_consentimento_id,
                        principalSchema: "configuracao",
                        principalTable: "termos_de_consentimento",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "opcoes_do_tipo_de_conceito",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador técnico da linha; os valores são substituídos em bloco a cada edição.")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Código do valor (maiúsculas, dígitos e sublinhado), único dentro do tipo."),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Rótulo exibido ao avaliador, único dentro do tipo."),
                    conta_como_aprovacao = table.Column<bool>(type: "boolean", nullable: false, comment: "Verdadeiro quando escolher este valor conta como aprovação. Um tipo pode não ter nenhum valor assim."),
                    posicao = table.Column<int>(type: "integer", nullable: false, comment: "Lugar do valor na lista do tipo, a partir de 1; é a única expressão da ordem."),
                    tipo_conceito_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Tipo de conceito categórico a que o valor pertence.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opcoes_do_tipo_de_conceito", x => x.id);
                    table.CheckConstraint("ck_opcoes_do_tipo_de_conceito_codigo", "codigo ~ '^[A-Z][A-Z0-9_]*$'");
                    table.CheckConstraint("ck_opcoes_do_tipo_de_conceito_posicao", "posicao >= 1");
                    table.ForeignKey(
                        name: "fk_opcoes_do_tipo_de_conceito_tipos_conceito",
                        column: x => x.tipo_conceito_id,
                        principalSchema: "configuracao",
                        principalTable: "tipos_conceito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Valores de um tipo de conceito categórico, na ordem em que foram informados.");

            migrationBuilder.CreateTable(
                name: "tipos_etapa_conceitos",
                schema: "configuracao",
                columns: table => new
                {
                    tipo_etapa_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Tipo de etapa que admite o conceito."),
                    tipo_conceito_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Tipo de conceito admitido pelo tipo de etapa.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_etapa_conceitos", x => new { x.tipo_etapa_id, x.tipo_conceito_id });
                    table.ForeignKey(
                        name: "fk_tipos_etapa_conceitos_tipos_conceito_tipo_conceito_id",
                        column: x => x.tipo_conceito_id,
                        principalSchema: "configuracao",
                        principalTable: "tipos_conceito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tipos_etapa_conceitos_tipos_etapa_tipo_etapa_id",
                        column: x => x.tipo_etapa_id,
                        principalSchema: "configuracao",
                        principalTable: "tipos_etapa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Tipos de conceito com que as etapas de um tipo de etapa podem ser avaliadas.");

            migrationBuilder.CreateTable(
                name: "identificadores_da_unidade",
                schema: "organizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "Tipo do identificador cujo histórico a linha registra. Códigos: IDENTIFICADOR_NO_ENDERECO_WEB, SIGLA, CODIGO, APELIDO."),
                    valor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo_mudanca = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identificadores_da_unidade", x => x.id);
                    table.CheckConstraint("ck_identificadores_da_unidade_tipo", "tipo IN ('IDENTIFICADOR_NO_ENDERECO_WEB', 'SIGLA', 'CODIGO', 'APELIDO')");
                    table.ForeignKey(
                        name: "fk_identificadores_da_unidade_unidades_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "organizacao",
                        principalTable: "unidades",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "instituicoes",
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
                    reitoria_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Unidade do tipo reitoria à qual a instituição está vinculada; opcional."),
                    instituicao_unica = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Sempre verdadeiro; com o índice único entre as instituições ativas, garante que exista uma só instituição ativa."),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instituicoes", x => x.id);
                    table.CheckConstraint("ck_instituicoes_cidade_completa", "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)");
                    table.CheckConstraint("ck_instituicoes_cidade_obrigatoria_com_endereco", "endereco_cep IS NULL OR cidade_codigo_ibge IS NOT NULL");
                    table.CheckConstraint("ck_instituicoes_endereco_cidade_coerente", "endereco_cidade_codigo_ibge IS NULL OR cidade_codigo_ibge IS NULL OR (endereco_cidade_codigo_ibge = cidade_codigo_ibge AND endereco_cidade_uf IS NOT NULL AND cidade_uf IS NOT NULL AND endereco_cidade_uf = cidade_uf)");
                    table.CheckConstraint("ck_instituicoes_endereco_completo", "(endereco_cep IS NULL AND endereco_cidade_codigo_ibge IS NULL AND endereco_cidade_nome IS NULL AND endereco_cidade_uf IS NULL AND endereco_nivel_resolucao IS NULL AND endereco_origem IS NULL AND endereco_logradouro IS NULL AND endereco_numero IS NULL AND endereco_complemento IS NULL AND endereco_bairro IS NULL AND endereco_distrito IS NULL AND endereco_latitude IS NULL AND endereco_longitude IS NULL) OR (endereco_cep IS NOT NULL AND endereco_cidade_codigo_ibge IS NOT NULL AND endereco_cidade_nome IS NOT NULL AND endereco_cidade_uf IS NOT NULL AND endereco_nivel_resolucao IS NOT NULL AND endereco_origem IS NOT NULL)");
                    table.CheckConstraint("ck_instituicoes_instituicao_unica", "instituicao_unica = true");
                    table.ForeignKey(
                        name: "fk_instituicoes_unidade_reitoria_id",
                        column: x => x.reitoria_id,
                        principalSchema: "organizacao",
                        principalTable: "unidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ofertas_de_curso",
                schema: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela criação, quando disponível."),
                    updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "Identificador do responsável pela última alteração, quando disponível."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o registro foi excluído logicamente."),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da exclusão lógica, quando houver."),
                    deleted_by = table.Column<string>(type: "text", nullable: true, comment: "Identificador do responsável pela exclusão lógica, quando disponível.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ofertas_de_curso", x => x.id);
                    table.CheckConstraint("ck_ofertas_de_curso_base_legal_programa", "programa_de_oferta = 'REGULAR' OR base_legal IS NOT NULL");
                    table.CheckConstraint("ck_ofertas_de_curso_codigo_formato", "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
                    table.CheckConstraint("ck_ofertas_de_curso_formato_pedagogico", "formato_pedagogico IN ('PRESENCIAL', 'SEMIPRESENCIAL', 'EAD')");
                    table.CheckConstraint("ck_ofertas_de_curso_funcionamento_regime_de_turno", "(regime_de_funcionamento <> 'INTENSIVO' OR regime_de_turno = 'INTEGRAL')");
                    table.CheckConstraint("ck_ofertas_de_curso_programa_de_oferta", "programa_de_oferta IN ('REGULAR', 'FORMA_PARA', 'PARFOR', 'PRONERA', 'PEPETI', 'CONVENIO_OUTRO', 'OUTRO')");
                    table.CheckConstraint("ck_ofertas_de_curso_regime_de_funcionamento", "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO', 'ALTERNANCIA_PEDAGOGICA')");
                    table.CheckConstraint("ck_ofertas_de_curso_regime_de_turno", "regime_de_turno IN ('REGULAR', 'INTEGRAL')");
                    table.CheckConstraint("ck_ofertas_de_curso_turnos_dominio", "turnos::text[] <@ ARRAY['MATUTINO', 'VESPERTINO', 'NOTURNO']::text[]");
                    table.CheckConstraint("ck_ofertas_de_curso_turnos_regime", "coalesce(array_ndims(turnos), 0) = 1 AND coalesce(array_lower(turnos, 1), 1) = 1 AND ((regime_de_turno = 'REGULAR' AND cardinality(turnos) = 1) OR (regime_de_turno = 'INTEGRAL' AND cardinality(turnos) = 2 AND turnos[1] <> turnos[2]))");
                    table.CheckConstraint("ck_ofertas_de_curso_vagas_anuais_autorizadas", "vagas_anuais_autorizadas IS NULL OR vagas_anuais_autorizadas >= 0");
                    table.ForeignKey(
                        name: "fk_ofertas_de_curso_cursos_curso_id",
                        column: x => x.curso_id,
                        principalSchema: "configuracao",
                        principalTable: "cursos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ofertas_de_curso_locais_de_oferta_local_oferta_id",
                        column: x => x.local_oferta_id,
                        principalSchema: "configuracao",
                        principalTable: "locais_de_oferta",
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
                    formato = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false, comment: "Formato do arquivo."),
                    tamanho_maximo_bytes = table.Column<long>(type: "bigint", nullable: false, comment: "O maior tamanho que a exigência admite para o formato; o teto do sistema (10 MB) só quando ela não define limite, e nunca acima de 20 MB. Copiado da exigência no início do envio."),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "A chave do objeto no bucket de envio (quarentena); só o id do arquivo a compõe."),
                    situacao = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false, comment: "Situação da verificação; só o arquivo verificado atende à exigência."),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: true),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "O SHA-256 do conteúdo, calculado pelo verificador; nulo enquanto não verificado."),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    etag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "O ETag que o PUT devolveu ao candidato, enviado na confirmação; fixa a versão do objeto que o verificador confere."),
                    publicacoes = table.Column<int>(type: "integer", nullable: false, comment: "Quantas vezes o pedido de verificação foi publicado; o prazo da reconciliação dobra a cada uma."),
                    proxima_publicacao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Quando a reconciliação pode republicar o pedido: a última publicação mais o prazo, que cresce a cada publicação."),
                    chave_definitiva = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "A chave do objeto promovido ao bucket definitivo; nula enquanto não verificado."),
                    motivo_da_recusa = table.Column<string>(type: "character varying(38)", maxLength: 38, nullable: true, comment: "Por que a verificação recusou o arquivo; nulo fora da recusa."),
                    constou_de_envio = table.Column<bool>(type: "boolean", nullable: false, comment: "O arquivo verificado já constou de um envio da inscrição; a limpeza dos envios seguintes nunca o apaga (UNI-REQ-0168)."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arquivos_da_apresentacao", x => x.id);
                    table.CheckConstraint("ck_arquivos_da_apresentacao_formato", "formato IN ('PDF', 'JPEG', 'PNG')");
                    table.CheckConstraint("ck_arquivos_da_apresentacao_motivo_da_recusa", "motivo_da_recusa IN ('TAMANHO_EXCEDIDO', 'FORMATO_NAO_CORRESPONDE', 'PDF_COM_SENHA', 'NAO_PASSOU_NA_VERIFICACAO_DE_SEGURANCA')");
                    table.CheckConstraint("ck_arquivos_da_apresentacao_situacao", "situacao IN ('PENDENTE', 'EM_VERIFICACAO', 'VERIFICADO', 'RECUSADO', 'REENVIO_NECESSARIO')");
                    table.ForeignKey(
                        name: "fk_arquivos_da_apresentacao_apresentacao_de_documento",
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
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da inscrição à qual pertence."),
                    ocorrencia_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da ocorrência de grupo, quando a resposta pertence a um grupo repetível."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    origem = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, comment: "Origem da resposta."),
                    cep = table.Column<string>(type: "text", nullable: true, comment: "Código postal do endereço, quando informado."),
                    logradouro = table.Column<string>(type: "text", nullable: true, comment: "Nome do logradouro, quando informado."),
                    numero = table.Column<string>(type: "text", nullable: true, comment: "Número do endereço, quando informado."),
                    complemento = table.Column<string>(type: "text", nullable: true, comment: "Complemento do endereço, quando informado."),
                    bairro = table.Column<string>(type: "text", nullable: true, comment: "Bairro do endereço, quando informado."),
                    distrito = table.Column<string>(type: "text", nullable: true, comment: "Distrito do endereço, quando informado."),
                    municipio_codigo_ibge = table.Column<string>(type: "text", nullable: true, comment: "Código IBGE do município, quando informado."),
                    municipio_nome = table.Column<string>(type: "text", nullable: true, comment: "Nome do município, quando informado."),
                    uf = table.Column<string>(type: "text", nullable: true, comment: "Sigla da unidade federativa, quando informada."),
                    latitude = table.Column<string>(type: "text", nullable: true, comment: "Latitude do endereço, quando disponível."),
                    longitude = table.Column<string>(type: "text", nullable: true, comment: "Longitude do endereço, quando disponível."),
                    nivel_resolucao = table.Column<string>(type: "text", nullable: true, comment: "Precisão da localização obtida para o endereço, quando disponível."),
                    procedencia = table.Column<string>(type: "text", nullable: true, comment: "Como o endereço foi preenchido no formulário: pela busca do CEP no Geo ou à mão."),
                    cep_confirmado = table.Column<bool>(type: "boolean", nullable: true, comment: "O candidato confirmou que o CEP é o geral da cidade ou do bairro, quando o Geo só o resolveu por faixa.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enderecos_da_inscricao", x => x.id);
                    table.CheckConstraint("ck_enderecos_da_inscricao_origem", "origem IN ('INFORMADA', 'CALCULADA')");
                    table.ForeignKey(
                        name: "fk_enderecos_da_inscricao_inscricao_record_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_enderecos_da_inscricao_ocorrencia_de_grupo_da_inscricao",
                        column: x => x.ocorrencia_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_da_inscricao",
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
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da inscrição à qual pertence."),
                    ocorrencia_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da ocorrência de grupo, quando a resposta pertence a um grupo repetível."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    posicao = table.Column<int>(type: "integer", nullable: true, comment: "Posição do valor em uma resposta com vários valores; nula para valor único."),
                    origem = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false, comment: "Origem da resposta."),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "Tipo do valor da resposta."),
                    valor_booleano = table.Column<bool>(type: "boolean", nullable: true, comment: "Resposta booleana, quando aplicável."),
                    valor_numero = table.Column<decimal>(type: "numeric", nullable: true, comment: "Resposta numérica, quando aplicável."),
                    valor_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da opção ou município respondido, quando aplicável."),
                    valor_rotulo = table.Column<string>(type: "text", nullable: true, comment: "O nome que o Geo conferiu para o município de valor_codigo; nulo no rascunho e preenchido na inscrição enviada."),
                    valor_texto = table.Column<string>(type: "text", nullable: true, comment: "Resposta em texto livre, quando aplicável."),
                    valor_data = table.Column<DateOnly>(type: "date", nullable: true, comment: "Resposta de data, quando aplicável."),
                    valor_cifrado = table.Column<byte[]>(type: "bytea", nullable: true, comment: "Resposta cifrada (AES-GCM, ADR-0121) — nunca texto claro: o número de documento, com a chave dos identificadores, ou o dado sensível de cifra individual, com a chave dos sensíveis.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_respostas_da_inscricao", x => x.id);
                    table.CheckConstraint("ck_respostas_da_inscricao_origem", "origem IN ('INFORMADA', 'CALCULADA')");
                    table.CheckConstraint("ck_respostas_da_inscricao_valor_do_tipo", "num_nonnulls(valor_booleano, valor_numero, valor_codigo, valor_texto, valor_data, valor_cifrado) = 1 AND tipo IN ('SIM_OU_NAO', 'NUMERO', 'OPCAO', 'TEXTO', 'DATA', 'MUNICIPIO') AND (valor_cifrado IS NOT NULL OR (tipo = 'SIM_OU_NAO' AND valor_booleano IS NOT NULL) OR (tipo = 'NUMERO' AND valor_numero IS NOT NULL) OR (tipo = 'OPCAO' AND valor_codigo IS NOT NULL) OR (tipo = 'TEXTO' AND valor_texto IS NOT NULL) OR (tipo = 'DATA' AND valor_data IS NOT NULL) OR (tipo = 'MUNICIPIO' AND valor_codigo IS NOT NULL)) AND (valor_rotulo IS NULL OR (tipo = 'MUNICIPIO' AND valor_codigo IS NOT NULL))");
                    table.ForeignKey(
                        name: "fk_respostas_da_inscricao_grupos_da_inscricao_ocorrencia_id",
                        column: x => x.ocorrencia_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_da_inscricao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_respostas_da_inscricao_inscricoes_inscricao_id",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Um valor respondido na inscrição, na coluna do tipo do fato, com a origem: informado pelo candidato ou calculado a partir de outra resposta.");

            migrationBuilder.CreateTable(
                name: "decisoes_da_retomada",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suspensao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    perfil_protegido_na_decisao = table.Column<bool>(type: "boolean", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_retomada", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_retomada_etapa", "etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS')");
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
                    decisor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    table.CheckConstraint("ck_decisoes_da_versao_etapa", "etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS')");
                    table.CheckConstraint("ck_decisoes_da_versao_motivo_na_recusa", "(resultado = 'RECUSADO') = (motivo IS NOT NULL)");
                    table.CheckConstraint("ck_decisoes_da_versao_resultado", "resultado IN ('APROVADO', 'RECUSADO')");
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
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
                name: "permissoes_da_versao_do_perfil",
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
                    table.PrimaryKey("pk_permissoes_da_versao_do_perfil", x => new { x.versao_id, x.codigo });
                    table.ForeignKey(
                        name: "fk_permissoes_da_versao_do_perfil_versoes_do_perfil_versao_id",
                        column: x => x.versao_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "revisoes_das_concessoes",
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
                    termina_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    encerrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    forma_do_encerramento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    motivo_do_encerramento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    encerrada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encerrada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    encerrada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aviso_48h_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aviso_24h_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisoes_das_concessoes", x => x.id);
                    table.UniqueConstraint("ak_revisao_das_concessoes_do_perfil_id_perfil_id", x => new { x.id, x.perfil_id });
                    table.CheckConstraint("ck_revisoes_das_concessoes_encerramento", "(encerrada_em IS NULL) = (forma_do_encerramento IS NULL)");
                    table.CheckConstraint("ck_revisoes_das_concessoes_forma", "forma_do_encerramento IS NULL OR forma_do_encerramento IN ('CONCLUIDA', 'PRAZO', 'MANUAL')");
                    table.CheckConstraint("ck_revisoes_das_concessoes_manual", "COALESCE(forma_do_encerramento = 'MANUAL', false) = (motivo_do_encerramento IS NOT NULL AND encerrada_por_conta_id IS NOT NULL)");
                    table.CheckConstraint("ck_revisoes_das_concessoes_termino", "termina_em = aberta_em + interval '72 hours'");
                    table.ForeignKey(
                        name: "fk_revisoes_das_concessoes_perfis_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revisoes_das_concessoes_versao_do_perfil_alvo",
                        columns: x => new { x.perfil_id, x.versao_alvo_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_revisoes_das_concessoes_versao_do_perfil_nova",
                        columns: x => new { x.perfil_id, x.versao_nova_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
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
                name: "condicoes_ofertadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_especializado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condicao_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condicao_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    condicao_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_ofertadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_ofertadas_atendimento_especializado",
                        column: x => x.atendimento_especializado_id,
                        principalSchema: "selecao",
                        principalTable: "atendimentos_especializados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deficiencias_atendidas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_especializado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_deficiencia_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_deficiencia_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipo_deficiencia_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deficiencias_atendidas", x => x.id);
                    table.ForeignKey(
                        name: "fk_deficiencias_atendidas_atendimento_especializado",
                        column: x => x.atendimento_especializado_id,
                        principalSchema: "selecao",
                        principalTable: "atendimentos_especializados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recursos_ofertados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atendimento_especializado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recurso_nome = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recursos_ofertados", x => x.id);
                    table.ForeignKey(
                        name: "fk_recursos_ofertados_atendimento_especializado",
                        column: x => x.atendimento_especializado_id,
                        principalSchema: "selecao",
                        principalTable: "atendimentos_especializados",
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos_do_ato_do_processo", x => x.id);
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_do_processo_ato_do_processo",
                        column: x => x.ato_do_processo_id,
                        principalSchema: "selecao",
                        principalTable: "atos_do_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anexos_do_ato_do_processo_documento_a_publicar",
                        column: x => x.documento_a_publicar_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_a_publicar",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regras_de_calculo_dos_dados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculo_do_dado_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do cálculo ao qual a regra pertence."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição de apresentação ou avaliação no conjunto."),
                    contribui = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código do valor produzido quando a regra é atendida, quando houver."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_de_calculo_dos_dados", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_de_calculo_dos_dados_calculo_do_dado",
                        column: x => x.calculo_do_dado_id,
                        principalSchema: "selecao",
                        principalTable: "calculos_dos_dados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Regras ordenadas que compõem o cálculo de um dado.");

            migrationBuilder.CreateTable(
                name: "grupos_do_quadro_de_pesos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) do grupo congelado."),
                    configuracao_classificacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id da configuração de classificação dona do quadro (FK, cascade delete)."),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código do grupo de área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área; casa a oferta com a linha do quadro."),
                    grupo_area_enem_rotulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Rótulo do grupo de área do ENEM, copiado junto do código."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Dispositivo legal que fundamenta os pesos do grupo, copiado da resolução de Pesos por Área."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_do_quadro_de_pesos", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_do_quadro_de_pesos_configuracao_classificacao",
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
                    parametros = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_eliminacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_regras_eliminacao_configuracao_classificacao",
                        column: x => x.configuracao_classificacao_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_classificacao",
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinos_remanejamento", x => x.id);
                    table.CheckConstraint("ck_destinos_remanejamento_ordem_positiva", "ordem >= 1");
                    table.CheckConstraint("ck_destinos_remanejamento_origem_diferente_destino", "modalidade_origem_codigo <> modalidade_destino_codigo");
                    table.ForeignKey(
                        name: "fk_destinos_remanejamento_configuracao_de_remanejamento",
                        column: x => x.configuracao_cascata_remanejamento_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_de_remanejamento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modalidades_selecionadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da modalidade selecionada."),
                    configuracao_distribuicao_vagas_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da configuração de distribuição à qual pertence."),
                    modalidade_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da modalidade no cadastro de origem."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código da modalidade copiado do cadastro."),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, comment: "Descrição da modalidade copiada do cadastro, quando disponível."),
                    natureza_legal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Natureza legal da modalidade. Códigos: COTA_RESERVADA, AMPLA, ACAO_AFIRMATIVA."),
                    composicao_vagas = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Composição das vagas da modalidade. Códigos: RESTANTE_DAS_VAGAS_OFERTADAS, DENTRO_DA_RESERVA_LEGAL, RETIRADA_DE_OUTRA_MODALIDADE, SUPLEMENTAR_AO_TOTAL."),
                    composicao_origem_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade de origem da composição, quando aplicável."),
                    regra_remanejamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Regra de remanejamento da modalidade. Códigos: SEGUE_ORDEM_DE_REMANEJAMENTO, DESTINO_UNICO, CRUZADO."),
                    remanejamento_destino = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade de destino do remanejamento, quando definido."),
                    remanejamento_par = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade pareada para remanejamento, quando definido."),
                    remanejamento_destino_final = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da modalidade de destino final do remanejamento, quando definido."),
                    regra_de_aplicabilidade = table.Column<string>(type: "jsonb", nullable: false, comment: "Condições de aplicabilidade da modalidade, copiadas do cadastro em JSON."),
                    acao_quando_indeferido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Ação prevista quando o pedido é indeferido, quando definida."),
                    base_legal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Fundamento legal da modalidade copiado do cadastro."),
                    quantidade_declarada = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade de vagas declarada, quando informada."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modalidades_selecionadas", x => x.id);
                    table.CheckConstraint("ck_modalidades_selecionadas_composicao_vagas", "composicao_vagas IN ('RESTANTE_DAS_VAGAS_OFERTADAS', 'DENTRO_DA_RESERVA_LEGAL', 'RETIRADA_DE_OUTRA_MODALIDADE', 'SUPLEMENTAR_AO_TOTAL')");
                    table.CheckConstraint("ck_modalidades_selecionadas_natureza_legal", "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'ACAO_AFIRMATIVA')");
                    table.CheckConstraint("ck_modalidades_selecionadas_regra_remanejamento", "regra_remanejamento IN ('SEGUE_ORDEM_DE_REMANEJAMENTO', 'DESTINO_UNICO', 'CRUZADO')");
                    table.ForeignKey(
                        name: "fk_modalidades_selecionadas_configuracao_de_vagas",
                        column: x => x.configuracao_distribuicao_vagas_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_de_vagas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Modalidades escolhidas para a distribuição de vagas, com dados copiados do cadastro.");

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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vagas_ofertadas", x => x.id);
                    table.ForeignKey(
                        name: "fk_vagas_ofertadas_configuracao_de_vagas",
                        column: x => x.configuracao_distribuicao_vagas_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_de_vagas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "municipios_do_fada",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do município na configuração do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    configuracao_do_fada_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da configuração do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, comment: "Código IBGE do município do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Nome do município do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, comment: "Sigla da unidade federativa do município do Fator de Atenuação das Desigualdades de Acesso (FADA)."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios_do_fada", x => x.id);
                    table.ForeignKey(
                        name: "fk_municipios_do_fada_configuracao_do_fada_id",
                        column: x => x.configuracao_do_fada_id,
                        principalSchema: "selecao",
                        principalTable: "configuracoes_do_fada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Municípios da configuração do Fator de Atenuação das Desigualdades de Acesso (FADA), copiados da base legal.");

            migrationBuilder.CreateTable(
                name: "bancas_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da banca."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único no processo."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da etapa à qual a banca pertence."),
                    posicao_no_tipo = table.Column<int>(type: "integer", nullable: false, comment: "Posição da banca entre as bancas do mesmo tipo."),
                    tipo_banca_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do tipo de banca no cadastro."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do tipo de banca copiado do cadastro."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de banca copiado do cadastro."),
                    papel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Papel do tipo de banca copiado do cadastro."),
                    exige_duas_ou_mais_bancas = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o tipo exige duas ou mais bancas."),
                    encerra_recurso_na_primeira_instancia = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o tipo encerra o recurso na primeira instância."),
                    tipos_de_decisao_julgados = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]", comment: "Tipos de decisão que o tipo de banca julgava quando a banca o copiou, em ordem alfabética ordinal do código e sem repetição."),
                    regra_deliberacao_modo = table.Column<string>(type: "text", nullable: true, comment: "Modo de deliberação da banca."),
                    regra_deliberacao_integrantes = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade de integrantes da banca."),
                    regra_deliberacao_suplentes = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade de suplentes da banca."),
                    regra_deliberacao_quorum = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade mínima de integrantes para deliberar."),
                    regra_deliberacao_criterio = table.Column<string>(type: "text", nullable: true, comment: "Critério da deliberação."),
                    regra_deliberacao_desempate = table.Column<string>(type: "text", nullable: true, comment: "Regra de desempate da deliberação."),
                    regra_deliberacao_encerramento = table.Column<string>(type: "text", nullable: true, comment: "Regra de encerramento da deliberação."),
                    regra_deliberacao_sem_unanimidade = table.Column<string>(type: "text", nullable: true, comment: "Regra aplicada quando não há unanimidade."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Processo seletivo ao qual a banca pertence."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancas_da_etapa", x => x.id);
                    table.ForeignKey(
                        name: "fk_bancas_da_etapa_etapa",
                        columns: x => new { x.etapa_processo_id, x.processo_seletivo_id },
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumns: new[] { "id", "processo_seletivo_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itens_de_avaliacao_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do item de avaliação."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único na etapa."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Etapa à qual o item pertence."),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Rótulo do critério de avaliação."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição do item na etapa, a partir de um."),
                    pontuacao_maxima = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, comment: "O quanto o item vale da nota máxima da etapa."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_de_avaliacao_da_etapa", x => x.id);
                    table.CheckConstraint("ck_itens_de_avaliacao_da_etapa_pontuacao_positiva", "pontuacao_maxima > 0");
                    table.ForeignKey(
                        name: "fk_itens_de_avaliacao_da_etapa_etapa_processo",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Itens de avaliação (critérios ou blocos) da etapa; o id é a identidade a que lançamento e recurso apontam.");

            migrationBuilder.CreateTable(
                name: "niveis_de_atendimento_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, comment: "Identificador técnico do nível.")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Texto escolhido pela banca na avaliação."),
                    fracao = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false, comment: "Fração da pontuação do item atribuída ao nível."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único na etapa."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição do nível na escala, a partir de um."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Etapa à qual o nível pertence.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_niveis_de_atendimento_da_etapa", x => x.id);
                    table.CheckConstraint("ck_niveis_atendimento_fracao_entre_0_e_1", "fracao BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_niveis_atendimento_rotulo_preenchido", "char_length(btrim(rotulo)) > 0");
                    table.ForeignKey(
                        name: "fk_niveis_atendimento_etapa",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Níveis de atendimento usados na avaliação dos itens da etapa.");

            migrationBuilder.CreateTable(
                name: "publicacoes_previstas_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro ao qual esta configuração pertence."),
                    ato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do ato previsto para publicação."),
                    papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Papel da publicação no resultado, quando aplicável: PRELIMINAR ou DEFINITIVO."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publicacoes_previstas_da_etapa", x => x.id);
                    table.CheckConstraint("ck_publicacoes_previstas_da_etapa_papel", "papel IS NULL OR papel IN ('PRELIMINAR', 'DEFINITIVO')");
                    table.ForeignKey(
                        name: "fk_publicacoes_previstas_da_etapa_etapa",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Publicações previstas para uma etapa de avaliação.");

            migrationBuilder.CreateTable(
                name: "recursos_configurados_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro ao qual esta configuração pertence."),
                    evento_inicial_do_prazo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Evento que inicia o prazo: PUBLICACAO_DO_ATO ou CIENCIA_INDIVIDUAL."),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código da regra aplicada ao prazo de recurso."),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Versão da regra aplicada ao prazo de recurso."),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo do conteúdo da regra aplicada ao prazo de recurso."),
                    prazo_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, comment: "Duração do prazo de recurso na unidade informada."),
                    prazo_unidade = table.Column<string>(type: "text", nullable: false, comment: "Unidade do prazo: HORAS, DIAS ou DIAS_UTEIS."),
                    admite_instancia_superior = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se há recurso à instância superior."),
                    prazo_segunda_instancia_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "Duração do prazo de recurso na unidade informada."),
                    prazo_segunda_instancia_unidade = table.Column<string>(type: "text", nullable: true, comment: "Unidade do prazo: HORAS, DIAS ou DIAS_UTEIS."),
                    efeito_suspensivo = table.Column<string>(type: "text", nullable: false, comment: "Efeito do recurso sobre o andamento do processo. Código: SEM_EFEITO_SUSPENSIVO."),
                    guarda_da_vaga = table.Column<string>(type: "text", nullable: false, comment: "Guarda da vaga durante o recurso: ATE_O_RESULTADO_DEFINITIVO ou NAO_GUARDA."),
                    publicacao_de_referencia_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da publicação usada como referência para o início do prazo."),
                    procedimentos_de_recurso = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb", comment: "Procedimentos de recurso declarados para a janela, um por matéria, em ordem canônica."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recursos_configurados_da_etapa", x => x.id);
                    table.CheckConstraint("ck_recursos_configurados_da_etapa_evento_inicial_do_prazo", "evento_inicial_do_prazo IN ('PUBLICACAO_DO_ATO', 'CIENCIA_INDIVIDUAL')");
                    table.ForeignKey(
                        name: "fk_recursos_configurados_da_etapa_etapa",
                        column: x => x.etapa_processo_id,
                        principalSchema: "selecao",
                        principalTable: "etapas_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configurações dos recursos de uma etapa de avaliação.");

            migrationBuilder.CreateTable(
                name: "bancas_requeridas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da banca."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único no processo."),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da fase à qual a banca pertence."),
                    posicao_no_tipo = table.Column<int>(type: "integer", nullable: false, comment: "Posição da banca entre as bancas do mesmo tipo."),
                    tipo_banca_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do tipo de banca no cadastro."),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do tipo de banca copiado do cadastro."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de banca copiado do cadastro."),
                    papel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Papel do tipo de banca copiado do cadastro."),
                    exige_duas_ou_mais_bancas = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o tipo exige duas ou mais bancas."),
                    encerra_recurso_na_primeira_instancia = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o tipo encerra o recurso na primeira instância."),
                    tipos_de_decisao_julgados = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]", comment: "Tipos de decisão que o tipo de banca julgava quando a banca o copiou, em ordem alfabética ordinal do código e sem repetição."),
                    regra_deliberacao_modo = table.Column<string>(type: "text", nullable: true, comment: "Modo de deliberação da banca."),
                    regra_deliberacao_integrantes = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade de integrantes da banca."),
                    regra_deliberacao_suplentes = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade de suplentes da banca."),
                    regra_deliberacao_quorum = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade mínima de integrantes para deliberar."),
                    regra_deliberacao_criterio = table.Column<string>(type: "text", nullable: true, comment: "Critério da deliberação."),
                    regra_deliberacao_desempate = table.Column<string>(type: "text", nullable: true, comment: "Regra de desempate da deliberação."),
                    regra_deliberacao_encerramento = table.Column<string>(type: "text", nullable: true, comment: "Regra de encerramento da deliberação."),
                    regra_deliberacao_sem_unanimidade = table.Column<string>(type: "text", nullable: true, comment: "Regra aplicada quando não há unanimidade."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Processo seletivo ao qual a banca pertence."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancas_requeridas", x => x.id);
                    table.ForeignKey(
                        name: "fk_bancas_requeridas_fase",
                        columns: x => new { x.fase_cronograma_id, x.processo_seletivo_id },
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumns: new[] { "id", "processo_seletivo_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documentos_exigidos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da exigência de documento."),
                    chave_local = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Identificador que a parte guarda dentro da configuração publicada; único no processo."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Processo seletivo que faz a exigência."),
                    exigido_na_fase_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Fase do cronograma do processo em que o documento é exigido."),
                    exigido_na_etapa_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Etapa da fase que coleta o documento; vazio quando o documento é da fase inteira."),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Formulário a que o documento pertence; vazio quando a fase não responde formulário. Códigos: INSCRICAO, ISENCAO_TAXA, HABILITACAO."),
                    tipo_documento_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Tipo de documento do catálogo que a exigência pede, como estava ao gravar a exigência."),
                    tipo_documento_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do tipo de documento, copiado do catálogo ao gravar a exigência."),
                    tipo_documento_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome do tipo de documento, copiado do catálogo ao gravar a exigência."),
                    tipo_documento_categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Categoria do tipo de documento, copiada do catálogo ao gravar a exigência."),
                    classificacao_protecao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Classificação de proteção de dados do documento, copiada do tipo de documento: PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR ou SENSIVEL."),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Finalidade do tratamento dos dados do documento, copiada do tipo de documento."),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "Hipótese legal do tratamento dos dados do documento, copiada do tipo de documento."),
                    fatos_comprovados = table.Column<string[]>(type: "text[]", nullable: false, comment: "Códigos dos fatos do processo que o documento comprova; vazio quando a exigência não comprova fato."),
                    aplicabilidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "A quem a exigência se aplica: todos os candidatos ou só quem atende às condições da exigência. Códigos: GERAL, CONDICIONAL."),
                    obrigatorio = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a falta do documento impede a inscrição de seguir."),
                    consequencia_indeferimento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "O que acontece com o candidato quando o documento é indeferido; vazio quando a exigência não determina resultado. Códigos: ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM, ABRE_PRAZO_PARA_REENVIO."),
                    formatos_permitidos_qualquer = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se o documento pode ser enviado em qualquer formato de arquivo."),
                    formatos_permitidos_lista = table.Column<string>(type: "jsonb", nullable: true, comment: "Lista dos formatos de arquivo aceitos, cada um com o limite de tamanho próprio quando houver; vazia quando qualquer formato é aceito."),
                    tamanho_maximo_bytes = table.Column<int>(type: "integer", nullable: true, comment: "Tamanho máximo do arquivo do documento, em bytes; vazio quando não há limite."),
                    modelo_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Modelo de documento que a exigência oferece ao candidato; vazio quando não há modelo."),
                    modelo_nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, comment: "Nome do arquivo do modelo de documento, copiado ao gravar a exigência."),
                    modelo_formato = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true, comment: "Formato do arquivo do modelo, copiado ao gravar a exigência. Códigos: DOCX, ODT, PDF."),
                    modelo_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Resumo SHA-256 do arquivo do modelo, copiado ao gravar a exigência."),
                    prazo_emissao_escolha = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Escolha sobre o prazo de emissão do documento: SEM_PRAZO ou COM_PRAZO; vazio enquanto a escolha não foi feita."),
                    prazo_emissao_valor = table.Column<int>(type: "integer", nullable: true, comment: "Quantidade de dias, meses ou anos do prazo de emissão, maior que zero; preenchida só quando a escolha é COM_PRAZO."),
                    prazo_emissao_unidade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, comment: "Unidade do prazo de emissão: DIAS, MESES ou ANOS; preenchida só quando a escolha é COM_PRAZO."),
                    prazo_emissao_referencia_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Data de referência do prazo de emissão: FIM_DAS_INSCRICOES, INICIO_DA_FASE, FIM_DA_FASE, DATA_ESPECIFICA ou DATA_DA_APRESENTACAO; preenchida só quando a escolha é COM_PRAZO."),
                    prazo_emissao_referencia_data = table.Column<DateOnly>(type: "date", nullable: true, comment: "Data específica usada como referência do prazo de emissão; preenchida só quando o tipo da data de referência é DATA_ESPECIFICA."),
                    prazo_emissao_referencia_fase_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Fase do cronograma cujo início ou fim é a referência do prazo de emissão; preenchida só quando o tipo da data de referência é INICIO_DA_FASE ou FIM_DA_FASE."),
                    prazo_emissao_efeito = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "O que a análise faz com o documento emitido fora do prazo: RECUSAR_DOCUMENTO ou ACEITAR_COM_AVISO; preenchida só quando a escolha é COM_PRAZO."),
                    validade_escolha = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Escolha sobre a validade do documento: NAO_EXIGE_DOCUMENTO_VALIDO ou EXIGE_DOCUMENTO_VALIDO; vazio enquanto a escolha não foi feita."),
                    validade_referencia_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Data de referência da validade: FIM_DAS_INSCRICOES, INICIO_DA_FASE, FIM_DA_FASE, DATA_ESPECIFICA ou DATA_DA_APRESENTACAO; preenchida só quando a escolha é EXIGE_DOCUMENTO_VALIDO."),
                    validade_referencia_data = table.Column<DateOnly>(type: "date", nullable: true, comment: "Data específica usada como referência da validade; preenchida só quando o tipo da data de referência é DATA_ESPECIFICA."),
                    validade_referencia_fase_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Fase do cronograma cujo início ou fim é a referência da validade; preenchida só quando o tipo da data de referência é INICIO_DA_FASE ou FIM_DA_FASE."),
                    validade_efeito = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "O que a análise faz com o documento vencido: RECUSAR_DOCUMENTO ou ACEITAR_COM_AVISO; preenchida só quando a escolha é EXIGE_DOCUMENTO_VALIDO."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_exigidos", x => x.id);
                    table.CheckConstraint("ck_documentos_exigidos_aplicabilidade", "aplicabilidade IN ('GERAL', 'CONDICIONAL')");
                    table.CheckConstraint("ck_documentos_exigidos_consequencia_indeferimento", "consequencia_indeferimento IS NULL OR consequencia_indeferimento IN ('ELIMINA', 'RECLASSIFICA_AC', 'REMOVE_VANTAGEM', 'ABRE_PRAZO_PARA_REENVIO')");
                    table.CheckConstraint("ck_documentos_exigidos_finalidade", "finalidade IS NULL OR finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                    table.CheckConstraint("ck_documentos_exigidos_formatos_permitidos_coerente", "(formatos_permitidos_qualquer = true AND formatos_permitidos_lista IS NULL) OR (formatos_permitidos_qualquer = false AND formatos_permitidos_lista IS NOT NULL AND jsonb_array_length(formatos_permitidos_lista) > 0)");
                    table.CheckConstraint("ck_documentos_exigidos_modelo_formato", "modelo_formato IS NULL OR modelo_formato IN ('DOCX', 'ODT', 'PDF')");
                    table.CheckConstraint("ck_documentos_exigidos_prazo_emissao_efeito", "prazo_emissao_efeito IS NULL OR prazo_emissao_efeito IN ('RECUSAR_DOCUMENTO', 'ACEITAR_COM_AVISO')");
                    table.CheckConstraint("ck_documentos_exigidos_prazo_emissao_escolha", "prazo_emissao_escolha IS NULL OR prazo_emissao_escolha IN ('SEM_PRAZO', 'COM_PRAZO')");
                    table.CheckConstraint("ck_documentos_exigidos_prazo_emissao_referencia_tipo", "prazo_emissao_referencia_tipo IS NULL OR prazo_emissao_referencia_tipo IN ('FIM_DAS_INSCRICOES', 'INICIO_DA_FASE', 'FIM_DA_FASE', 'DATA_ESPECIFICA', 'DATA_DA_APRESENTACAO')");
                    table.CheckConstraint("ck_documentos_exigidos_prazo_emissao_unidade", "prazo_emissao_unidade IS NULL OR prazo_emissao_unidade IN ('DIAS', 'MESES', 'ANOS')");
                    table.CheckConstraint("ck_documentos_exigidos_validade_efeito", "validade_efeito IS NULL OR validade_efeito IN ('RECUSAR_DOCUMENTO', 'ACEITAR_COM_AVISO')");
                    table.CheckConstraint("ck_documentos_exigidos_validade_escolha", "validade_escolha IS NULL OR validade_escolha IN ('NAO_EXIGE_DOCUMENTO_VALIDO', 'EXIGE_DOCUMENTO_VALIDO')");
                    table.CheckConstraint("ck_documentos_exigidos_validade_referencia_tipo", "validade_referencia_tipo IS NULL OR validade_referencia_tipo IN ('FIM_DAS_INSCRICOES', 'INICIO_DA_FASE', 'FIM_DA_FASE', 'DATA_ESPECIFICA', 'DATA_DA_APRESENTACAO')");
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
                },
                comment: "Documentos que o processo seletivo exige do candidato: em que fase, para quem, o que restringe o arquivo e quando a análise recusa o documento.");

            migrationBuilder.CreateTable(
                name: "publicacoes_previstas_da_fase",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro."),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro ao qual esta configuração pertence."),
                    ato_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do ato previsto para publicação."),
                    papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Papel da publicação no resultado, quando aplicável: PRELIMINAR ou DEFINITIVO."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publicacoes_previstas_da_fase", x => x.id);
                    table.CheckConstraint("ck_publicacoes_previstas_da_fase_papel", "papel IS NULL OR papel IN ('PRELIMINAR', 'DEFINITIVO')");
                    table.ForeignKey(
                        name: "fk_publicacoes_previstas_da_fase_fase",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Publicações previstas para uma fase do cronograma.");

            migrationBuilder.CreateTable(
                name: "recursos_configurados_da_fase",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro."),
                    fase_cronograma_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro ao qual esta configuração pertence."),
                    regra_codigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código da regra aplicada ao prazo de recurso."),
                    regra_versao = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Versão da regra aplicada ao prazo de recurso."),
                    regra_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "Resumo do conteúdo da regra aplicada ao prazo de recurso."),
                    prazo_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, comment: "Duração do prazo de recurso na unidade informada."),
                    prazo_unidade = table.Column<string>(type: "text", nullable: false, comment: "Unidade do prazo: HORAS, DIAS ou DIAS_UTEIS."),
                    admite_instancia_superior = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se há recurso à instância superior."),
                    prazo_segunda_instancia_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true, comment: "Duração do prazo de recurso na unidade informada."),
                    prazo_segunda_instancia_unidade = table.Column<string>(type: "text", nullable: true, comment: "Unidade do prazo: HORAS, DIAS ou DIAS_UTEIS."),
                    efeito_suspensivo = table.Column<string>(type: "text", nullable: false, comment: "Efeito do recurso sobre o andamento do processo. Código: SEM_EFEITO_SUSPENSIVO."),
                    guarda_da_vaga = table.Column<string>(type: "text", nullable: false, comment: "Guarda da vaga durante o recurso: ATE_O_RESULTADO_DEFINITIVO ou NAO_GUARDA."),
                    publicacao_de_referencia_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da publicação usada como referência para o início do prazo."),
                    procedimentos_de_recurso = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb", comment: "Procedimentos de recurso declarados para a janela, um por matéria, em ordem canônica."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recursos_configurados_da_fase", x => x.id);
                    table.ForeignKey(
                        name: "fk_recursos_configurados_da_fase_fase",
                        column: x => x.fase_cronograma_id,
                        principalSchema: "selecao",
                        principalTable: "fases_cronograma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Configuração dos recursos de uma fase do cronograma.");

            migrationBuilder.CreateTable(
                name: "etapas_formulario",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    formulario_processo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "O que a etapa é. Códigos: SECAO, BLOCO."),
                    bloco = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "O bloco que o sistema monta; nulo na seção. Códigos: COMPROVACAO_DOCUMENTAL, MODALIDADES_CALCULADAS, REVISAO_E_ACEITE."),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    aviso = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    exibicao = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_etapas_formulario", x => x.id);
                    table.CheckConstraint("ck_etapas_formulario_bloco", "bloco IS NULL OR bloco IN ('COMPROVACAO_DOCUMENTAL', 'MODALIDADES_CALCULADAS', 'REVISAO_E_ACEITE')");
                    table.CheckConstraint("ck_etapas_formulario_tipo", "tipo IN ('SECAO', 'BLOCO')");
                    table.ForeignKey(
                        name: "fk_etapas_formulario_formulario_processo",
                        column: x => x.formulario_processo_id,
                        principalSchema: "selecao",
                        principalTable: "formularios_processo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dados_dos_formularios",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual pertence."),
                    grupo_coletado_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador do grupo repetível ao qual o dado pertence, quando houver."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    ordem = table.Column<int>(type: "integer", nullable: false, comment: "Posição de apresentação ou avaliação no conjunto."),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Texto apresentado à pessoa candidata."),
                    tipo_renderizacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Tipo de campo: NUMERO, SIM_OU_NAO, ESCOLHA_UNICA, ESCOLHA_MULTIPLA, TEXTO, DATA, ENDERECO ou MUNICIPIO."),
                    obrigatoriedade = table.Column<string>(type: "jsonb", nullable: false, comment: "Regra que determina quando a resposta é obrigatória, em JSON."),
                    ajuda = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Orientação adicional exibida junto ao campo, quando houver."),
                    pedir_confirmacao = table.Column<bool>(type: "boolean", nullable: false, comment: "Indica se a resposta deve ser confirmada pela pessoa candidata."),
                    restricoes = table.Column<string>(type: "jsonb", nullable: false, comment: "Restrições aplicadas à resposta, em JSON."),
                    origem_valores = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Origem das opções: CATALOGO, OPCOES_DO_PROCESSO, MUNICIPIOS_DO_BONUS, UNIDADES_FEDERATIVAS ou MUNICIPIOS_DA_PROVA_PRESENCIAL."),
                    formato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, comment: "Formato esperado da resposta, quando especificado."),
                    classificacao_protecao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Classificação de proteção do dado coletado."),
                    hipotese_legal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "Hipótese legal que fundamenta o tratamento do dado."),
                    finalidade_tratamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Finalidade informada para o tratamento do dado."),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, comment: "Descrição do dado solicitado."),
                    componente_exibicao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Componente usado para exibir o dado, quando especificado."),
                    impedimento = table.Column<string>(type: "jsonb", nullable: true, comment: "Regra de impedimento associada à resposta, em JSON, quando houver."),
                    finalidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Finalidade do formulário. Códigos: INSCRICAO, ISENCAO_TAXA, HABILITACAO."),
                    etapa_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true, comment: "Código da etapa do formulário, quando houver."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dados_dos_formularios", x => x.id);
                    table.CheckConstraint("ck_dados_dos_formularios_finalidade", "finalidade IN ('INSCRICAO', 'ISENCAO_TAXA', 'HABILITACAO')");
                    table.CheckConstraint("ck_dados_dos_formularios_origem_valores", "origem_valores IN ('CATALOGO', 'OPCOES_DO_PROCESSO', 'MUNICIPIOS_DO_FADA', 'UNIDADES_FEDERATIVAS', 'MUNICIPIOS_DA_PROVA_PRESENCIAL')");
                    table.ForeignKey(
                        name: "fk_dados_dos_formularios_grupo_formulario",
                        column: x => x.grupo_coletado_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_dos_formularios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dados_dos_formularios_processo_seletivo_processo_seletivo_id",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Dados solicitados nos formulários de um processo seletivo.");

            migrationBuilder.CreateTable(
                name: "importacoes_de_notas_do_enem",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da importação."),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do processo seletivo ao qual pertence a importação."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa de nota do ENEM, guardada por valor e sem chave estrangeira: pertence à configuração do processo, que a retificação recria."),
                    edicao_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "O código da opção do fato EDICAO_ENEM, como o processo a declara."),
                    leiaute_codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do leiaute usado para ler o arquivo."),
                    leiaute_versao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Versão do leiaute usado para ler o arquivo."),
                    leiaute_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "SHA-256 da definição do leiaute com que o arquivo foi lido."),
                    arquivo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, comment: "SHA-256 dos bytes do arquivo importado."),
                    importado_por = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "A conta do token de quem importou."),
                    importado_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "O nome de quem importou, gravado no momento da ação."),
                    importado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora da importação do arquivo."),
                    versao_configuracao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A versão divulgada do certame cujos pesos apuraram as avaliações do lote."),
                    total_de_linhas = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de linhas lidas no arquivo."),
                    total_de_inscritos_com_nota = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de inscritos com nota importada."),
                    total_de_lancamentos = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de notas individuais lançadas."),
                    total_de_inscritos_sem_linha = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de inscritos sem linha correspondente no arquivo."),
                    total_de_linhas_sem_inscrito = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de linhas sem inscrição correspondente."),
                    total_de_inscritos_em_conflito = table.Column<int>(type: "integer", nullable: false, comment: "Quantidade de inscritos com linhas conflitantes."),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false, comment: "Ordem atribuída pelo banco para desempatar importações do mesmo escopo.")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importacoes_de_notas_do_enem", x => x.id);
                    table.UniqueConstraint("ak_importacoes_de_notas_do_enem_id_etapa", x => new { x.id, x.etapa_processo_id });
                    table.CheckConstraint("ck_importacoes_de_notas_do_enem_edicao", "edicao_codigo <> ''");
                    table.CheckConstraint("ck_importacoes_de_notas_do_enem_hashes", "arquivo_hash ~ '^[0-9a-f]{64}$' AND leiaute_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_importacoes_de_notas_do_enem_totais", "total_de_linhas >= 0 AND total_de_inscritos_com_nota >= 0 AND total_de_lancamentos >= 0 AND total_de_inscritos_sem_linha >= 0 AND total_de_linhas_sem_inscrito >= 0 AND total_de_inscritos_em_conflito >= 0");
                    table.ForeignKey(
                        name: "fk_importacoes_de_notas_do_enem_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_importacoes_de_notas_do_enem_versao_configuracao",
                        column: x => x.versao_configuracao_id,
                        principalSchema: "selecao",
                        principalTable: "versoes_configuracao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Uma importação do retorno do INEP para um processo, uma etapa e uma edição do ENEM. Os registros não são alterados ou excluídos; vale por inteiro o lote mais recente para esses dados. Do arquivo, guarda-se apenas o resumo do conteúdo.");

            migrationBuilder.CreateTable(
                name: "eventos_de_bloqueio_do_perfil",
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
                    ator_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ator_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    papel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    versao_vigente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    perfil_protegido = table.Column<bool>(type: "boolean", nullable: false),
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_de_bloqueio_do_perfil", x => x.id);
                    table.CheckConstraint("ck_eventos_de_bloqueio_do_perfil_desativacao", "tipo <> 'DESATIVACAO' OR (ator = 'HUMANO' AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_de_bloqueio_do_perfil_reativacao", "tipo <> 'REATIVACAO' OR (ator = 'HUMANO' AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_de_bloqueio_do_perfil_retomada", "tipo <> 'RETOMADA' OR (ator = 'SISTEMA' AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND ator_emissor IS NULL AND ator_conta_id IS NULL AND ator_nome IS NULL AND papel IS NULL AND motivo IS NULL AND origem IS NOT NULL AND ((origem = 'DECISAO_DA_ETAPA' AND decisao_id IS NOT NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'TROCA_DE_VIGENCIA' AND versao_que_entrou_id IS NOT NULL AND decisao_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'RECLASSIFICACAO' AND permissao_codigo IS NOT NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL)))");
                    table.CheckConstraint("ck_eventos_de_bloqueio_do_perfil_suspensao", "tipo <> 'SUSPENSAO' OR (ator = 'HUMANO' AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND papel IS NOT NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)");
                    table.CheckConstraint("ck_eventos_de_bloqueio_do_perfil_tipo", "tipo IN ('SUSPENSAO', 'RETOMADA', 'DESATIVACAO', 'REATIVACAO')");
                    table.ForeignKey(
                        name: "fk_eventos_de_bloqueio_decisao_da_retomada",
                        column: x => x.decisao_id,
                        principalSchema: "autorizacao",
                        principalTable: "decisoes_da_retomada",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_de_bloqueio_desativacao",
                        column: x => x.desativacao_id,
                        principalSchema: "autorizacao",
                        principalTable: "desativacoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_de_bloqueio_do_perfil_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_de_bloqueio_suspensao",
                        column: x => x.suspensao_id,
                        principalSchema: "autorizacao",
                        principalTable: "suspensoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_de_bloqueio_versao_que_entrou",
                        column: x => x.versao_que_entrou_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eventos_de_bloqueio_versao_vigente",
                        column: x => x.versao_vigente_id,
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "acessos_ao_documento_da_versao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leitor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acessos_ao_documento_da_versao", x => x.id);
                    table.ForeignKey(
                        name: "fk_acessos_ao_documento_da_versao_documento_da_versao",
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
                    beneficiario_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    concedida_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    concedida_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    valida_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogada_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revogada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    revogada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_revogacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revogada_pela_revisao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concessoes", x => x.id);
                    table.UniqueConstraint("ak_concessao_perfil_id_id", x => new { x.perfil_id, x.id });
                    table.CheckConstraint("ck_concessoes_escopo_unidade", "(escopo_tipo = 'UNIDADE') = (unidade_id IS NOT NULL)");
                    table.CheckConstraint("ck_concessoes_revogacao_completa", "(revogada_em IS NULL AND revogada_por_emissor IS NULL AND revogada_por_conta_id IS NULL AND motivo_revogacao IS NULL AND revogada_pela_revisao_id IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NOT NULL AND revogada_por_conta_id IS NOT NULL AND motivo_revogacao IS NOT NULL AND revogada_pela_revisao_id IS NULL) OR (revogada_em IS NOT NULL AND revogada_por_emissor IS NULL AND revogada_por_conta_id IS NULL AND motivo_revogacao IS NOT NULL AND revogada_pela_revisao_id IS NOT NULL)");
                    table.CheckConstraint("ck_concessoes_validade_posterior", "valida_ate IS NULL OR valida_ate > concedida_em");
                    table.ForeignKey(
                        name: "fk_concessoes_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "autorizacao",
                        principalTable: "perfis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_concessoes_revisao_que_revogou",
                        columns: x => new { x.revogada_pela_revisao_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_das_concessoes",
                        principalColumns: new[] { "id", "perfil_id" },
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
                name: "envios_de_decisoes",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enviado_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enviado_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enviado_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    enviado_por_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    confirmou_exclusao = table.Column<bool>(type: "boolean", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_envios_de_decisoes", x => x.id);
                    table.UniqueConstraint("ak_envio_de_decisoes_da_revisao_revisao_id_id_etapa", x => new { x.revisao_id, x.id, x.etapa });
                    table.CheckConstraint("ck_envios_de_decisoes_etapa", "etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS')");
                    table.ForeignKey(
                        name: "fk_envios_de_decisoes_revisao_das_concessoes",
                        columns: x => new { x.revisao_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_das_concessoes",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "condicoes_do_calculo_dos_dados",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_derivacao_configurada_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da regra de cálculo à qual a condição pertence."),
                    alternativa = table.Column<int>(type: "integer", nullable: false, comment: "Número da alternativa que reúne condições atendidas em conjunto."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato usado na condição. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Comparação aplicada ao valor: ALGUM_DE, DIFERENTE, IGUAL, MAIOR_IGUAL, MENOR_IGUAL ou NENHUM_DE."),
                    valor = table.Column<string>(type: "jsonb", nullable: false, comment: "Valor de comparação da condição, em JSON."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_do_calculo_dos_dados", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_calculo_regra_de_calculo",
                        column: x => x.regra_derivacao_configurada_id,
                        principalSchema: "selecao",
                        principalTable: "regras_de_calculo_dos_dados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Condições que determinam a aplicação de uma regra de cálculo.");

            migrationBuilder.CreateTable(
                name: "areas_do_quadro_de_pesos",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador interno (UUIDv7) da área congelada."),
                    grupo_peso_area_enem_congelado_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Id do grupo congelado dono da área (FK, cascade delete)."),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "Código da área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área."),
                    rotulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Rótulo oficial da área do ENEM, copiado junto do código."),
                    peso = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false, comment: "Peso da área na média do grupo, copiado da resolução de Pesos por Área."),
                    corte = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true, comment: "Nota mínima da área (0 a 1000), copiada da resolução de Pesos por Área; nulo quando a área não tem corte."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_areas_do_quadro_de_pesos", x => x.id);
                    table.CheckConstraint("ck_areas_do_quadro_de_pesos_corte", "corte IS NULL OR (corte >= 0 AND corte <= 1000)");
                    table.CheckConstraint("ck_areas_do_quadro_de_pesos_peso", "peso >= 0");
                    table.ForeignKey(
                        name: "fk_areas_do_quadro_de_pesos_grupo_do_quadro_de_pesos",
                        column: x => x.grupo_peso_area_enem_congelado_id,
                        principalSchema: "selecao",
                        principalTable: "grupos_do_quadro_de_pesos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Peso e corte de cada área do ENEM num grupo do quadro de pesos por área congelado na classificação, copiados por valor da resolução de Pesos por Área declarada.");

            migrationBuilder.CreateTable(
                name: "categorias_julgadas",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da categoria julgada."),
                    banca_requerida_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da banca que julga a categoria."),
                    categoria_documento_origem_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da categoria de documento de origem."),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Código da categoria de documento."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Nome da categoria de documento."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
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
                },
                comment: "Categorias de documento julgadas por uma banca.");

            migrationBuilder.CreateTable(
                name: "condicoes_das_exigencias",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da condição da exigência."),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do documento exigido ao qual a condição pertence."),
                    alternativa = table.Column<int>(type: "integer", nullable: false, comment: "Número da alternativa que reúne condições atendidas em conjunto."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato usado na condição. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Comparação aplicada ao valor: ALGUM_DE, DIFERENTE, IGUAL, MAIOR_IGUAL, MENOR_IGUAL ou NENHUM_DE."),
                    valor = table.Column<string>(type: "jsonb", nullable: false, comment: "Valor de comparação da condição, em JSON."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_das_exigencias", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_das_exigencias_documento_exigido",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Condições que determinam a exigência de um documento.");

            migrationBuilder.CreateTable(
                name: "documentos_exigidos_base_legal",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da base legal da exigência."),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Exigência de documento à qual a base legal pertence."),
                    referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Referência da base legal da exigência."),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Abrangência da base legal."),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Situação da base legal."),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Observação sobre a base legal, quando informada."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos_exigidos_base_legal", x => x.id);
                    table.CheckConstraint("ck_documentos_exigidos_base_legal_abrangencia", "abrangencia IN ('FEDERAL', 'ESTADUAL', 'MUNICIPAL', 'INTERNA_NORMA', 'INTERNA_EDITAL')");
                    table.CheckConstraint("ck_documentos_exigidos_base_legal_situacao", "situacao IN ('PENDENTE', 'RESOLVIDO')");
                    table.ForeignKey(
                        name: "fk_documentos_exigidos_base_legal_documento_exigido",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Bases legais das exigências de documentos.");

            migrationBuilder.CreateTable(
                name: "regras_de_comprovacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    processo_seletivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_que_contem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    forma = table.Column<string>(type: "text", nullable: false),
                    documento_exigido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantidade_minima = table.Column<int>(type: "integer", nullable: true),
                    consequencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    criterio_de_distincao = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    data_referencia = table.Column<DateOnly>(type: "date", nullable: true),
                    ocorrencias_esperadas = table.Column<string>(type: "jsonb", nullable: true),
                    grupo_para_repeticao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_de_comprovacao", x => x.id);
                    table.CheckConstraint("ck_regras_comprovacao_forma_coerente", "(forma = 'DOCUMENTO' AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NOT NULL AND consequencia IS NULL) OR (forma = 'TODOS_OS_ITENS' AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL AND criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (forma = 'QUANTIDADE_MINIMA_DE_ITENS' AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL AND criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL)");
                    table.CheckConstraint("ck_regras_de_comprovacao_criterio_de_distincao_coerente", "(criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (criterio_de_distincao IS NOT NULL AND criterio_de_distincao IN ('COMPETENCIA_MENSAL', 'EXERCICIO_ANUAL') AND data_referencia IS NOT NULL AND ocorrencias_esperadas IS NULL) OR (criterio_de_distincao IS NOT NULL AND criterio_de_distincao = 'OCORRENCIA' AND data_referencia IS NULL AND (ocorrencias_esperadas IS NULL OR jsonb_array_length(ocorrencias_esperadas) > 0))");
                    table.CheckConstraint("ck_regras_de_comprovacao_id_diferente_de_regra_que_contem_id", "id <> regra_que_contem_id");
                    table.CheckConstraint("ck_regras_de_comprovacao_ordem_nao_negativa", "ordem >= 0");
                    table.CheckConstraint("ck_regras_de_comprovacao_quantidade_minima_positiva", "quantidade_minima IS NULL OR quantidade_minima >= 1");
                    table.ForeignKey(
                        name: "fk_regras_comprovacao_grupo",
                        column: x => x.regra_que_contem_id,
                        principalSchema: "selecao",
                        principalTable: "regras_de_comprovacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_regras_de_comprovacao_documento_exigido",
                        column: x => x.documento_exigido_id,
                        principalSchema: "selecao",
                        principalTable: "documentos_exigidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_regras_de_comprovacao_processo_seletivo",
                        column: x => x.processo_seletivo_id,
                        principalSchema: "selecao",
                        principalTable: "processos_seletivos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "condicoes_de_exibicao_do_dado",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fato_coletado_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do dado do formulário ao qual a condição pertence."),
                    alternativa = table.Column<int>(type: "integer", nullable: false, comment: "Número da alternativa que reúne condições atendidas em conjunto."),
                    codigo_do_dado = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, comment: "Código do dado do candidato usado na condição. Exemplos: RENDA_PER_CAPITA, CONCORRER_RENDA."),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Comparação aplicada ao valor: ALGUM_DE, DIFERENTE, IGUAL, MAIOR_IGUAL, MENOR_IGUAL ou NENHUM_DE."),
                    valor = table.Column<string>(type: "jsonb", nullable: false, comment: "Valor de comparação da condição, em JSON."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicoes_de_exibicao_do_dado", x => x.id);
                    table.ForeignKey(
                        name: "fk_condicoes_de_exibicao_do_dado_dado_formulario",
                        column: x => x.fato_coletado_id,
                        principalSchema: "selecao",
                        principalTable: "dados_dos_formularios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Condições que determinam a exibição de um dado no formulário.");

            migrationBuilder.CreateTable(
                name: "avaliacoes_da_etapa",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da avaliação da inscrição na etapa."),
                    importacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da importação que originou a avaliação."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa do lote, repetida para ler a avaliação sem o lote; a chave estrangeira composta com o lote impede que divirja."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A inscrição avaliada, guardada por valor: quem não tem linha no arquivo do lote também tem avaliação, sem nota."),
                    oferta_curso_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A oferta de curso concorrida (origem da oferta no edital divulgado), guardada por valor e sem chave estrangeira: pertence à configuração do processo."),
                    grupo_area_enem_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "O código do grupo de área do ENEM cujos pesos apuraram a nota."),
                    situacao = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false, comment: "Códigos: APURADA, INSCRITO_SEM_LINHA, EM_CONFLITO."),
                    nota = table.Column<decimal>(type: "numeric", nullable: true, comment: "Σ(nota × peso) ÷ Σ(pesos) das cinco áreas, sem arredondar. Nulo quando não há nota. Não vai para log.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avaliacoes_da_etapa", x => x.id);
                    table.CheckConstraint("ck_avaliacoes_da_etapa_nota", "(situacao = 'APURADA') = (nota IS NOT NULL) AND (nota IS NULL OR nota BETWEEN 0 AND 1000)");
                    table.CheckConstraint("ck_avaliacoes_da_etapa_situacao", "situacao IN ('APURADA', 'INSCRITO_SEM_LINHA', 'EM_CONFLITO')");
                    table.ForeignKey(
                        name: "fk_avaliacoes_da_etapa_importacao_de_notas_do_enem",
                        columns: x => new { x.importacao_id, x.etapa_processo_id },
                        principalSchema: "selecao",
                        principalTable: "importacoes_de_notas_do_enem",
                        principalColumns: new[] { "id", "etapa_processo_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "A nota da etapa de uma inscrição numa oferta de curso concorrida (1ª ou 2ª opção), apurada pelos pesos do grupo de área do curso quando o lote foi importado. Os registros não são alterados ou excluídos; a avaliação vale com o lote. A nota por área está nos lançamentos do mesmo lote.");

            migrationBuilder.CreateTable(
                name: "inscritos_da_importacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do registro de inscrito na importação."),
                    importacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da importação à qual pertence o inscrito."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da inscrição com linha válida no arquivo."),
                    numero_de_inscricao_no_enem = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, comment: "O número de inscrição do INEP, guardado para a conferência de um recurso.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inscritos_da_importacao", x => x.id);
                    table.UniqueConstraint("ak_inscritos_da_importacao_importacao_inscricao", x => new { x.importacao_id, x.inscricao_id });
                    table.CheckConstraint("ck_inscritos_da_importacao_numero_enem", "numero_de_inscricao_no_enem ~ '^[0-9]{12}$'");
                    table.ForeignKey(
                        name: "fk_inscritos_da_importacao_importacao",
                        column: x => x.importacao_id,
                        principalSchema: "selecao",
                        principalTable: "importacoes_de_notas_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inscritos_da_importacao_inscricao",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "A inscrição com linha válida em um lote de notas e o número de inscrição atribuído pelo INEP. Os registros não são alterados ou excluídos.");

            migrationBuilder.CreateTable(
                name: "ocorrencias_da_importacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da ocorrência na importação."),
                    importacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da importação que registrou a ocorrência."),
                    tipo = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: false, comment: "Códigos: INSCRITO_SEM_LINHA, LINHA_SEM_INSCRITO, CONFLITO."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Identificador da inscrição afetada, quando encontrada."),
                    numeros_das_linhas = table.Column<int[]>(type: "integer[]", nullable: true, comment: "Os números das linhas do arquivo, a partir de 1."),
                    cpf_parcialmente_oculto = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true, comment: "O CPF mascarado da linha sem inscrito (***.999.999-**); nunca o CPF puro."),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "O nome que o arquivo traz para a linha sem inscrito."),
                    sexo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true, comment: "O sexo (F ou M) que o arquivo traz para a linha sem inscrito.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocorrencias_da_importacao", x => x.id);
                    table.CheckConstraint("ck_ocorrencias_da_importacao_forma", "(tipo = 'INSCRITO_SEM_LINHA' AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NULL) OR (tipo = 'LINHA_SEM_INSCRITO' AND inscricao_id IS NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) = 1) OR (tipo = 'CONFLITO' AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) >= 2)");
                    table.ForeignKey(
                        name: "fk_ocorrencias_da_importacao_importacao_de_notas_do_enem",
                        column: x => x.importacao_id,
                        principalSchema: "selecao",
                        principalTable: "importacoes_de_notas_do_enem",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ocorrencias_da_importacao_inscricao",
                        column: x => x.inscricao_id,
                        principalSchema: "selecao",
                        principalTable: "inscricoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "O que não virou nota ao importar um lote: inscrito sem linha, linha de CPF sem inscrito e inscrito em conflito. Nunca guarda o CPF.");

            migrationBuilder.CreateTable(
                name: "bases_da_permissao_concedida",
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
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bases_da_permissao_concedida", x => x.id);
                    table.CheckConstraint("ck_bases_da_permissao_origem", "origem IN ('MIGRACAO', 'SAIDA', 'LIBERACAO', 'CONCILIACAO')");
                    table.CheckConstraint("ck_bases_da_permissao_saida", "(origem = 'SAIDA') = (classificacao IS NULL AND de_unidade IS NULL)");
                    table.ForeignKey(
                        name: "fk_bases_da_permissao_concedida_concessao",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_da_revisao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decidido_como = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    resultado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    decidido_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decidido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_da_revisao", x => x.id);
                    table.CheckConstraint("ck_itens_da_revisao_decisao_completa", "(resultado IS NULL AND decidido_como IS NULL AND decidido_por_emissor IS NULL AND decidido_por_conta_id IS NULL AND decidido_em IS NULL) OR (resultado IS NOT NULL AND decidido_como IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_conta_id IS NOT NULL AND decidido_em IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_itens_da_revisao_concessoes_concessao_id",
                        column: x => x.concessao_id,
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_itens_da_revisao_revisoes_de_acessos_revisao_id",
                        column: x => x.revisao_id,
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_de_acessos",
                        principalColumn: "id",
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
                    decisor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: false),
                    lote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_no_historico = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_liberacoes_da_reclassificacao", x => x.id);
                    table.CheckConstraint("ck_liberacoes_da_reclassificacao_etapa", "etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS')");
                    table.CheckConstraint("ck_liberacoes_da_reclassificacao_motivo", "(resultado = 'MANTIDA') = (motivo IS NOT NULL) AND resultado IN ('LIBERADA', 'MANTIDA')");
                    table.ForeignKey(
                        name: "fk_liberacoes_da_reclassificacao_concessao",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_liberacoes_da_reclassificacao_reclassificacao_da_permissao",
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
                    revisao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    exige_protecao_de_dados = table.Column<bool>(type: "boolean", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_migracoes_da_concessao", x => x.id);
                    table.CheckConstraint("ck_migracoes_da_concessao_situacao", "situacao IN ('RETIDA', 'AGUARDANDO_PROTECAO_DE_DADOS', 'MIGRADA', 'RECUSADA', 'NAO_MIGRADA', 'FORA_DA_LISTA')");
                    table.ForeignKey(
                        name: "fk_migracoes_da_concessao_concessoes_perfil_id_concessao_id",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_migracoes_da_concessao_revisao_das_concessoes",
                        columns: x => new { x.revisao_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_das_concessoes",
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
                    solicitada_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitada_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    solicitada_por_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    desfecho_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    desfecho_por_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    desfecho_por_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    desfecho_por_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_do_desfecho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_providencias", x => x.id);
                    table.CheckConstraint("ck_providencias_desfecho_completo", "(estado = 'ABERTA' AND desfecho_em IS NULL AND desfecho_por_emissor IS NULL AND desfecho_por_conta_id IS NULL AND desfecho_por_nome IS NULL AND motivo_do_desfecho IS NULL) OR (estado <> 'ABERTA' AND desfecho_em IS NOT NULL AND desfecho_por_emissor IS NOT NULL AND desfecho_por_conta_id IS NOT NULL AND motivo_do_desfecho IS NOT NULL)");
                    table.CheckConstraint("ck_providencias_estado", "estado IN ('ABERTA', 'RESPONDIDA', 'ATENDIDA')");
                    table.CheckConstraint("ck_providencias_fundamento", "char_length(fundamento) BETWEEN 10 AND 2000");
                    table.CheckConstraint("ck_providencias_tipo", "tipo IN ('RETIRADA', 'REVISAO')");
                    table.ForeignKey(
                        name: "fk_providencias_concessoes_concessao_id",
                        column: x => x.concessao_id,
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_da_migracao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    envio_de_origem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    envio_de_origem_etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    envio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    envio_etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ator = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    decisor_tem_concessao = table.Column<bool>(type: "boolean", nullable: true),
                    versao_avaliada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    composicao_avaliada = table.Column<string[]>(type: "text[]", nullable: false),
                    acrescimo_avaliado = table.Column<string[]>(type: "text[]", nullable: false),
                    termina_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ordem_de_registro = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisoes_da_migracao", x => x.id);
                    table.CheckConstraint("ck_decisoes_da_migracao_envio", "(envio_id IS NULL) = (envio_etapa IS NULL) AND (envio_etapa IS NULL OR envio_etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS'))");
                    table.CheckConstraint("ck_decisoes_da_migracao_envio_de_origem", "(envio_de_origem_id IS NULL) = (envio_de_origem_etapa IS NULL) AND (envio_de_origem_etapa IS NULL OR envio_de_origem_etapa = 'GESTAO_DE_ACESSOS')");
                    table.CheckConstraint("ck_decisoes_da_migracao_ramo", "(ator = 'HUMANO' AND etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS') AND decisao IN ('MIGRAR', 'RECUSAR') AND decisor_emissor IS NOT NULL AND decisor_conta_id IS NOT NULL AND decisor_tem_concessao IS NOT NULL AND envio_de_origem_id IS NOT NULL AND (envio_etapa IS NULL OR envio_etapa = etapa) AND (etapa <> 'PROTECAO_DE_DADOS' OR envio_id IS NOT NULL) AND (etapa <> 'GESTAO_DE_ACESSOS' OR envio_id IS NULL OR envio_id = envio_de_origem_id)) OR (ator = 'SISTEMA' AND decisor_emissor IS NULL AND decisor_conta_id IS NULL AND decisor_nome IS NULL AND decisor_tem_concessao IS NULL AND envio_id IS NULL AND ((etapa = 'GESTAO_DE_ACESSOS' AND decisao = 'RETER' AND envio_de_origem_id IS NOT NULL) OR (etapa = 'SISTEMA' AND decisao IN ('NAO_MIGRADA', 'FORA_DA_LISTA'))))");
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_concessoes_perfil_id_concessao_id",
                        columns: x => new { x.perfil_id, x.concessao_id },
                        principalSchema: "autorizacao",
                        principalTable: "concessoes",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_envio",
                        columns: x => new { x.revisao_id, x.envio_id, x.envio_etapa },
                        principalSchema: "autorizacao",
                        principalTable: "envios_de_decisoes",
                        principalColumns: new[] { "revisao_id", "id", "etapa" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_envio_de_origem",
                        columns: x => new { x.revisao_id, x.envio_de_origem_id, x.envio_de_origem_etapa },
                        principalSchema: "autorizacao",
                        principalTable: "envios_de_decisoes",
                        principalColumns: new[] { "revisao_id", "id", "etapa" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_revisao_das_concessoes",
                        columns: x => new { x.revisao_id, x.perfil_id },
                        principalSchema: "autorizacao",
                        principalTable: "revisoes_das_concessoes",
                        principalColumns: new[] { "id", "perfil_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decisoes_da_migracao_versao_do_perfil_avaliada",
                        columns: x => new { x.perfil_id, x.versao_avaliada_id },
                        principalSchema: "autorizacao",
                        principalTable: "versoes_do_perfil",
                        principalColumns: new[] { "perfil_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bases_legais_da_comprovacao",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    regra_de_comprovacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    abrangencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bases_legais_da_comprovacao", x => x.id);
                    table.CheckConstraint("ck_bases_legais_da_comprovacao_abrangencia", "abrangencia IN ('FEDERAL', 'ESTADUAL', 'MUNICIPAL', 'INTERNA_NORMA', 'INTERNA_EDITAL')");
                    table.CheckConstraint("ck_bases_legais_da_comprovacao_situacao", "situacao IN ('PENDENTE', 'RESOLVIDO')");
                    table.ForeignKey(
                        name: "fk_bases_legais_comprovacao_regra",
                        column: x => x.regra_de_comprovacao_id,
                        principalSchema: "selecao",
                        principalTable: "regras_de_comprovacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lancamentos_de_nota",
                schema: "selecao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do lançamento da nota."),
                    importacao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da importação que originou o lançamento."),
                    inscricao_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador da inscrição à qual pertence a nota."),
                    etapa_processo_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "A etapa do lote, repetida para ler o lançamento sem o lote; a chave estrangeira composta com o lote impede que divirja."),
                    item_codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "Área do ENEM: REDACAO, CIENCIAS_DA_NATUREZA, CIENCIAS_HUMANAS, LINGUAGENS ou MATEMATICA."),
                    nota = table.Column<decimal>(type: "numeric(7,3)", precision: 7, scale: 3, nullable: false, comment: "Nota da área do ENEM, de zero a mil, com até três casas decimais."),
                    situacao = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, comment: "Códigos: PRESENTE, AUSENTE.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lancamentos_de_nota", x => x.id);
                    table.CheckConstraint("ck_lancamentos_de_nota_nota", "nota BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_lancamentos_de_nota_situacao", "situacao IN ('PRESENTE', 'AUSENTE')");
                    table.ForeignKey(
                        name: "fk_lancamentos_de_nota_importacao_de_notas_do_enem",
                        columns: x => new { x.importacao_id, x.etapa_processo_id },
                        principalSchema: "selecao",
                        principalTable: "importacoes_de_notas_do_enem",
                        principalColumns: new[] { "id", "etapa_processo_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lancamentos_de_nota_inscrito_da_importacao",
                        columns: x => new { x.importacao_id, x.inscricao_id },
                        principalSchema: "selecao",
                        principalTable: "inscritos_da_importacao",
                        principalColumns: new[] { "importacao_id", "inscricao_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Uma nota de uma inscrição em uma área do ENEM, com sua situação. Os registros não são alterados ou excluídos; a correção exige importar outro lote.");

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
                    pessoa_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pessoa_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aceito_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceites_do_termo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anexos_do_pedido",
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anexos_do_pedido", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "decisoes_do_pedido",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decisor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    decisor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("pk_decisoes_do_pedido", x => x.id);
                    table.CheckConstraint("ck_decisoes_do_pedido_motivo_na_recusa", "resultado <> 'RECUSADO' OR motivo IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "leituras_do_anexo_do_pedido",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anexo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leitor_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    leitor_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    instante = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leituras_do_anexo_do_pedido", x => x.id);
                    table.ForeignKey(
                        name: "fk_leituras_do_anexo_do_pedido_anexos_do_pedido_anexo_id",
                        column: x => x.anexo_id,
                        principalSchema: "autorizacao",
                        principalTable: "anexos_do_pedido",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidos_de_concessao",
                schema: "autorizacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitante_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    solicitante_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    solicitante_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    beneficiario_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    beneficiario_emissor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    beneficiario_aceitou_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    justificativa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    valida_ate_sugerida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    anexara_documento = table.Column<bool>(type: "boolean", nullable: false),
                    situacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valida_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concessao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termo_versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termo_numero = table.Column<int>(type: "integer", nullable: true),
                    termo_resumo = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    versao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_de_concessao", x => x.id);
                    table.CheckConstraint("ck_pedidos_beneficiario_completo", "(beneficiario_emissor IS NULL) = (beneficiario_conta_id IS NULL)");
                    table.CheckConstraint("ck_pedidos_concessao_so_aprovado", "concessao_id IS NULL OR situacao = 'APROVADO'");
                    table.CheckConstraint("ck_pedidos_decisao_com_beneficiario", "situacao IN ('AGUARDANDO_DOCUMENTO', 'AGUARDANDO_BENEFICIARIO', 'CANCELADO', 'DEVOLVIDO') OR beneficiario_conta_id IS NOT NULL");
                    table.CheckConstraint("ck_pedidos_encerramento", "(situacao IN ('APROVADO', 'RECUSADO', 'CANCELADO', 'DEVOLVIDO')) = (encerrado_em IS NOT NULL)");
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
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, comment: "Versão da linha mantida pelo PostgreSQL para controle de alterações concorrentes."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora de criação do registro."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Data e hora da última alteração, quando houver.")
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
                    autor_conta_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    table.CheckConstraint("ck_termos_na_instalacao_versao", "(situacao = 'SEM_TERMO_PUBLICADO') = (versao_do_termo_id IS NULL) AND (versao_do_termo_id IS NULL) = (numero IS NULL)");
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
                table: "categorias_de_documento",
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
                table: "dados_do_candidato",
                columns: new[] { "id", "ativo", "fonte_do_valor", "quantidade_de_valores", "classificacao_protecao", "codigo", "componente_exibicao", "created_at", "created_by", "dependencias", "descricao", "tipo", "escopo", "finalidade_tratamento", "fonte_das_opcoes", "formato", "hipotese_legal", "nome", "origem", "fase_de_disponibilidade", "regras_padrao", "semeado_pelo_sistema", "updated_at", "updated_by", "protecao_aceita_em", "protecao_aceita_por", "protecao_ciclo", "protecao_recomendada", "protecao_decidida_em", "protecao_decidida_por", "protecao_justificativa", "protecao_manifestacao_desfavoravel", "protecao_manifestacoes_no_ciclo", "protecao_manifestada_em", "protecao_situacao", "protecao_solicitada_em", "protecao_solicitada_por" },
                values: new object[,]
                {
                    { new Guid("fa700000-0000-7000-8000-000000000001"), true, "CAMPO_FORMULARIO:COR_RACA", "VALOR_UNICO", "SENSIVEL", "COR_RACA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Cor ou raça que o candidato declara. É o dado de que depende a concorrência às vagas reservadas a pretos, pardos e indígenas.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Cor ou raça", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000002"), true, "CAMPO_FORMULARIO:QUILOMBOLA", "VALOR_UNICO", "SENSIVEL", "QUILOMBOLA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato se declara quilombola. É o dado de que depende a concorrência às vagas reservadas a quilombolas.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Quilombola", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000003"), true, "CAMPO_FORMULARIO:PCD", "VALOR_UNICO", "SENSIVEL", "PCD", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato declara ser pessoa com deficiência. É o dado de que depende a concorrência às vagas reservadas a pessoas com deficiência.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Pessoa com deficiência", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000004"), true, "REGRA_DERIVACAO:EGRESSO_ESCOLA_PUBLICA", "VALOR_UNICO", "PESSOAL", "EGRESSO_ESCOLA_PUBLICA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato cursou o ensino médio em escola pública. O sistema o calcula a partir das duas respostas sobre a origem escolar, pela regra da Lei nº 12.711/2012.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Egresso de escola pública", "CALCULADO", "INSCRICAO", "[{\"contribui\":null,\"quando\":[[{\"fato\":\"ONDE_CURSOU_EM\",\"operador\":\"ALGUM_DE\",\"valor\":[\"SOMENTE_PUBLICA\",\"COMUNITARIA_CAMPO\",\"PUBLICA_E_COMUNITARIA\"]}],[{\"fato\":\"ONDE_CURSOU_EM\",\"operador\":\"IGUAL\",\"valor\":\"CERTIFICACAO\"},{\"fato\":\"FORMA_CONCLUSAO_EM\",\"operador\":\"ALGUM_DE\",\"valor\":[\"ENCCEJA\",\"PROFICIENCIA\",\"ENEM\"]}]]}]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000005"), true, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", "VALOR_UNICO", "PESSOAL", "RENDA_PER_CAPITA", "MOEDA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Renda mensal da família do candidato dividida pelo número de integrantes da composição familiar, em reais. Define o enquadramento nas vagas reservadas por renda.", "NUMERICO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita", "CALCULADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000006"), true, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", "VALOR_UNICO", "PESSOAL", "FAIXA_ETARIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new[] { "DATA_NASCIMENTO" }, "Idade em anos completos do candidato na data de referência dos fatos do processo, calculada pelo sistema a partir da data de nascimento, para verificar os requisitos de participação.", "NUMERICO", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Idade", "CALCULADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000007"), true, "CAMPO_FORMULARIO:SEXO", "VALOR_UNICO", "PESSOAL", "SEXO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Sexo biológico do candidato, conforme ele declara na inscrição.", "OPCAO", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sexo biológico", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000008"), true, "REGRA_DERIVACAO:MODALIDADE", "VARIOS_VALORES", "INTERNO", "MODALIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Modalidades de concorrência em que o candidato concorre, resultantes das regras de reserva de vagas do processo aplicadas às respostas dele.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "MODALIDADE", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Modalidade de concorrência", "CALCULADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000009"), true, "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO", "VARIOS_VALORES", "SENSIVEL", "CONDICAO_ATENDIMENTO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Condições do candidato pelas quais ele pede atendimento especializado nas etapas do processo.", "OPCAO", "CANDIDATO", "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.", "OPCOES_DO_PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Condição de atendimento especializado", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000010"), true, "CAMPO_FORMULARIO:NACIONALIDADE", "VALOR_UNICO", "PESSOAL", "NACIONALIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nacionalidade do candidato, que define os documentos de identificação que ele informa.", "OPCAO", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nacionalidade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000011"), true, "CAMPO_FORMULARIO:TIPO_DEFICIENCIA", "VALOR_UNICO", "SENSIVEL", "TIPO_DEFICIENCIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Tipo de deficiência do candidato com deficiência, entre os do cadastro de tipos de deficiência.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "TIPOS_DE_DEFICIENCIA", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Tipo de deficiência", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000012"), true, "CAMPO_FORMULARIO:BAIXA_RENDA", "VALOR_UNICO", "PESSOAL", "BAIXA_RENDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Autodeclaração de que a renda familiar per capita é igual ou inferior a um salário mínimo, feita na inscrição para as vagas reservadas por renda.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita igual ou inferior a um salário mínimo", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000013"), true, "CAMPO_FORMULARIO:CONCORRER_PCD", "VALOR_UNICO", "INTERNO", "CONCORRER_PCD", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a pessoas com deficiência.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a pessoas com deficiência", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000014"), true, "CAMPO_FORMULARIO:CONCORRER_EP", "VALOR_UNICO", "INTERNO", "CONCORRER_EP", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a egressos de escola pública.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a egressos de escola pública", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000015"), true, "CAMPO_FORMULARIO:CONCORRER_PPI", "VALOR_UNICO", "INTERNO", "CONCORRER_PPI", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a pretos, pardos e indígenas.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a pretos, pardos e indígenas", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000016"), true, "CAMPO_FORMULARIO:CONCORRER_Q", "VALOR_UNICO", "INTERNO", "CONCORRER_Q", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas a quilombolas.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas a quilombolas", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000017"), true, "CAMPO_FORMULARIO:CONCORRER_RENDA", "VALOR_UNICO", "INTERNO", "CONCORRER_RENDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas reservadas por renda familiar per capita.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas reservadas por renda familiar per capita", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000018"), true, "CAMPO_FORMULARIO:ENDERECO_RESIDENCIAL", "VALOR_UNICO", "PESSOAL", "ENDERECO_RESIDENCIAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Endereço em que o candidato reside, usado para verificar os requisitos regionais do processo.", "ENDERECO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Endereço residencial", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000019"), true, "CAMPO_FORMULARIO:DATA_NASCIMENTO", "VALOR_UNICO", "PESSOAL", "DATA_NASCIMENTO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Data de nascimento do candidato, usada para verificar a idade e os requisitos de participação.", "DATA", "CANDIDATO", "Verificação dos requisitos de participação e das exigências documentais do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Data de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000020"), true, "ATRIBUTO_CANDIDATO:UF_RESIDENCIA", "VALOR_UNICO", "PESSOAL", "UF_RESIDENCIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new[] { "ENDERECO_RESIDENCIAL" }, "Unidade da Federação em que o candidato reside, derivada do endereço residencial.", "OPCAO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "UNIDADES_FEDERATIVAS", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de residência", "CALCULADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000021"), true, "ATRIBUTO_CANDIDATO:MUNICIPIO_RESIDENCIA", "VALOR_UNICO", "PESSOAL", "MUNICIPIO_RESIDENCIA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new[] { "ENDERECO_RESIDENCIAL" }, "Município em que o candidato reside, derivado do endereço residencial.", "OPCAO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "MUNICIPIOS", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Município de residência", "CALCULADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000022"), true, "CLASSIFICACAO:MODALIDADE_CONVOCACAO", "VALOR_UNICO", "INTERNO", "MODALIDADE_CONVOCACAO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Grupo de vagas em que o candidato foi convocado, conhecido só depois do resultado final. Os documentos da habilitação seguem a convocação.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "MODALIDADE", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Modalidade da convocação", "CALCULADO", "RESULTADO_FINAL", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000023"), true, "CAMPO_FORMULARIO:MAIOR_IDADE", "VALOR_UNICO", "PESSOAL", "MAIOR_IDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o membro da composição familiar é maior de idade.", "SIM_OU_NAO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Maior de idade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000024"), true, "CAMPO_FORMULARIO:SEM_RENDA", "VALOR_UNICO", "PESSOAL", "SEM_RENDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o membro da composição familiar não tem renda.", "SIM_OU_NAO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sem renda", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000025"), true, "CAMPO_FORMULARIO:SOB_GUARDA", "VALOR_UNICO", "PESSOAL", "SOB_GUARDA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o membro da composição familiar está sob guarda.", "SIM_OU_NAO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sob guarda", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000026"), true, "CAMPO_FORMULARIO:PARENTESCO", "VALOR_UNICO", "PESSOAL", "PARENTESCO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Parentesco do membro da composição familiar com o candidato.", "OPCAO", "MEMBRO_GRUPO", "Comprovação documental dos membros da composição familiar do candidato no processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Parentesco", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000027"), true, "CAMPO_FORMULARIO:NOME", "VALOR_UNICO", "PESSOAL", "NOME", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome civil do candidato, como vem do login gov.br.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000028"), true, "CAMPO_FORMULARIO:DESEJA_NOME_SOCIAL", "VALOR_UNICO", "PESSOAL", "DESEJA_NOME_SOCIAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato deseja ser identificado pelo nome social no processo seletivo.", "SIM_OU_NAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja usar nome social", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000029"), true, "CAMPO_FORMULARIO:NOME_SOCIAL", "VALOR_UNICO", "PESSOAL", "NOME_SOCIAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome pelo qual o candidato prefere ser identificado e que aparece no lugar do nome civil.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome social", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000030"), true, "CAMPO_FORMULARIO:CPF", "VALOR_UNICO", "IDENTIFICADOR", "CPF", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do CPF do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "CPF", "CUMPRIMENTO_OBRIGACAO_LEGAL", "CPF", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000031"), true, "CAMPO_FORMULARIO:RG_NUMERO", "VALOR_UNICO", "IDENTIFICADOR", "RG_NUMERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do RG do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000032"), true, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR", "VALOR_UNICO", "PESSOAL", "RG_ORGAO_EMISSOR", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Órgão que emitiu o RG do candidato, entre os de uma lista fechada.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Órgão emissor do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000033"), true, "CAMPO_FORMULARIO:RG_DATA_EMISSAO", "VALOR_UNICO", "PESSOAL", "RG_DATA_EMISSAO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Data em que o RG do candidato foi emitido.", "DATA", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Data de emissão do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000036"), true, "CAMPO_FORMULARIO:NOME_MAE", "VALOR_UNICO", "PESSOAL", "NOME_MAE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome da mãe do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome da mãe", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000037"), true, "CAMPO_FORMULARIO:NOME_PAI", "VALOR_UNICO", "PESSOAL", "NOME_PAI", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome do pai do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "NOME_PESSOA", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome do pai", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000038"), true, "CAMPO_FORMULARIO:ESTADO_CIVIL", "VALOR_UNICO", "PESSOAL", "ESTADO_CIVIL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Estado civil do candidato.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Estado civil", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000039"), true, "CAMPO_FORMULARIO:EMAIL", "VALOR_UNICO", "PESSOAL", "EMAIL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Endereço de e-mail para comunicação com o candidato.", "TEXTO", "CANDIDATO", "Comunicação com o candidato sobre o processo seletivo.", null, "EMAIL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "E-mail", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000040"), true, "CAMPO_FORMULARIO:TELEFONE", "VALOR_UNICO", "PESSOAL", "TELEFONE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Telefone, com DDD, para comunicação com o candidato.", "TEXTO", "CANDIDATO", "Comunicação com o candidato sobre o processo seletivo.", null, "TELEFONE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Telefone", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000041"), true, "CAMPO_FORMULARIO:RG_UF", "VALOR_UNICO", "PESSOAL", "RG_UF", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Unidade da Federação que emitiu o RG do candidato.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "UNIDADES_FEDERATIVAS", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de emissão do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000042"), true, "CAMPO_FORMULARIO:NATURALIDADE_UF", "VALOR_UNICO", "PESSOAL", "NATURALIDADE_UF", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Unidade da Federação em que o candidato nasceu.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "UNIDADES_FEDERATIVAS", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "UF de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000043"), true, "CAMPO_FORMULARIO:NATURALIDADE_MUNICIPIO", "VALOR_UNICO", "PESSOAL", "NATURALIDADE_MUNICIPIO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Município em que o candidato nasceu.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "MUNICIPIOS", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Município de nascimento", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000044"), true, "CAMPO_FORMULARIO:TIPO_ENDERECO", "VALOR_UNICO", "PESSOAL", "TIPO_ENDERECO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Tipo de localidade em que o candidato reside, perguntado antes do endereço.", "OPCAO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Tipo de localidade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000045"), true, "CAMPO_FORMULARIO:NOME_COMUNIDADE", "VALOR_UNICO", "PESSOAL", "NOME_COMUNIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Nome da aldeia, da comunidade tradicional ou do quilombo em que o candidato reside.", "TEXTO", "CANDIDATO", "Verificação da residência do candidato para os requisitos regionais e o bônus regional do processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome da aldeia, comunidade ou quilombo", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000046"), true, "CAMPO_FORMULARIO:FORMA_CONCLUSAO_EM", "VALOR_UNICO", "PESSOAL", "FORMA_CONCLUSAO_EM", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Como o candidato concluiu o ensino médio: curso regular, Educação de Jovens e Adultos, ENCCEJA, exame de proficiência ou ENEM.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Forma de conclusão do ensino médio", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000047"), true, "CAMPO_FORMULARIO:ONDE_CURSOU_EM", "VALOR_UNICO", "PESSOAL", "ONDE_CURSOU_EM", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Em que rede de ensino o candidato cursou o ensino médio, ou se o concluiu por certificação sem frequentá-lo.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Onde cursou o ensino médio", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000048"), true, "CAMPO_FORMULARIO:RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", "VALOR_UNICO", "PESSOAL", "RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se a renda familiar per capita do candidato é igual ou inferior a um salário mínimo e meio, como ele declara no pedido de isenção da taxa.", "SIM_OU_NAO", "CANDIDATO", "Análise do pedido de isenção da taxa de inscrição do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Renda familiar per capita de até um salário mínimo e meio", "DECLARADO", "SOLICITACAO_ISENCAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000049"), true, "CAMPO_FORMULARIO:EDICAO_ENEM", "VALOR_UNICO", "PESSOAL", "EDICAO_ENEM", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Edição do ENEM cujas notas são importadas para a classificação do candidato.", "OPCAO", "CANDIDATO", "Importação das notas do ENEM da edição escolhida pelo candidato, na classificação do processo seletivo.", "OPCOES_DO_PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Edição do ENEM", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000050"), true, "CAMPO_FORMULARIO:INDIGENA", "VALOR_UNICO", "SENSIVEL", "INDIGENA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato se declara indígena. A condição exige declaração de pertencimento e passa só por análise de documentos.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Indígena", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000051"), true, "CAMPO_FORMULARIO:PASSAPORTE_NUMERO", "VALOR_UNICO", "IDENTIFICADOR", "PASSAPORTE_NUMERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do passaporte do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do passaporte", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000052"), true, "CAMPO_FORMULARIO:RNM_NUMERO", "VALOR_UNICO", "IDENTIFICADOR", "RNM_NUMERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Número do Registro Nacional Migratório (RNM) do candidato.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Número do Registro Nacional Migratório (RNM)", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000053"), true, "CAMPO_FORMULARIO:RECURSO_ACESSIBILIDADE", "VARIOS_VALORES", "SENSIVEL", "RECURSO_ACESSIBILIDADE", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Recursos de acessibilidade de que o candidato precisa nas etapas, entre os que o processo oferece.", "OPCAO", "CANDIDATO", "Oferta de atendimento especializado ao candidato na realização das etapas do processo seletivo.", "OPCOES_DO_PROCESSO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Recurso de acessibilidade", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000054"), true, "REGRA_DERIVACAO:OPTOU_PELAS_COTAS_DA_LEI", "VALOR_UNICO", "PESSOAL", "OPTOU_PELAS_COTAS_DA_LEI", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Se o candidato é egresso de escola pública e optou por concorrer às vagas de escola pública.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Optou pelas cotas da Lei nº 12.711/2012", "CALCULADO", "INSCRICAO", "[{\"contribui\":null,\"quando\":[[{\"fato\":\"EGRESSO_ESCOLA_PUBLICA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_EP\",\"operador\":\"IGUAL\",\"valor\":true}]]}]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000055"), true, "CAMPO_FORMULARIO:CONCORRER_VAGA_POR_ACRESCIMO", "VALOR_UNICO", "INTERNO", "CONCORRER_VAGA_POR_ACRESCIMO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Opção do candidato por concorrer às vagas por acréscimo para indígenas e quilombolas.", "SIM_OU_NAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", null, null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Deseja concorrer às vagas por acréscimo para indígenas e quilombolas", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000056"), true, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR_OUTRO", "VALOR_UNICO", "PESSOAL", "RG_ORGAO_EMISSOR_OUTRO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "O órgão emissor do RG que não está na lista.", "TEXTO", "CANDIDATO", "Identificação do candidato no processo seletivo.", null, "LIVRE", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Nome do órgão emissor do RG", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000057"), true, "CAMPO_FORMULARIO:SEXO_CIVIL", "VALOR_UNICO", "PESSOAL", "SEXO_CIVIL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Sexo que consta do registro civil do candidato.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Sexo civil", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000058"), true, "CAMPO_FORMULARIO:IDENTIDADE_GENERO", "VALOR_UNICO", "SENSIVEL", "IDENTIDADE_GENERO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Identidade de gênero que o candidato declara.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Identidade de gênero", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000059"), true, "CAMPO_FORMULARIO:ORIENTACAO_SEXUAL", "VALOR_UNICO", "SENSIVEL", "ORIENTACAO_SEXUAL", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Orientação sexual que o candidato declara.", "OPCAO", "CANDIDATO", "Identificação do candidato no processo seletivo.", "CATALOGO", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Orientação sexual", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000060"), true, "ATRIBUTO_CANDIDATO:MODALIDADE_SEGUNDA_OPCAO", "VARIOS_VALORES", "INTERNO", "MODALIDADE_SEGUNDA_OPCAO", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Modalidades de concorrência do candidato na segunda opção de curso, que tem distribuição de vagas própria.", "OPCAO", "CANDIDATO", "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.", "MODALIDADE", null, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Modalidade de concorrência da 2ª opção de curso", "CALCULADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null },
                    { new Guid("fa700000-0000-7000-8000-000000000061"), true, "CAMPO_FORMULARIO:CIDADE_DE_PROVA", "VALOR_UNICO", "PESSOAL", "CIDADE_DE_PROVA", null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new string[0], "Cidade de prova escolhida pelo candidato entre as cidades de aplicação da prova.", "OPCAO", "CANDIDATO", "Organização da aplicação da prova presencial do processo seletivo.", "MUNICIPIOS_PROVA_PRESENCIAL", null, "EXECUCAO_POLITICAS_PUBLICAS", "Cidade de prova", "DECLARADO", "INSCRICAO", "[]", true, null, null, null, null, 1, null, null, null, null, false, 0, null, "PENDENTE", null, null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "modalidades",
                columns: new[] { "id", "acao_quando_indeferido", "base_legal", "codigo", "composicao_origem", "composicao_vagas", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "natureza_legal", "regra_de_aplicabilidade", "regra_remanejamento", "updated_at", "updated_by", "remanejamento_destino", "remanejamento_destino_final", "remanejamento_par" },
                values: new object[,]
                {
                    { new Guid("70da1000-0000-7000-8000-000000000001"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "AC", null, "RESTANTE_DAS_VAGAS_OFERTADAS", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Ampla concorrência", false, "AMPLA", "[]", null, null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000002"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_PPI", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, preto/pardo/indígena", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"ALGUM_DE\",\"valor\":[\"PRETA\",\"PARDA\"]},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}],[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"IGUAL\",\"valor\":\"INDIGENA\"},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000003"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_Q", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, quilombola", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_Q\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000004"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_PCD", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, pessoa com deficiência", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000005"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LB_EP", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — baixa renda, egresso de escola pública", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"BAIXA_RENDA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_RENDA\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000006"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_PPI", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, preto/pardo/indígena", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"ALGUM_DE\",\"valor\":[\"PRETA\",\"PARDA\"]},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true}],[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"COR_RACA\",\"operador\":\"IGUAL\",\"valor\":\"INDIGENA\"},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PPI\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000007"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_Q", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, quilombola", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_Q\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000008"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_PCD", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, pessoa com deficiência", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000009"), null, "Lei 12.711/2012 (red. Lei 14.723/2023)", "LI_EP", null, "DENTRO_DA_RESERVA_LEGAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Cota — independente de renda, egresso de escola pública", false, "COTA_RESERVADA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":true}]]", "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000010"), null, "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)", "AC_PCD", "AC", "RETIRADA_DE_OUTRA_MODALIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Ampla Concorrência – Pessoa com Deficiência (V)", false, "ACAO_AFIRMATIVA", "[[{\"fato\":\"PCD\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_PCD\",\"operador\":\"IGUAL\",\"valor\":true}]]", "DESTINO_UNICO", null, null, "AC", null, null },
                    { new Guid("70da1000-0000-7000-8000-000000000012"), null, "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "AC_I", null, "SUPLEMENTAR_AO_TOTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Vaga por acréscimo — candidato indígena (PSIQ)", false, "ACAO_AFIRMATIVA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"INDIGENA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_VAGA_POR_ACRESCIMO\",\"operador\":\"IGUAL\",\"valor\":true}]]", "CRUZADO", null, null, null, null, "AC_Q" },
                    { new Guid("70da1000-0000-7000-8000-000000000013"), null, "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "AC_Q", null, "SUPLEMENTAR_AO_TOTAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Vaga por acréscimo — candidato quilombola (PSIQ)", false, "ACAO_AFIRMATIVA", "[[{\"fato\":\"OPTOU_PELAS_COTAS_DA_LEI\",\"operador\":\"IGUAL\",\"valor\":false},{\"fato\":\"QUILOMBOLA\",\"operador\":\"IGUAL\",\"valor\":true},{\"fato\":\"CONCORRER_VAGA_POR_ACRESCIMO\",\"operador\":\"IGUAL\",\"valor\":true}]]", "CRUZADO", null, null, null, null, "AC_I" }
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "perfis",
                columns: new[] { "id", "ativo", "created_at", "descricao", "governanca", "nome", "updated_at" },
                values: new object[,]
                {
                    { new Guid("019a0000-0000-7000-8000-000000000001"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Opera as concessões de acesso, mantém os perfis de acesso e publica o termo de responsabilidade.", "GESTOR_DE_ACESSOS", "Gestor de acessos", null },
                    { new Guid("019a0000-0000-7000-8000-000000000002"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Decide, em nome da Unifesspa, a segunda etapa dos pedidos com dado pessoal, as versões protegidas de perfil, a revisão das concessões e a proteção do catálogo, e conduz a revisão periódica dos acessos.", "REPRESENTANTE_DO_CONTROLADOR", "Representante do controlador", null },
                    { new Guid("019a0000-0000-7000-8000-000000000003"), true, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Acompanha o tratamento de dados pessoais na gestão de acessos: consulta pedidos, concessões, revisões, versões de perfil, revisão das concessões e catálogo, registra manifestação técnica na proteção do catálogo e solicita providências sobre concessões, sem aprovar, recusar nem revogar.", "GESTOR_DE_PROTECAO_DE_DADOS", "Gestor de proteção de dados", null }
                });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "precedencias_entre_fases",
                columns: new[] { "id", "antecessora_codigo", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "sucessora_codigo", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("93ec0000-0000-7000-8000-000000000001"), "INSCRICAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HOMOLOGACAO_INSCRICOES", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000004"), "RESULTADO_FINAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HABILITACAO", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000005"), "HABILITACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "MATRICULA", null, null },
                    { new Guid("93ec0000-0000-7000-8000-000000000006"), "HETEROIDENTIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "HOMOLOGACAO_RESULTADO_FINAL", null, null }
                });

            migrationBuilder.InsertData(
                schema: "selecao",
                table: "regras_do_catalogo",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "declaracao_da_regra", "hash", "invariantes", "tipo", "updated_at", "versao" },
                values: new object[,]
                {
                    { new Guid("d0a00000-0000-7000-8000-000000000001"), "Proposta da CEPS; média ponderada NOTA=Σ(nota×peso)/Σpeso", "FORMULA_MEDIA_PONDERADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"fonte_pesos\":[\"etapa\",\"peso_area_enem\"]}", "a9164af9b08bf28cce8a24d21b90681dad2453a958ee35385914a47bc89bc293", "[\"divisor = Σ(peso das etapas classificatória∪ambas)\"]", "REGRA_CALCULO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000002"), "Portaria MEC 18/2012 art. 16 — SiSU federal (CEPS não calcula)", "CLASSIFICACAO_IMPORTADA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "71f33ea317f8fbbac69ae71147dc8525d3b5f73a6d4533b291aafc273fab82a7", "[\"sem cálculo local; classificação federal por importação da lista de classificação\"]", "REGRA_CALCULO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000003"), "Decisão da CEPS: truncamento em duas casas, padrão", "PRECISAO_TRUNCAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "a054a8e682c4e045dc68d068f821d000bfbf8e5b8a8060a2913594aa9ff830bf", "[\"trunca na N-ésima casa, sem arredondar\"]", "REGRA_ARREDONDAMENTO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000004"), "Reprodução de editais antigos (PSE/Convênios)", "PRECISAO_ARREDONDAR_A_PARTIR_DE_CINCO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "c12be79030a20b9d164d3cc5e6039d01b752467d68c80e55f1f91a4f22debcb5", "[\"arredonda p/ cima se 3ª casa ≥ 5\"]", "REGRA_ARREDONDAMENTO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000005"), "Edital por processo (nota mínima eliminatória)", "ELIMINACAO_NOTA_MINIMA_ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "f476fd544e77be10c8c0260b5e187c9f6dcedd090edf853f5461db2e3357b350", "[\"nota < mínima na etapa → elimina\"]", "REGRA_ELIMINACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000006"), "Res. 805/2024 art. 5º e Anexo I (ponto de corte por área do ENEM; Redação = 400)", "ELIMINACAO_CORTE_EM_AREA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "51f955af938952330e249040a54e61f311b217bb265cf9875e6f06dfeefc9813", "[\"nota na área < mínimo → elimina\",\"no máximo um corte por área\"]", "REGRA_ELIMINACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000007"), "Res. 805/2024 art. 5º (zero em qualquer área elimina)", "ELIMINACAO_ZERO_EM_AREA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "4b10a49dc7c34b3cafa6cd5e29b72bab6c7c8be9abb5731a2f02f800ecbb2137", "[\"nota zero em qualquer área do ENEM → elimina\"]", "REGRA_ELIMINACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000008"), "Fator de Atenuação das Desigualdades de Acesso (FADA) multiplicativo aplicado à nota final após os pesos das áreas do ENEM, sem teto por padrão", "FADA_MULTIPLICATIVO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "0c90842b3a25f21bd5e692d1e48f0a0ed697260a174f5ce40fe8f9344eea51c5", "[\"nota final multiplicada pelo fator, após os pesos; teto opcional\"]", "REGRA_FADA", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000009"), "Lei 10.741/2003 art. 27 (Estatuto do Idoso)", "DESEMPATE_IDOSO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "afadf230757d399e72dcd5fe93e89f4200cc9a93461896a7173d90c794736387", "[\"prioriza quem tem idade igual ou superior à idade mínima declarada no critério\"]", "CRITERIO_DESEMPATE", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000010"), "Edital (ordem de desempate)", "DESEMPATE_MAIOR_NOTA_ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "b216a196dac420ae81e1a508f719d5f6ebd32e773abf97de30c17cce65dbe27a", "[\"ordena por maior nota da etapa indicada\"]", "CRITERIO_DESEMPATE", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000011"), "Edital (maior idade cronológica)", "DESEMPATE_MAIOR_IDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "3eabafb58968b36361f9779191bbaca906af27acb04d6b008b3141c99d7944b0", "[\"ordena por data de nascimento (nascido mais cedo vence)\"]", "CRITERIO_DESEMPATE", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000012"), "Edital (critério adicional por dado do candidato — ex.: professor rural)", "DESEMPATE_CONDICAO_SOBRE_DADO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "6da800f55f2e9634a0e8fe168e86deff6bcbc025539948aa492c4cdd0291ded2", "[\"prioriza quem atende à condição sobre um dado do candidato; o dado deve estar cadastrado e ser solicitado\"]", "CRITERIO_DESEMPATE", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000013"), "UNI-REQ-0045 — processamento da 1ª opção antes da 2ª", "ALOCACAO_PRIMEIRA_OPCAO_PRIORITARIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "9b08764b327434401de25cf5be209f031cf5d7f9b2044cb43b5fd2d9ca95f15a", "[\"processam-se todas as 1ªs opções de cada curso\",\"a vaga de modalidade não preenchida é remanejada dentro do curso, entre candidatos de 1ª opção\",\"só a vaga que sobra depois do remanejamento vai à 2ª opção, em ordem de nota, sem deslocar aprovado de 1ª opção\",\"quem não é classificado em nenhuma opção vai para a lista de espera\"]", "REGRA_ORDEM_ALOCACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000014"), "Lei 12.711/2012 art. 1º parágrafo único (red. Lei 14.723/2023) — renda familiar bruta per capita ≤ 1 SM (ensino superior); PN MEC 18/2012 art. 6º-7º — apuração da renda mensal per capita + exclusões obrigatórias", "RENDA-PER-CAPITA-LEI-12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "3300f396d5f99b257bb5ada2b919d7749e3ad9babc097f63d7c99e3ce641e99a", "[\"média mensal da renda familiar bruta no período considerado, após exclusões, dividida pelo número de membros da família deve ser menor ou igual ao limite em salários mínimos multiplicado pelo valor de referência do salário mínimo\"]", "REGRA_ELEGIBILIDADE", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000017"), "Portaria MEC 18/2012 art. 11, parágrafo único (red. PN 2.027/2023) — reserva limitada às vagas da oferta e prioridade do inciso III (LB) sobre o inciso IV (LI); art. 10 II (mínimo 50%, piso)", "AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "6929fffe7c5164b48f8c05867dffe296c72baa029dfdbfafb873757dc85ad3fc", "[\"a reserva nunca excede as vagas da oferta (art. 11 §único I)\",\"na falta de vagas, as modalidades de baixa renda têm prioridade sobre as demais (art. 11 §único II)\",\"a reserva pode superar o mínimo legal de 50%, reduzindo as vagas da ampla concorrência (art. 10 II)\",\"a distribuição em cursos com poucas vagas segue o limite previsto em lei, sem impedir a oferta\"]", "REGRA_AJUSTE_DISTRIBUICAO_VAGAS", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000018"), "Lei 9.784/1999 art. 56 (cabimento do recurso administrativo) e art. 61 (efeito suspensivo por decisão fundamentada); prazo, efeito suspensivo e guarda da vaga configuráveis por edital", "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "cffb7c530a7664cfd44d4750daa2974a1800cf0b73ff084039f701e2b453b2dc", "[\"o recurso corre em duas instâncias no Uni+; a 2ª instância existe quando o recurso admite instância superior, e o julgamento do recurso fica registrado como ato publicado\",\"o efeito suspensivo do recurso na 1ª instância vem da escolha do edital, e a escolha é obrigatória: o edital declara explicitamente que não há efeito suspensivo, e a falta da declaração é recusada\",\"o recurso na 2ª instância sempre tem efeito suspensivo, sem configuração\",\"a guarda da vaga em disputa é uma declaração separada do efeito suspensivo: a vaga fica guardada até o resultado definitivo ou não fica guardada, e, sem declaração, vale guardar até o resultado definitivo\",\"com instância superior, o edital não pode declarar que a vaga não fica guardada\",\"interposição só é aceita durante o período da fase de recurso\",\"não cabe recurso contra resultado definitivo\",\"o prazo de interposição na 1ª instância começa no instante da publicação do ato: se a publicação atrasa, o prazo acompanha o atraso, sem retificação\",\"o prazo da 2ª instância é próprio, é declarado quando o recurso admite instância superior, é recusado quando não admite, e começa na publicação do indeferimento ao candidato\",\"o evento que inicia o prazo é a publicação de uma publicação prevista preliminar da própria fase, identificada por ela e não pelo código do tipo de ato — a publicação do mesmo tipo por outra fase do cronograma não inicia o prazo desta\",\"o evento que inicia o prazo nunca é um ato que registra a configuração da versão publicada\",\"os prazos correm exclusivamente em dia útil e admitem apenas dias úteis em valor inteiro ou horas — dias corridos são recusados, e fração de dia útil também, cada um com causa própria; nunca aproximado em silêncio\",\"contagem sobre dia útil depende de duas declarações do processo, o calendário de dias úteis vigente da localidade regente e a convenção de contagem — vale para todo prazo de recurso, nas duas unidades e nas duas instâncias\",\"julgamento e retificação são novos fatos e não sobrescrevem os registros anteriores\"]", "REGRA_PRAZO_RECURSO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000019"), "Portaria MEC nº 704/2025 (DOU 20/10/2025, Seção 1, p. 36-37), art. 2º e Anexo — insere o art. 20-A na Portaria Normativa MEC nº 21/2012; Lei 12.711/2012 art. 3º §1º (red. Lei 14.723/2023)", "REMANEJAMENTO_ORDEM_LEI_12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"destinoFinalCodigo\":\"AC\",\"ordens\":[\n  {\"origem\":\"LB_PPI\",\"destinos\":[\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_Q\",\"destinos\":[\"LB_PPI\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_PCD\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LB_EP\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_PPI\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_Q\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"]},\n  {\"origem\":\"LI_PCD\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_EP\"]},\n  {\"origem\":\"LI_EP\",\"destinos\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\"]}\n]}", "1dfcb5775510b09c7698fd743a8b7c2d01f4a6f13c3c34942034ffc6f407b6e5", "[\"oito origens federais, ordem fixa por origem e ampla concorrência como modalidade final — a ordem é aplicada sem recálculo\"]", "CRITERIO_REMANEJAMENTO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000020"), "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "ae056fe1463054f24b56768b6fbe43741f7834e2befbf9cea03f523f689f2057", "[\"evento que inicia o prazo fora da meia-noite: a hora do evento que inicia o prazo não influencia o fechamento — o dia civil do evento que inicia o prazo é excluído por inteiro e a contagem parte do primeiro dia útil seguinte (1 dia útil iniciado sexta às 18h, sem feriado no intervalo, fecha no fim de segunda)\",\"evento que inicia o prazo em dia não útil: o início desloca para o primeiro dia útil seguinte; o dia do evento que inicia o prazo, útil ou não, nunca conta (1 dia útil iniciado domingo às 18h, sem feriado no intervalo, fecha no fim de segunda)\",\"em dias úteis: N dias úteis inteiros contados após o dia excluído; o prazo termina à meia-noite que inicia o dia seguinte ao N-ésimo dia útil; esse instante não está incluído no prazo, conforme o fuso horário registrado na versão publicada\",\"em horas: a contagem começa no primeiro instante do primeiro dia útil seguinte ao dia do evento que inicia o prazo e consome apenas horas situadas em dia útil (48h iniciadas sexta às 18h, sem feriado no intervalo, começam segunda 00:00 e fecham quarta 00:00)\"]", "CONVENCAO_CONTAGEM_PRAZO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000021"), "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "CONTAGEM_PRAZO_HORAS_UTEIS_DESDE_O_EVENTO_INICIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "c2d80dcb4e20f527568a740ce272c0cdab8b620ab21a8170a57160af5a001203", "[\"evento que inicia o prazo fora da meia-noite: a contagem parte do instante exato do evento que inicia o prazo — a hora do fechamento deriva da hora do evento que inicia o prazo, sem deslocamento para o início de outro dia (48h iniciadas sexta às 18h, com sábado e domingo não úteis e sem feriado, fecham terça 18h)\",\"evento que inicia o prazo em dia não útil: o início não desloca — o relógio não avança em instante situado em dia não útil e o primeiro avanço ocorre no primeiro instante útil seguinte (24h iniciadas domingo às 18h, com segunda útil, só começam a consumir segunda 00:00 e fecham terça 00:00)\",\"em horas: consome exatamente o valor declarado em horas situadas em dia útil, atravessando a madrugada de dia útil normalmente; fecha no instante em que o saldo zera\",\"em dias úteis: N dias úteis equivalem a N×24 horas situadas em dia útil consumidas desde o evento que inicia o prazo; dia civil de transição de fuso contribui com as horas que realmente tem, nunca um bloco presumido de 24 (1 dia útil iniciado sexta às 18h, sem feriado no intervalo, fecha segunda 18h)\"]", "CONVENCAO_CONTAGEM_PRAZO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000022"), "O edital de abertura é o fundamento normativo do prazo de interposição e declara a convenção pela qual ele se conta (UNI-REQ-0095): na ausência de norma específica que fixe outro prazo, o edital estabelece o seu, e o sistema congela e reproduz o declarado sem julgar a escolha. Decisão institucional juridicamente orientada — não é parecer formal nem jurisprudência consolidada. Não dispõe sobre efeito suspensivo, cuja confirmação é dependência própria (UNI-REQ-0117).", "CONTAGEM_PRAZO_AVANCA_DATA_UTIL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "2d4aa7f3c0a0712b1f56271b88de6590d2f29e5b96b78fecf66ddf2db3fac71c", "[\"evento que inicia o prazo fora da meia-noite: mantém a hora do evento que inicia o prazo — a contagem parte do instante exato, sem deslocamento para o início de outro dia (1 dia útil iniciado sexta às 18h, com sábado e domingo não úteis e sem feriado, fecha segunda 18h)\",\"evento que inicia o prazo em dia não útil: em dias úteis, desloca para o próximo dia útil na mesma hora (evento que inicia o prazo no domingo às 18h conta como segunda 18h, e 1 dia útil fecha terça 18h); em horas não há deslocamento, apenas a não contagem dos instantes de dia não útil\",\"em dias úteis: fecha na mesma hora do evento que inicia o prazo, N datas úteis adiante, pulando cada data não útil; se a hora do evento que inicia o prazo não existir na data de fechamento por transição de fuso, fecha no primeiro instante válido seguinte\",\"em horas: consome horas situadas em dia útil desde o evento que inicia o prazo, sem deslocar o início — nesta unidade a convenção coincide com a contagem de horas úteis a partir do evento que inicia o prazo; a diferença entre as duas está apenas na contagem em dias úteis\"]", "CONVENCAO_CONTAGEM_PRAZO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000023"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIBUICAO_LEI_12711", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\"]}", "e732a36dfc79e64d7acc9afc13fccb09284ff567d5d50f2bc946b3a4d605249e", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"LB_EP≥0 e LI_EP≥0\",\"AC≥0\",\"VR_final+RETIRADAS+AC=VO_base\",\"modalidade fora de modalidades_admitidas é recusada\"]", "REGRA_DISTRIBUICAO_VAGAS", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000024"), "Res. Unifesspa 532/2021 (vagas PcD/Indígena/Quilombola); Portaria MEC 18/2012 art. 12 (reservas suplementares e outras ações afirmativas)", "DISTRIBUICAO_INSTITUCIONAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro institucional não nomeado por regra própria\",\"modalidades_admitidas\":null}", "9b2c768ff2a081cf5989ad584754a3a5b532e3e2f7c5168d81d28e7e354d1880", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"a soma das quantidades declaradas é o total publicado, e fecha no VO_base\",\"modalidades_admitidas nulo: rol aberto, para o certame institucional que ainda não tem regra própria\"]", "REGRA_DISTRIBUICAO_VAGAS", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000025"), "Res. Unifesspa 532/2021-CONSEPE, art. 2º (vagas por acréscimo para candidatos indígenas e quilombolas)", "DISTRIBUICAO_POR_ACRESCIMO_INDIGENA_E_QUILOMBOLA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"Processo Seletivo Indígena e Quilombola\",\"modalidades_admitidas\":[\"AC_I\",\"AC_Q\"]}", "1c03f5d1c364ea02380c4239594082799c9b14d1366b9b81eff391fd3f136de0", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame exclusivo: não há ampla concorrência\",\"rol composto só de vagas por acréscimo — sem outro conjunto ao qual se somem, a soma delas é o total publicado\"]", "REGRA_DISTRIBUICAO_VAGAS", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000026"), "Res. Unifesspa 532/2021-CONSEPE, art. 1º (reserva de vaga para pessoa com deficiência); Portaria MEC 18/2012 art. 12", "DISTRIBUICAO_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"quadro_fixo_por_modalidade\":\"objeto {codigo: quantidade} fixado por edital (NÃO art. 10)\",\"aplicacao\":\"quadro fixo sem as cotas federais — AC_PCD como reserva de pessoa com deficiência fora do regime federal\",\"modalidades_admitidas\":[\"AC\",\"AC_PCD\"]}", "7af723f2b051599d743058d8a80a9cc8b78742c40e028c31c83d59b1faa89c20", "[\"quadro fixo por edital (não recalculado pelo art. 10)\",\"certame sem as cotas da Lei 12.711\",\"AC_PCD retira de AC: o par fecha no VO_base\"]", "REGRA_DISTRIBUICAO_VAGAS", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000027"), "Portaria Normativa MEC nº 18/2012 art. 10 e 11 (red. PN 2.027/2023) — distribuição e arredondamento das vagas reservadas; Lei 12.711/2012 (red. Lei 14.723/2023)", "DISTRIBUICAO_LEI_12711_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"pr_minimo\":\"numeric (piso 0,5 — art. 10 II; teto 1,0)\",\"modo_arredondamento\":\"teto (ceil) em todas as sub-reservas EXCETO LI_Q (floor) — art. 11\",\"ordem_garantia_minima\":[\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_PCD\",\"LI_EP\"],\"sub_reservas\":[\"PPI\",\"Q\",\"PCD\",\"EP\"],\"entradas_por_edital\":[\"VO_base\",\"PR\",\"ReferenciaReservaDemografica\"],\"modalidades_admitidas\":[\"AC\",\"LB_PPI\",\"LB_Q\",\"LB_PCD\",\"LB_EP\",\"LI_PPI\",\"LI_Q\",\"LI_PCD\",\"LI_EP\",\"AC_PCD\"]}", "49aceb36f02de21633ca0c49972a72c9dd8848d442e06af921ec160b8a227f33", "[\"VR=ceil(VO×PR)\",\"VRRI=ceil(VR×0,5)\",\"VRSI=VR−VRRI\",\"sub-reservas ceil EXCETO LI_Q=floor (art. 11)\",\"garantia mín-1 ordenada I-VII condicional à disponibilidade (art. 10 §2º), LI_Q fora\",\"LB_EP≥0 e LI_EP≥0\",\"AC≥0\",\"VR_final+RETIRADAS+AC=VO_base\",\"AC_PCD retira de AC, como qualquer outra retirada federal\",\"modalidade fora de modalidades_admitidas é recusada\"]", "REGRA_DISTRIBUICAO_VAGAS", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000028"), "Edital (ordem de desempate por nota de área do ENEM)", "DESEMPATE_MAIOR_NOTA_AREA_ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "69acb10c7aff5f8ef0f8222e65454c4ea08f87620eea45515ccb8be7845d2257", "[\"ordena por maior nota na primeira área da lista; o empate que resta passa à área seguinte\",\"cada área é citada uma única vez, pelo código, e existe em todos os grupos do quadro de pesos por área registrado na versão da classificação\",\"exige classificação baseada em ENEM, calculada pela média ponderada\"]", "CRITERIO_DESEMPATE", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000029"), "Edital (falta em dia de prova do ENEM)", "ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "b07e389dac687d427f988424b968626ec0b5876a7d5a04864b19bdbdfd2bf4d6", "[\"falta em pelo menos um dia de prova da edição do ENEM usada no processo → elimina\",\"sem participação registrada na edição do ENEM indicada pelo candidato equivale a falta\"]", "REGRA_ELIMINACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000030"), "Decisão institucional da Unifesspa", "APURACAO_AVALIACAO_UNICA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"corretores_distintos\":1,\"formas_do_conceito\":[\"POR_OPCOES\",\"NUMERICO\"]}", "bae89bddb6e30db62a655bc0e7ebfc49435ca03154ff241b88d58fdc744c558a", "[\"um só avaliador lança a nota de cada candidato na etapa\",\"a nota lançada é a nota da etapa, sem combinação com outra\"]", "REGRA_APURACAO_AVALIACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000031"), "UNI-REQ-0097 (dupla correção) e edital do processo seletivo", "APURACAO_CORRECAO_INDEPENDENTE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"corretores_distintos\":3,\"formas_do_conceito\":[\"NUMERICO\"]}", "0511a574005e32f27cb9b54c78d31e5d2474d6d8916f7ac06d918d7b9377e85a", "[\"mais de um avaliador corrige cada candidato na etapa, cada um sem conhecer a nota do outro\",\"a nota da etapa é a combinação das correções independentes, na forma que o edital determina\"]", "REGRA_APURACAO_AVALIACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000032"), "Edital (eliminação do candidato que faltar à etapa)", "ELIMINACAO_AUSENCIA_NA_ETAPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{}", "eba80411a9fc04e4dc51d534fd5b50cc4ca9ea45a1e596ef74b32c5cd9a74f19", "[\"falta à etapa referenciada → elimina\",\"não se aplica à etapa de nota do ENEM\"]", "REGRA_ELIMINACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000033"), "UNI-REQ-0383 (procedimento de recurso por matéria) e UNI-REQ-0097 (correção cega)", "PROCEDIMENTO_RECURSO_RECORRECAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"revisa\":\"desempenho avaliado por pessoas\",\"evidencia\":\"nova correção cega do material\",\"corretores\":\"os_da_regra_de_consolidacao_da_etapa\"}", "0b214cc0b0bd7610fd7e2ddf03f1e4fb8c7ac13d90cb780d8bf4982ee623a738", "[\"a revisão é uma nova correção do material, feita sem conhecer a nota anterior\",\"os corretores são os da apuração da etapa\"]", "REGRA_PROCEDIMENTO_RECURSO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000034"), "UNI-REQ-0383 (procedimento de recurso por matéria)", "PROCEDIMENTO_RECURSO_CONFERENCIA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"revisa\":\"comprovação de um fato e aplicação da pontuação prevista por tabela\",\"evidencia\":\"o documento e a tabela\",\"corretores\":\"os_da_regra_de_consolidacao_da_etapa_quando_ha_consolidacao_humana\"}", "3ecbeea4bf0ef7dc09f58ed5fa4077f489083b81aee587a76a02cf56c0341c28", "[\"a revisão confere o documento contra a tabela do edital\",\"há corretores só quando a etapa tem apuração; do contrário, nenhum\"]", "REGRA_PROCEDIMENTO_RECURSO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000035"), "UNI-REQ-0383 (procedimento de recurso por matéria) e UNI-REQ-0281", "PROCEDIMENTO_RECURSO_CORRECAO_MATERIAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"revisa\":\"erro de registro (transcrição, soma, presença)\",\"evidencia\":\"o registro e a evidência do erro\",\"corretores\":\"nenhum\"}", "68185a7b9aafdacfbcfc2b431bf672d3d0c12bb84dfb300d3671ca05e10fec82", "[\"a correção tem sempre um motivo da lista da etapa, sem a opção outro\",\"não é saída genérica para o que o cálculo da nota não explica\"]", "REGRA_PROCEDIMENTO_RECURSO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000036"), "UNI-REQ-0383 (procedimento de recurso por matéria)", "PROCEDIMENTO_RECURSO_RECALCULO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"revisa\":\"cálculo sobre insumos já conferidos (gabarito, média ponderada do ENEM, classificação)\",\"evidencia\":\"os dados registrados na versão publicada e o boletim\",\"corretores\":\"nenhum\"}", "4b79e746dda9247af7589a48e6908a4e2fd333da257fcb4015f3fafc6467f4d2", "[\"o recálculo parte dos dados registrados na versão publicada, sem nova correção\",\"a nota do ENEM é recalculada à vista do boletim\"]", "REGRA_PROCEDIMENTO_RECURSO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000037"), "Decisão institucional da Unifesspa", "APURACAO_CONCEITO_UNANIMIDADE", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"corretores_distintos\":2,\"criterio\":\"UNANIMIDADE\",\"formas_do_conceito\":[\"POR_OPCOES\"],\"quando_nao_consolida\":\"DECISAO_DA_BANCA\"}", "0dcc0358c55a8deeb5c42c53dd51dd3e00482c2a3d2fbe58af8ea74f95c71a5d", "[\"cada corretor lança o conceito sem conhecer o dos demais\",\"o conceito da etapa é o valor em que o critério converge: todos os corretores atribuem o mesmo valor\",\"quando o critério não converge, o tratamento que a versão declara é aplicado\"]", "REGRA_APURACAO_AVALIACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000038"), "Decisão institucional da Unifesspa", "APURACAO_CONCEITO_MAIORIA_SIMPLES", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"corretores_distintos\":3,\"criterio\":\"MAIORIA_SIMPLES\",\"formas_do_conceito\":[\"POR_OPCOES\"],\"quando_nao_consolida\":\"NOVO_CORRETOR\"}", "c1800bc344c0b04e867953d2b203e355b7aa2fd758ce531dc17c6a9c01559d33", "[\"cada corretor lança o conceito sem conhecer o dos demais\",\"o conceito da etapa é o valor em que o critério converge: um valor com mais atribuições que cada um dos outros\",\"quando o critério não converge, o tratamento que a versão declara é aplicado\"]", "REGRA_APURACAO_AVALIACAO", null, "v1" },
                    { new Guid("d0a00000-0000-7000-8000-000000000039"), "Decisão institucional da Unifesspa", "APURACAO_CONCEITO_MAIORIA_ABSOLUTA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "{\"corretores_distintos\":3,\"criterio\":\"MAIORIA_ABSOLUTA\",\"formas_do_conceito\":[\"POR_OPCOES\"],\"quando_nao_consolida\":\"NOVO_CORRETOR\"}", "9e6971981925fa1f297f2d6cce0e0599ae43b54bc4fb5127497920dd929dc726", "[\"cada corretor lança o conceito sem conhecer o dos demais\",\"o conceito da etapa é o valor em que o critério converge: um valor com mais da metade das atribuições\",\"quando o critério não converge, o tratamento que a versão declara é aplicado\"]", "REGRA_APURACAO_AVALIACAO", null, "v1" }
                });

            migrationBuilder.InsertData(
                schema: "autorizacao",
                table: "termos_de_responsabilidade",
                columns: new[] { "id", "created_at", "ultimo_numero", "updated_at", "versao_vigente_id" },
                values: new object[] { new Guid("019a0000-0000-7000-8000-0000000000a1"), new DateTimeOffset(new DateTime(2026, 11, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_de_documento",
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
                table: "tipos_de_fase",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by", "coleta_inscricao" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000001"), null, "INSCRICAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Período em que o candidato se inscreve no processo seletivo.", false, "Inscrição", "NO_EDITAL", null, null, null, null, null, true });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_de_fase",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by", "coleta_solicitacao_isencao" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000002"), "Lei nº 12.799/2013", "SOLICITACAO_ISENCAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Janela em que o candidato pede isenção da taxa de inscrição. Abre junto com as inscrições e termina antes delas.", false, "Solicitação de isenção", "NO_EDITAL", null, null, null, null, null, true });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_de_fase",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by", "permite_complementacao" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000003"), null, "HOMOLOGACAO_INSCRICOES", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Conferência das inscrições recebidas e publicação de quais foram homologadas.", false, "Homologação das inscrições", "NO_EDITAL", null, null, null, null, null, true });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_de_fase",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000004"), null, "ENSALAMENTO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Distribuição dos candidatos pelos locais de prova.", false, "Ensalamento", "NO_EDITAL", null, null, null, null, null });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_de_fase",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by", "agrupa_etapas" },
                values: new object[] { new Guid("f45e0000-0000-7000-8000-000000000005"), null, "AVALIACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Fase que agrupa as etapas pontuadas do certame.", false, "Avaliação", "NO_EDITAL", null, null, null, null, null, true });

            migrationBuilder.InsertData(
                schema: "configuracao",
                table: "tipos_de_fase",
                columns: new[] { "id", "base_legal", "codigo", "created_at", "created_by", "deleted_at", "deleted_by", "descricao", "is_deleted", "nome", "origem_data", "unidade_sugerida_nome", "unidade_sugerida_origem_id", "unidade_sugerida_sigla", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("f45e0000-0000-7000-8000-000000000006"), null, "CLASSIFICACAO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Apuração das notas e ordenação dos candidatos por modalidade.", false, "Classificação", "NO_EDITAL", null, null, null, null, null },
                    { new Guid("f45e0000-0000-7000-8000-000000000009"), null, "RESULTADO_FINAL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "Publicação do resultado do processo: o preliminar, o prazo de recurso e o definitivo, na mesma fase.", false, "Resultado final", "NO_EDITAL", null, null, null, null, null },
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
                table: "opcoes_dos_dados",
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
                table: "permissoes_do_perfil",
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
                name: "ix_acessos_ao_documento_da_versao_documento",
                schema: "autorizacao",
                table: "acessos_ao_documento_da_versao",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_anexos_do_ato_nome",
                schema: "publicacoes",
                table: "anexos_do_ato",
                columns: new[] { "ato_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anexos_do_ato_ordem",
                schema: "publicacoes",
                table: "anexos_do_ato",
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
                name: "ix_anexos_do_pedido_pedido",
                schema: "autorizacao",
                table: "anexos_do_pedido",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_apresentacoes_de_documento_inscricao_exigencia",
                schema: "selecao",
                table: "apresentacoes_de_documento",
                columns: new[] { "inscricao_id", "exigencia_id" });

            migrationBuilder.CreateIndex(
                name: "ux_areas_do_quadro_de_pesos_grupo_do_quadro_codigo",
                schema: "selecao",
                table: "areas_do_quadro_de_pesos",
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
                filter: "situacao = 'EM_VERIFICACAO'");

            migrationBuilder.CreateIndex(
                name: "ix_arquivos_da_apresentacao_inscricao",
                schema: "selecao",
                table: "arquivos_da_apresentacao",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_atendimentos_especializados_processo_seletivo_id",
                schema: "selecao",
                table: "atendimentos_especializados",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atos_da_protecao_do_catalogo_ato",
                schema: "configuracao",
                table: "atos_da_protecao_do_catalogo",
                columns: new[] { "ato", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_atos_da_protecao_do_catalogo_item",
                schema: "configuracao",
                table: "atos_da_protecao_do_catalogo",
                columns: new[] { "cadastro", "item_id", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_atos_da_protecao_do_catalogo_item_ciclo",
                schema: "configuracao",
                table: "atos_da_protecao_do_catalogo",
                columns: new[] { "cadastro", "item_id", "ciclo", "instante" });

            migrationBuilder.CreateIndex(
                name: "ux_atos_da_protecao_do_catalogo_ordem_de_registro",
                schema: "configuracao",
                table: "atos_da_protecao_do_catalogo",
                column: "ordem_de_registro",
                unique: true);

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
                name: "ix_atos_normativos_numeracao",
                schema: "publicacoes",
                table: "atos_normativos",
                columns: new[] { "orgao", "serie", "ano", "numero" });

            migrationBuilder.CreateIndex(
                name: "ix_atos_normativos_publicado_em",
                schema: "publicacoes",
                table: "atos_normativos",
                columns: new[] { "publicado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_atos_normativos_retificado",
                schema: "publicacoes",
                table: "atos_normativos",
                column: "ato_retificado_id",
                unique: true,
                filter: "ato_retificado_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_da_etapa_importacao_id_etapa_processo_id",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "importacao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_da_etapa_inscricao_etapa",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "inscricao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_avaliacoes_da_etapa_importacao_inscricao_oferta",
                schema: "selecao",
                table: "avaliacoes_da_etapa",
                columns: new[] { "importacao_id", "inscricao_id", "oferta_curso_id" },
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
                name: "ix_bancas_da_etapa_etapa_processo_id_processo_seletivo_id",
                schema: "selecao",
                table: "bancas_da_etapa",
                columns: new[] { "etapa_processo_id", "processo_seletivo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bancas_da_etapa_processo_chave_local",
                schema: "selecao",
                table: "bancas_da_etapa",
                columns: new[] { "processo_seletivo_id", "chave_local" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bancas_requeridas_fase_cronograma_id_processo_seletivo_id",
                schema: "selecao",
                table: "bancas_requeridas",
                columns: new[] { "fase_cronograma_id", "processo_seletivo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bancas_requeridas_processo_chave_local",
                schema: "selecao",
                table: "bancas_requeridas",
                columns: new[] { "processo_seletivo_id", "chave_local" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bases_da_permissao_concedida_perfil_concessao",
                schema: "autorizacao",
                table: "bases_da_permissao_concedida",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bases_da_permissao_concessao_codigo",
                schema: "autorizacao",
                table: "bases_da_permissao_concedida",
                columns: new[] { "concessao_id", "codigo", "ordem_no_historico" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_bases_da_permissao_ordem",
                schema: "autorizacao",
                table: "bases_da_permissao_concedida",
                column: "ordem_no_historico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bases_legais_da_comprovacao_regra_de_comprovacao_id",
                schema: "selecao",
                table: "bases_legais_da_comprovacao",
                column: "regra_de_comprovacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_bases_legais_do_fada_codigo_vivo",
                schema: "configuracao",
                table: "bases_legais_do_fada",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_bases_legais_do_fada_tipo_instrumento",
                schema: "configuracao",
                table: "bases_legais_do_fada",
                column: "tipo_instrumento");

            migrationBuilder.CreateIndex(
                name: "ux_calculos_dos_dados_processo_dado",
                schema: "selecao",
                table: "calculos_dos_dados",
                columns: new[] { "processo_seletivo_id", "codigo_do_dado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendarios_de_dias_uteis_versao_dataset",
                schema: "configuracao",
                table: "calendarios_de_dias_uteis",
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
                name: "ix_categorias_de_documento_codigo_vivo",
                schema: "configuracao",
                table: "categorias_de_documento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_categorias_de_documento_ordem_codigo",
                schema: "configuracao",
                table: "categorias_de_documento",
                columns: new[] { "ordem", "codigo" });

            migrationBuilder.CreateIndex(
                name: "ux_categorias_julgadas_codigo",
                schema: "selecao",
                table: "categorias_julgadas",
                columns: new[] { "banca_requerida_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certames_divulgados_divulgado_em",
                schema: "selecao",
                table: "certames_divulgados",
                column: "divulgado_em");

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
                name: "ix_chaves_de_idempotencia_expira_em",
                schema: "plataforma",
                table: "chaves_de_idempotencia",
                column: "expira_em");

            migrationBuilder.CreateIndex(
                name: "ux_chaves_de_idempotencia_escopo_rota_chave",
                schema: "plataforma",
                table: "chaves_de_idempotencia",
                columns: new[] { "escopo", "rota", "chave" },
                unique: true);

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
                name: "ix_concessoes_revogada_pela_revisao_id_perfil_id",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "revogada_pela_revisao_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ux_concessoes_ativa_unica",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "beneficiario_emissor", "beneficiario_conta_id", "perfil_id", "escopo_tipo", "unidade_id" },
                unique: true,
                filter: "revogada_em IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_concessoes_origem",
                schema: "autorizacao",
                table: "concessoes",
                columns: new[] { "origem_tipo", "origem_id", "beneficiario_emissor", "beneficiario_conta_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_das_exigencias_documento_exigido_id",
                schema: "selecao",
                table: "condicoes_das_exigencias",
                column: "documento_exigido_id");

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_de_atendimento_codigo_vivo",
                schema: "configuracao",
                table: "condicoes_de_atendimento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_de_exibicao_do_dado_fato_coletado_id",
                schema: "selecao",
                table: "condicoes_de_exibicao_do_dado",
                column: "fato_coletado_id");

            migrationBuilder.CreateIndex(
                name: "ix_condicoes_do_calculo_dos_dados_regra_id",
                schema: "selecao",
                table: "condicoes_do_calculo_dos_dados",
                column: "regra_derivacao_configurada_id");

            migrationBuilder.CreateIndex(
                name: "ux_condicoes_ofertadas_por_atendimento_origem",
                schema: "selecao",
                table: "condicoes_ofertadas",
                columns: new[] { "atendimento_especializado_id", "condicao_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_classificacao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_classificacao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_de_remanejamento_processo",
                schema: "selecao",
                table: "configuracoes_de_remanejamento",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_configuracoes_de_vagas_processo_oferta",
                schema: "selecao",
                table: "configuracoes_de_vagas",
                columns: new[] { "processo_seletivo_id", "oferta_curso_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_divulgacao_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_divulgacao",
                column: "processo_seletivo_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_do_fada_processo_seletivo_id",
                schema: "selecao",
                table: "configuracoes_do_fada",
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
                name: "ix_cursos_codigo_vivo",
                schema: "configuracao",
                table: "cursos",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_cursos_ordenacao_alfabetica",
                schema: "configuracao",
                table: "cursos",
                columns: new[] { "nome_ordenacao", "codigo", "id" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_dados_do_candidato_codigo",
                schema: "configuracao",
                table: "dados_do_candidato",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dados_dos_formularios_grupo_ordem",
                schema: "selecao",
                table: "dados_dos_formularios",
                columns: new[] { "grupo_coletado_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dados_dos_formularios_processo_dado",
                schema: "selecao",
                table: "dados_dos_formularios",
                columns: new[] { "processo_seletivo_id", "codigo_do_dado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dados_dos_formularios_processo_finalidade_ordem",
                schema: "selecao",
                table: "dados_dos_formularios",
                columns: new[] { "processo_seletivo_id", "finalidade", "ordem" },
                unique: true,
                filter: "grupo_coletado_id IS NULL");

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
                name: "ix_decisoes_da_migracao_revisao_envio_de_origem_e_etapa",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "revisao_id", "envio_de_origem_id", "envio_de_origem_etapa" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_revisao_id_envio_id_envio_etapa",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "revisao_id", "envio_id", "envio_etapa" });

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_da_migracao_revisao_id_perfil_id",
                schema: "autorizacao",
                table: "decisoes_da_migracao",
                columns: new[] { "revisao_id", "perfil_id" });

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
                column: "ordem_no_historico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_da_versao_etapa",
                schema: "autorizacao",
                table: "decisoes_da_versao",
                columns: new[] { "versao_id", "ciclo", "etapa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decisoes_do_pedido_pedido",
                schema: "autorizacao",
                table: "decisoes_do_pedido",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ux_decisoes_do_pedido_ordem_de_registro",
                schema: "autorizacao",
                table: "decisoes_do_pedido",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_deficiencias_atendidas_por_atendimento_origem",
                schema: "selecao",
                table: "deficiencias_atendidas",
                columns: new[] { "atendimento_especializado_id", "tipo_deficiencia_origem_id" },
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
                column: "ordem_no_historico",
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
                name: "ix_dias_nao_uteis_calendario_data",
                schema: "configuracao",
                table: "dias_nao_uteis",
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
                column: "ordem_no_historico",
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
                filter: "situacao <> 'RECUSADO'");

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
                name: "ux_documentos_exigidos_processo_chave_local",
                schema: "selecao",
                table: "documentos_exigidos",
                columns: new[] { "processo_seletivo_id", "chave_local" },
                unique: true);

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
                name: "ux_enderecos_da_inscricao_dado",
                schema: "selecao",
                table: "enderecos_da_inscricao",
                columns: new[] { "inscricao_id", "ocorrencia_id", "codigo_do_dado" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_envios_de_decisoes_revisao_id_perfil_id",
                schema: "autorizacao",
                table: "envios_de_decisoes",
                columns: new[] { "revisao_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ux_envios_de_decisoes_gestao",
                schema: "autorizacao",
                table: "envios_de_decisoes",
                column: "revisao_id",
                unique: true,
                filter: "etapa = 'GESTAO_DE_ACESSOS'");

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
                columns: new[] { "processo_seletivo_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ux_etapas_processo_processo_chave_local",
                schema: "selecao",
                table: "etapas_processo",
                columns: new[] { "processo_seletivo_id", "chave_local" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_bloqueio_do_perfil_decisao_id",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                column: "decisao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_bloqueio_do_perfil_desativacao_id",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                column: "desativacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_bloqueio_do_perfil_perfil",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                columns: new[] { "perfil_id", "instante" });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_bloqueio_do_perfil_suspensao_id",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                column: "suspensao_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_bloqueio_do_perfil_versao_que_entrou_id",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                column: "versao_que_entrou_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_de_bloqueio_do_perfil_versao_vigente_id",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                column: "versao_vigente_id");

            migrationBuilder.CreateIndex(
                name: "ux_eventos_de_bloqueio_do_perfil_ordem",
                schema: "autorizacao",
                table: "eventos_de_bloqueio_do_perfil",
                column: "ordem_no_historico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exclusividades_de_atos_ato_id",
                schema: "publicacoes",
                table: "exclusividades_de_atos",
                column: "ato_id");

            migrationBuilder.CreateIndex(
                name: "ix_exclusividades_de_atos_ato_inicial_id",
                schema: "publicacoes",
                table: "exclusividades_de_atos",
                column: "ato_inicial_id");

            migrationBuilder.CreateIndex(
                name: "ux_exclusividades_de_atos_objeto_tipo",
                schema: "publicacoes",
                table: "exclusividades_de_atos",
                columns: new[] { "entidade_tipo", "entidade_id", "tipo_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_chave_local",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "chave_local" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_ordem",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fases_cronograma_processo_tipo_fase",
                schema: "selecao",
                table: "fases_cronograma",
                columns: new[] { "processo_seletivo_id", "tipo_fase_origem_id" },
                unique: true);

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
                name: "ux_grupos_da_inscricao_membro",
                schema: "selecao",
                table: "grupos_da_inscricao",
                columns: new[] { "inscricao_id", "grupo_codigo", "membro_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grupos_do_quadro_de_pesos_configuracao_classificacao_id",
                schema: "selecao",
                table: "grupos_do_quadro_de_pesos",
                column: "configuracao_classificacao_id");

            migrationBuilder.CreateIndex(
                name: "ux_grupos_dos_formularios_processo_codigo",
                schema: "selecao",
                table: "grupos_dos_formularios",
                columns: new[] { "processo_seletivo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_historicos_da_obrigatoriedade_regra_snapshot_at",
                schema: "selecao",
                table: "historicos_da_obrigatoriedade",
                columns: new[] { "regra_id", "snapshot_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_identificadores_da_unidade_unidade_tipo_inicio",
                schema: "organizacao",
                table: "identificadores_da_unidade",
                columns: new[] { "unidade_id", "tipo", "vigencia_inicio" });

            migrationBuilder.CreateIndex(
                name: "ux_implantacoes_das_permissoes_sequencia",
                schema: "autorizacao",
                table: "implantacoes_das_permissoes",
                column: "sequencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_importacoes_de_notas_do_enem_escopo",
                schema: "selecao",
                table: "importacoes_de_notas_do_enem",
                columns: new[] { "processo_seletivo_id", "etapa_processo_id", "edicao_codigo", "importado_em", "ordem_de_registro" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_importacoes_de_notas_do_enem_versao_configuracao_id",
                schema: "selecao",
                table: "importacoes_de_notas_do_enem",
                column: "versao_configuracao_id");

            migrationBuilder.CreateIndex(
                name: "ux_importacoes_de_notas_do_enem_ordem_de_registro",
                schema: "selecao",
                table: "importacoes_de_notas_do_enem",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                column: "cpf_indice");

            migrationBuilder.CreateIndex(
                name: "ix_inscricoes_processo_conta_id",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "conta_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inscricoes_processo_cpf_indice",
                schema: "selecao",
                table: "inscricoes",
                columns: new[] { "processo_seletivo_id", "cpf_indice" },
                unique: true,
                filter: "situacao <> 'CANCELADA'");

            migrationBuilder.CreateIndex(
                name: "ix_inscritos_da_importacao_inscricao",
                schema: "selecao",
                table: "inscritos_da_importacao",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ux_instalacoes_executor_pendente",
                schema: "autorizacao",
                table: "instalacoes",
                columns: new[] { "executor_emissor", "executor_conta_id" },
                unique: true,
                filter: "situacao IN ('AGUARDANDO_DOCUMENTO', 'DOCUMENTO_EM_VERIFICACAO', 'DOCUMENTO_VERIFICADO', 'AGUARDANDO_CODIGO')");

            migrationBuilder.CreateIndex(
                name: "ix_instituicoes_cidade_codigo_ibge",
                schema: "organizacao",
                table: "instituicoes",
                column: "cidade_codigo_ibge");

            migrationBuilder.CreateIndex(
                name: "ix_instituicoes_instituicao_unica_vivo",
                schema: "organizacao",
                table: "instituicoes",
                column: "instituicao_unica",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_instituicoes_reitoria_id",
                schema: "organizacao",
                table: "instituicoes",
                column: "reitoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_da_revisao_concessao",
                schema: "autorizacao",
                table: "itens_da_revisao",
                column: "concessao_id");

            migrationBuilder.CreateIndex(
                name: "ux_itens_da_revisao_revisao_concessao",
                schema: "autorizacao",
                table: "itens_da_revisao",
                columns: new[] { "revisao_id", "concessao_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_de_avaliacao_da_etapa_etapa_processo_id",
                schema: "selecao",
                table: "itens_de_avaliacao_da_etapa",
                column: "etapa_processo_id");

            migrationBuilder.CreateIndex(
                name: "ux_itens_avaliacao_etapa_chave_local",
                schema: "selecao",
                table: "itens_de_avaliacao_da_etapa",
                columns: new[] { "etapa_processo_id", "chave_local" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_de_nota_importacao_id_etapa_processo_id",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "importacao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_de_nota_inscricao_etapa",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "inscricao_id", "etapa_processo_id" });

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_de_nota_importacao_inscricao_item",
                schema: "selecao",
                table: "lancamentos_de_nota",
                columns: new[] { "importacao_id", "inscricao_id", "item_codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leituras_do_anexo_do_pedido_anexo_id",
                schema: "autorizacao",
                table: "leituras_do_anexo_do_pedido",
                column: "anexo_id");

            migrationBuilder.CreateIndex(
                name: "ix_leituras_do_anexo_do_pedido_pedido",
                schema: "autorizacao",
                table: "leituras_do_anexo_do_pedido",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ix_liberacoes_da_reclassificacao_concessao",
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
                column: "ordem_no_historico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locais_de_oferta_campus_responsavel_id",
                schema: "configuracao",
                table: "locais_de_oferta",
                column: "campus_responsavel_id");

            migrationBuilder.CreateIndex(
                name: "ix_locais_de_oferta_cidade_codigo_ibge",
                schema: "configuracao",
                table: "locais_de_oferta",
                column: "cidade_codigo_ibge");

            migrationBuilder.CreateIndex(
                name: "ix_manifestacoes_da_protecao_item_ciclo",
                schema: "configuracao",
                table: "manifestacoes_da_protecao",
                columns: new[] { "cadastro", "item_id", "ciclo", "instante" });

            migrationBuilder.CreateIndex(
                name: "ux_manifestacoes_da_protecao_inicial_do_autor",
                schema: "configuracao",
                table: "manifestacoes_da_protecao",
                columns: new[] { "cadastro", "item_id", "ciclo", "autor_emissor", "autor_conta_id" },
                unique: true,
                filter: "substitui_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_manifestacoes_da_protecao_ordem_de_registro",
                schema: "configuracao",
                table: "manifestacoes_da_protecao",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_manifestacoes_da_protecao_substitui",
                schema: "configuracao",
                table: "manifestacoes_da_protecao",
                column: "substitui_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_migracoes_da_concessao_perfil_id_concessao_id",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "perfil_id", "concessao_id" });

            migrationBuilder.CreateIndex(
                name: "ix_migracoes_da_concessao_revisao_id_perfil_id",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "revisao_id", "perfil_id" });

            migrationBuilder.CreateIndex(
                name: "ux_migracoes_da_concessao_revisao_concessao",
                schema: "autorizacao",
                table: "migracoes_da_concessao",
                columns: new[] { "revisao_id", "concessao_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modalidades_codigo_vivo",
                schema: "configuracao",
                table: "modalidades",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_modalidades_selecionadas_configuracao_de_vagas",
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
                name: "ix_motivos_de_decisao_do_processo_processo",
                schema: "selecao",
                table: "motivos_de_decisao_do_processo",
                column: "processo_seletivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_municipios_de_prova_processo_codigo_ibge",
                schema: "selecao",
                table: "municipios_de_prova",
                columns: new[] { "processo_seletivo_id", "codigo_ibge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipios_de_prova_processo_posicao",
                schema: "selecao",
                table: "municipios_de_prova",
                columns: new[] { "processo_seletivo_id", "posicao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_municipios_do_fada_configuracao_do_fada_id",
                schema: "selecao",
                table: "municipios_do_fada",
                column: "configuracao_do_fada_id");

            migrationBuilder.CreateIndex(
                name: "ux_niveis_atendimento_etapa_chave_local",
                schema: "selecao",
                table: "niveis_de_atendimento_da_etapa",
                columns: new[] { "etapa_processo_id", "chave_local" },
                unique: true);

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
                name: "ix_ocorrencias_da_importacao_importacao",
                schema: "selecao",
                table: "ocorrencias_da_importacao",
                column: "importacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_ocorrencias_da_importacao_inscricao_id",
                schema: "selecao",
                table: "ocorrencias_da_importacao",
                column: "inscricao_id");

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_de_curso_codigo_vivo",
                schema: "configuracao",
                table: "ofertas_de_curso",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_de_curso_curso_id",
                schema: "configuracao",
                table: "ofertas_de_curso",
                columns: new[] { "curso_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_ofertas_de_curso_local_oferta_id",
                schema: "configuracao",
                table: "ofertas_de_curso",
                column: "local_oferta_id");

            migrationBuilder.CreateIndex(
                name: "ux_opcoes_declaradas_fato_processo_dado_codigo",
                schema: "selecao",
                table: "opcoes_declaradas_fato",
                columns: new[] { "processo_seletivo_id", "codigo_do_dado", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opcoes_do_tipo_de_conceito_codigo",
                schema: "configuracao",
                table: "opcoes_do_tipo_de_conceito",
                columns: new[] { "tipo_conceito_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opcoes_do_tipo_de_conceito_posicao",
                schema: "configuracao",
                table: "opcoes_do_tipo_de_conceito",
                columns: new[] { "tipo_conceito_id", "posicao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opcoes_do_tipo_de_conceito_rotulo",
                schema: "configuracao",
                table: "opcoes_do_tipo_de_conceito",
                columns: new[] { "tipo_conceito_id", "rotulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_opcoes_dos_dados_fato_codigo",
                schema: "configuracao",
                table: "opcoes_dos_dados",
                columns: new[] { "fato_candidato_id", "codigo" },
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
                columns: new[] { "solicitante_emissor", "solicitante_conta_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_ativo_unico",
                schema: "autorizacao",
                table: "pedidos_de_concessao",
                columns: new[] { "beneficiario_email", "perfil_id", "unidade_id" },
                unique: true,
                filter: "situacao IN ('AGUARDANDO_DOCUMENTO', 'AGUARDANDO_BENEFICIARIO', 'AGUARDANDO_GESTOR', 'AGUARDANDO_PROTECAO_DE_DADOS')")
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
                name: "ix_pesos_por_area_do_enem_resolucao_grupo_vivo",
                schema: "configuracao",
                table: "pesos_por_area_do_enem",
                columns: new[] { "resolucao", "grupo_curso" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_precedencias_entre_fases_par_vivo",
                schema: "configuracao",
                table: "precedencias_entre_fases",
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
                filter: "estado = 'ABERTA'");

            migrationBuilder.CreateIndex(
                name: "ux_publicacoes_previstas_da_etapa_ato",
                schema: "selecao",
                table: "publicacoes_previstas_da_etapa",
                columns: new[] { "etapa_processo_id", "ato_codigo", "papel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_publicacoes_previstas_da_fase_ato",
                schema: "selecao",
                table: "publicacoes_previstas_da_fase",
                columns: new[] { "fase_cronograma_id", "ato_codigo", "papel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_rascunhos_processos_seletivos_unidade_id",
                schema: "selecao",
                table: "rascunhos_processos_seletivos",
                columns: new[] { "unidade_administradora_origem_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_recalculos_da_aprovacao_versao_id",
                schema: "autorizacao",
                table: "recalculos_da_aprovacao",
                column: "versao_id");

            migrationBuilder.CreateIndex(
                name: "ix_reclassificacoes_da_permissao_perfil_codigo",
                schema: "autorizacao",
                table: "reclassificacoes_da_permissao",
                columns: new[] { "perfil_id", "codigo", "ordem_no_historico" },
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
                column: "ordem_no_historico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recursos_configurados_da_etapa_etapa_processo_id",
                schema: "selecao",
                table: "recursos_configurados_da_etapa",
                column: "etapa_processo_id");

            migrationBuilder.CreateIndex(
                name: "ux_recursos_configurados_da_fase_fase_cronograma",
                schema: "selecao",
                table: "recursos_configurados_da_fase",
                column: "fase_cronograma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recursos_de_acessibilidade_codigo_vivo",
                schema: "configuracao",
                table: "recursos_de_acessibilidade",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_recursos_de_acessibilidade_nome_vivo",
                schema: "configuracao",
                table: "recursos_de_acessibilidade",
                column: "nome",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_recursos_ofertados_por_atendimento_origem",
                schema: "selecao",
                table: "recursos_ofertados",
                columns: new[] { "atendimento_especializado_id", "recurso_origem_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referencias_demograficas_censo_vivo",
                schema: "configuracao",
                table: "referencias_demograficas",
                column: "censo_referencia",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_regras_de_calculo_dos_dados_config_ordem",
                schema: "selecao",
                table: "regras_de_calculo_dos_dados",
                columns: new[] { "calculo_do_dado_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regras_de_comprovacao_processo_seletivo_id",
                schema: "selecao",
                table: "regras_de_comprovacao",
                column: "processo_seletivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_regras_de_comprovacao_regra_que_contem_id",
                schema: "selecao",
                table: "regras_de_comprovacao",
                column: "regra_que_contem_id");

            migrationBuilder.CreateIndex(
                name: "ux_regras_de_comprovacao_documento_exigido_id",
                schema: "selecao",
                table: "regras_de_comprovacao",
                column: "documento_exigido_id",
                unique: true,
                filter: "documento_exigido_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_regras_do_catalogo_hash",
                schema: "selecao",
                table: "regras_do_catalogo",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ux_regras_do_catalogo_codigo_versao",
                schema: "selecao",
                table: "regras_do_catalogo",
                columns: new[] { "codigo", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regras_eliminacao_configuracao_classificacao_id",
                schema: "selecao",
                table: "regras_eliminacao",
                column: "configuracao_classificacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_dado_booleano",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "codigo_do_dado", "valor_booleano" },
                filter: "valor_booleano IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_dado_codigo",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "codigo_do_dado", "valor_codigo" },
                filter: "valor_codigo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_dado_data",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "codigo_do_dado", "valor_data" },
                filter: "valor_data IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_respostas_da_inscricao_dado_numero",
                schema: "selecao",
                table: "respostas_da_inscricao",
                columns: new[] { "codigo_do_dado", "valor_numero" },
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
                columns: new[] { "inscricao_id", "ocorrencia_id", "codigo_do_dado", "posicao" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_revisoes_das_concessoes_perfil_id_versao_alvo_id",
                schema: "autorizacao",
                table: "revisoes_das_concessoes",
                columns: new[] { "perfil_id", "versao_alvo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_revisoes_das_concessoes_perfil_id_versao_nova_id",
                schema: "autorizacao",
                table: "revisoes_das_concessoes",
                columns: new[] { "perfil_id", "versao_nova_id" });

            migrationBuilder.CreateIndex(
                name: "ux_revisoes_das_concessoes_aberta",
                schema: "autorizacao",
                table: "revisoes_das_concessoes",
                column: "perfil_id",
                unique: true,
                filter: "encerrada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_revisoes_das_concessoes_ordem_de_registro",
                schema: "autorizacao",
                table: "revisoes_das_concessoes",
                column: "ordem_de_registro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sincronizacoes_com_o_sigaa_data_de_referencia",
                schema: "discentes",
                table: "sincronizacoes_com_o_sigaa",
                column: "data_de_referencia");

            migrationBuilder.CreateIndex(
                name: "ix_sincronizacoes_com_o_sigaa_started_at",
                schema: "discentes",
                table: "sincronizacoes_com_o_sigaa",
                column: "started_at");

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
                column: "ordem_no_historico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tentativas_de_instalacao_instalacao",
                schema: "autorizacao",
                table: "tentativas_de_instalacao",
                column: "instalacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_termos_de_consentimento_codigo_vivo",
                schema: "configuracao",
                table: "termos_de_consentimento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_termos_de_consentimento_nome",
                schema: "configuracao",
                table: "termos_de_consentimento",
                column: "nome",
                filter: "is_deleted = false");

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
                name: "ix_tipos_conceito_ativo",
                schema: "configuracao",
                table: "tipos_conceito",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_conceito_codigo",
                schema: "configuracao",
                table: "tipos_conceito",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_ato_publicado_codigo_vivo",
                schema: "publicacoes",
                table: "tipos_de_ato_publicado",
                column: "codigo",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_banca_codigo_vivo",
                schema: "configuracao",
                table: "tipos_de_banca",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_deficiencia_codigo_vivo",
                schema: "configuracao",
                table: "tipos_de_deficiencia",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_deficiencia_nome_vivo",
                schema: "configuracao",
                table: "tipos_de_deficiencia",
                column: "nome",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_documento_categoria",
                schema: "configuracao",
                table: "tipos_de_documento",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_documento_codigo_vivo",
                schema: "configuracao",
                table: "tipos_de_documento",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_fase_codigo_vivo",
                schema: "configuracao",
                table: "tipos_de_fase",
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
                name: "ix_tipos_etapa_conceitos_tipo_conceito_id",
                schema: "configuracao",
                table: "tipos_etapa_conceitos",
                column: "tipo_conceito_id");

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
                name: "ix_unidades_apelido",
                schema: "organizacao",
                table: "unidades",
                column: "apelido");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_apelido_para_busca_trgm",
                schema: "organizacao",
                table: "unidades",
                column: "apelido_para_busca")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_unidades_codigo_para_busca_trgm",
                schema: "organizacao",
                table: "unidades",
                column: "codigo_para_busca")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_unidades_codigo_vivo",
                schema: "organizacao",
                table: "unidades",
                column: "codigo",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_identificador_no_endereco_web_para_busca_trgm",
                schema: "organizacao",
                table: "unidades",
                column: "identificador_no_endereco_web_para_busca")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_unidades_nome_para_busca_trgm",
                schema: "organizacao",
                table: "unidades",
                column: "nome_para_busca")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_unidades_sigla_para_busca_trgm",
                schema: "organizacao",
                table: "unidades",
                column: "sigla_para_busca")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_unidades_sigla_vivo",
                schema: "organizacao",
                table: "unidades",
                column: "sigla",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_superior_id",
                schema: "organizacao",
                table: "unidades",
                column: "unidade_superior_id");

            migrationBuilder.CreateIndex(
                name: "ux_unidades_identificador_no_endereco_web_ativos",
                schema: "organizacao",
                table: "unidades",
                column: "identificador_no_endereco_web",
                unique: true,
                filter: "is_deleted = false");

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
                filter: "situacao = 'PENDENTE'");

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
                name: "ix_versoes_do_termo_consentimento_termo_promovida_em",
                schema: "configuracao",
                table: "versoes_do_termo_consentimento",
                columns: new[] { "termo_consentimento_id", "promovida_em" });

            migrationBuilder.CreateIndex(
                name: "ux_versoes_do_termo_consentimento_termo_numero",
                schema: "configuracao",
                table: "versoes_do_termo_consentimento",
                columns: new[] { "termo_consentimento_id", "numero" },
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
                name: "ix_vinculos_de_discentes_id_discente_sigaa",
                schema: "discentes",
                table: "vinculos_de_discentes",
                column: "id_discente_sigaa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_do_ato_com_entidade_objeto",
                schema: "publicacoes",
                table: "vinculos_do_ato_com_entidade",
                columns: new[] { "entidade_tipo", "entidade_id", "ato_id" });

            migrationBuilder.CreateIndex(
                name: "ux_vinculos_do_ato_com_entidade_trio",
                schema: "publicacoes",
                table: "vinculos_do_ato_com_entidade",
                columns: new[] { "ato_id", "entidade_tipo", "entidade_id" },
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
                name: "fk_anexos_do_pedido_pedidos_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "anexos_do_pedido",
                column: "pedido_id",
                principalSchema: "autorizacao",
                principalTable: "pedidos_de_concessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_decisoes_do_pedido_pedidos_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "decisoes_do_pedido",
                column: "pedido_id",
                principalSchema: "autorizacao",
                principalTable: "pedidos_de_concessao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_leituras_do_anexo_do_pedido_pedido_de_concessao_pedido_id",
                schema: "autorizacao",
                table: "leituras_do_anexo_do_pedido",
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
                name: "fk_termos_de_responsabilidade_versao_do_termo_vigente",
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
                name: "fk_termos_de_responsabilidade_versao_do_termo_vigente",
                schema: "autorizacao",
                table: "termos_de_responsabilidade");

            migrationBuilder.DropTable(
                name: "aceites_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "aceites_do_termo",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "acessos_ao_documento_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "anexos_do_ato",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "anexos_do_ato_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "areas_do_quadro_de_pesos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "areas_dos_pesos_do_enem",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "arquivos_da_apresentacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "atos_da_protecao_do_catalogo",
                schema: "configuracao");

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
                name: "bases_da_permissao_concedida",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "bases_legais_da_comprovacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "categorias_de_documento",
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
                name: "chaves_de_idempotencia",
                schema: "plataforma");

            migrationBuilder.DropTable(
                name: "comprovantes_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicoes_das_exigencias",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicoes_de_atendimento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "condicoes_de_exibicao_do_dado",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicoes_do_calculo_dos_dados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "condicoes_ofertadas",
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
                name: "decisoes_do_pedido",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "deficiencias_atendidas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "destinos_remanejamento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "dias_nao_uteis",
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
                name: "eventos_de_bloqueio_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "exclusividades_de_atos",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "geracoes_de_envio_ao_inep",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "gestores_da_instalacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "historicos_da_obrigatoriedade",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "identificadores_da_unidade",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "instituicoes",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "itens_da_revisao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "itens_de_avaliacao_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "lancamentos_de_nota",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "leituras_do_anexo_do_pedido",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "liberacoes_da_reclassificacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "manifestacoes_da_protecao",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "migracoes_da_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "modalidades",
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
                name: "motivos_de_decisao_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "municipios_da_base_legal_fada",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "municipios_de_prova",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "municipios_do_fada",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "niveis_de_atendimento_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ocorrencias_da_importacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "ofertas_de_curso",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "opcoes_declaradas_fato",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "opcoes_do_tipo_de_conceito",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "opcoes_dos_dados",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "permissoes_da_versao_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "permissoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "permissoes_implantadas",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "precedencias_entre_fases",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "providencias",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "publicacoes_previstas_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "publicacoes_previstas_da_fase",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "rascunhos_processos_seletivos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "recalculos_da_aprovacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "recursos_configurados_da_etapa",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "recursos_configurados_da_fase",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "recursos_de_acessibilidade",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "recursos_ofertados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "referencias_demograficas",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "regras_do_catalogo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_eliminacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "respostas_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "sincronizacoes_com_o_sigaa",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "tentativas_de_instalacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "termos_exigidos_formulario",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "termos_na_instalacao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "tipos_de_ato_publicado",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "tipos_de_banca",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_de_deficiencia",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_de_documento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_de_fase",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_etapa_conceitos",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_processo",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "trocas_de_versao_do_pedido",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "vagas_ofertadas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "versoes_do_termo_consentimento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "vigencias_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "vinculos_de_discentes",
                schema: "discentes");

            migrationBuilder.DropTable(
                name: "vinculos_do_ato_com_entidade",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "documentos_da_versao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "atos_do_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "grupos_do_quadro_de_pesos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "pesos_por_area_do_enem",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "apresentacoes_de_documento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_de_comprovacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "bancas_requeridas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "dados_dos_formularios",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "regras_de_calculo_dos_dados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "chamadas",
                schema: "ingresso");

            migrationBuilder.DropTable(
                name: "envios_de_decisoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "configuracoes_de_remanejamento",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "calendarios_de_dias_uteis",
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
                name: "obrigatoriedades_legais",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "unidades",
                schema: "organizacao");

            migrationBuilder.DropTable(
                name: "revisoes_de_acessos",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "inscritos_da_importacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "anexos_do_pedido",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "reclassificacoes_da_permissao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "bases_legais_do_fada",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "configuracoes_do_fada",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "cursos",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "locais_de_oferta",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "dados_do_candidato",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "concessoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "atendimentos_especializados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "grupos_da_inscricao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "instalacoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "tipos_conceito",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "tipos_etapa",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "configuracoes_de_vagas",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "termos_de_consentimento",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "atos_normativos",
                schema: "publicacoes");

            migrationBuilder.DropTable(
                name: "documentos_a_publicar",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "configuracoes_classificacao",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "documentos_exigidos",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "grupos_dos_formularios",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "calculos_dos_dados",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "suspensoes_do_perfil",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "importacoes_de_notas_do_enem",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "pedidos_de_concessao",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "implantacoes_das_permissoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "campus",
                schema: "configuracao");

            migrationBuilder.DropTable(
                name: "revisoes_das_concessoes",
                schema: "autorizacao");

            migrationBuilder.DropTable(
                name: "inscricoes",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "etapas_processo",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "fases_cronograma",
                schema: "selecao");

            migrationBuilder.DropTable(
                name: "versoes_configuracao",
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
                name: "ordem_do_historico_de_acesso",
                schema: "autorizacao");
        }
    }
}
