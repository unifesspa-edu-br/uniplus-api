namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    // Migration InitialCreate do Selecao: registra no __EFMigrationsHistory o
    // schema canônico do módulo (6 tabelas de domínio + idempotency_cache
    // cross-cutting, ADR-0027).
    //
    // Motivação imediata: standalone retornava 500 em todo POST com
    // Idempotency-Key porque idempotency_cache não existia em runtime
    // (uniplus-api#416). EF Core não permite ModelSnapshot parcial
    // (PendingModelChangesWarning bloqueia MigrateAsync se o DbContext expõe
    // entidades não cobertas pelo snapshot), então a InitialCreate cobre o
    // model completo. Isso reescopa uniplus-api#155 de "criar a InitialCreate"
    // para "refinamentos pós-InitialCreate" (ALTER columns, naming convention,
    // FKs faltantes nas entidades inscricoes/processos_seletivos — ver
    // achados P2 do review).
    //
    // CRÍTICO: Up() é IDEMPOTENTE (CREATE TABLE IF NOT EXISTS / CREATE INDEX
    // IF NOT EXISTS). Razão: ambientes pré-existentes (standalone hoje) já têm
    // as 6 tabelas de domínio criadas por mecanismo legado fora da pipeline
    // EF (não populavam __EFMigrationsHistory). Se Up() usasse o
    // CreateTable não-condicional padrão do EF, MigrateAsync falharia com
    // 42P07 "relation already exists" no primeiro deploy desta migration em
    // ambientes pré-existentes. Bancos novos rodam o Up() em estado vazio
    // e tudo é criado normalmente.
    //
    // Down() é PROIBIDO em produção: derrubaria todas as tabelas com PII
    // (candidatos, inscricoes, editais). Rollback de schema em PROD/HML
    // deve ser feito por procedimento operacional explícito (backup + DROP
    // manual), não por automation. Aqui levanta InvalidOperationException
    // para falhar ruidosamente em qualquer tentativa.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS candidatos (
                ""Id"" uuid NOT NULL,
                cpf character varying(11) NOT NULL,
                nome_civil character varying(300) NOT NULL,
                nome_social character varying(300) NULL,
                email character varying(320) NOT NULL,
                ""DataNascimento"" date NOT NULL,
                ""Telefone"" character varying(20) NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                ""IsDeleted"" boolean NOT NULL,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" text NULL,
                CONSTRAINT ""PK_candidatos"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS editais (
                ""Id"" uuid NOT NULL,
                numero_edital integer NOT NULL,
                ano_edital integer NOT NULL,
                ""Titulo"" character varying(500) NOT NULL,
                ""TipoProcesso"" integer NOT NULL,
                ""Status"" integer NOT NULL,
                periodo_inscricao_inicio timestamp with time zone NULL,
                periodo_inscricao_fim timestamp with time zone NULL,
                fator_divisao numeric(18,4) NULL,
                bonus_regional_percentual numeric(5,2) NULL,
                ""MaximoOpcoesCurso"" integer NOT NULL DEFAULT 1,
                ""BonusRegionalHabilitado"" boolean NOT NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                ""IsDeleted"" boolean NOT NULL,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" text NULL,
                CONSTRAINT ""PK_editais"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS idempotency_cache (
                ""Id"" uuid NOT NULL,
                ""Scope"" character varying(255) NOT NULL,
                ""Endpoint"" character varying(500) NOT NULL,
                ""IdempotencyKey"" character varying(255) NOT NULL,
                ""BodyHash"" character(64) NOT NULL,
                ""Status"" smallint NOT NULL,
                ""ResponseStatus"" integer NULL,
                ""ResponseHeadersJson"" jsonb NULL,
                ""ResponseBodyCipher"" bytea NULL,
                ""ExpiresAt"" timestamp with time zone NOT NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                CONSTRAINT ""PK_idempotency_cache"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS inscricoes (
                ""Id"" uuid NOT NULL,
                ""CandidatoId"" uuid NOT NULL,
                ""EditalId"" uuid NOT NULL,
                ""Modalidade"" integer NOT NULL,
                ""Status"" integer NOT NULL,
                ""CodigoCursoPrimeiraOpcao"" character varying(50) NULL,
                ""CodigoCursoSegundaOpcao"" character varying(50) NULL,
                ""ListaEspera"" boolean NOT NULL,
                ""NumeroInscricao"" character varying(50) NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                ""IsDeleted"" boolean NOT NULL,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" text NULL,
                CONSTRAINT ""PK_inscricoes"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS processos_seletivos (
                ""Id"" uuid NOT NULL,
                ""EditalId"" uuid NOT NULL,
                ""CodigoCurso"" character varying(50) NOT NULL,
                ""NomeCurso"" character varying(300) NOT NULL,
                ""Campus"" character varying(200) NOT NULL,
                ""TotalVagas"" integer NOT NULL,
                ""Turno"" character varying(50) NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                ""IsDeleted"" boolean NOT NULL,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" text NULL,
                CONSTRAINT ""PK_processos_seletivos"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS cotas (
                ""Id"" uuid NOT NULL,
                ""EditalId"" uuid NOT NULL,
                ""Modalidade"" integer NOT NULL,
                ""PercentualVagas"" numeric(5,2) NOT NULL,
                ""Descricao"" character varying(500) NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                ""IsDeleted"" boolean NOT NULL,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" text NULL,
                CONSTRAINT ""PK_cotas"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_cotas_editais_EditalId"" FOREIGN KEY (""EditalId"")
                    REFERENCES editais (""Id"") ON DELETE CASCADE
            );
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS etapas (
                ""Id"" uuid NOT NULL,
                ""EditalId"" uuid NOT NULL,
                ""Nome"" character varying(200) NOT NULL,
                ""Tipo"" integer NOT NULL,
                ""Peso"" numeric(5,2) NOT NULL,
                ""Ordem"" integer NOT NULL,
                ""NotaMinima"" numeric(5,2) NULL,
                ""Eliminatoria"" boolean NOT NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NULL,
                ""IsDeleted"" boolean NOT NULL,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" text NULL,
                CONSTRAINT ""PK_etapas"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_etapas_editais_EditalId"" FOREIGN KEY (""EditalId"")
                    REFERENCES editais (""Id"") ON DELETE CASCADE
            );
        ");

        migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_candidatos_cpf"" ON candidatos (cpf);");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_cotas_EditalId"" ON cotas (""EditalId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_etapas_EditalId"" ON etapas (""EditalId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS idx_idempotency_expires_at ON idempotency_cache (""ExpiresAt"");");
        migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS idx_idempotency_lookup ON idempotency_cache (""Scope"", ""Endpoint"", ""IdempotencyKey"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_inscricoes_CandidatoId_EditalId"" ON inscricoes (""CandidatoId"", ""EditalId"");");
        migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_inscricoes_NumeroInscricao"" ON inscricoes (""NumeroInscricao"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // Rollback automático bloqueado: derrubaria tabelas com PII de candidatos
        // (LGPD) e dados de editais publicados. Operação só pode ser feita por
        // procedimento manual com backup explícito e aprovação operacional —
        // nunca pelo `dotnet ef database update <previous>` no pipeline.
        throw new InvalidOperationException(
            "Rollback de InitialCreate é proibido em automation (LGPD: derrubaria PII de candidatos). "
            + "Para reverter, executar procedimento operacional manual com backup do schema selecao.* + DROP explícito.");
    }
}
