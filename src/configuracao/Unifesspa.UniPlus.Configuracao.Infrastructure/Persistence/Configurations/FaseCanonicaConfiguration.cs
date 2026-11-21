namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal sealed class FaseCanonicaConfiguration : IEntityTypeConfiguration<FaseCanonica>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 300;
    private const int EnumTokenMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int UnidadeSiglaMaxLength = 50;
    private const int UnidadeNomeMaxLength = 250;
    private const int AuditUserMaxLength = 255;

    // Instante fixo do seed (HasData exige valor determinístico).
    private static readonly DateTimeOffset InstanteDoSeed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<FaseCanonica> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("fases_canonicas", ConfigurarChecks);
        builder.HasKey(f => f.Id);
        ConfigurarIdentificacao(builder);
        ConfigurarSinalizadores(builder);
        ConfigurarOrigemDaData(builder);
        ConfigurarUnidadeSugerida(builder);
        ConfigurarAuditoria(builder);
        ConfigurarIndices(builder);
        builder.HasData(MaterializarSeed());
    }

    // Codigo é value object imutável — persistido por valor como varchar via
    // CodigoFaseValueConverter (reidratação fail-fast). O CHECK de formato, o
    // CHECK de conjunto canônico e o índice único parcial protegem a coluna.
    private static void ConfigurarIdentificacao(EntityTypeBuilder<FaseCanonica> builder)
    {
        builder.Property(f => f.Codigo)
            .HasConversion<CodigoFaseValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(f => f.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(f => f.Descricao).HasMaxLength(DescricaoMaxLength);
        builder.Property(f => f.BaseLegal).HasMaxLength(BaseLegalMaxLength);
    }

    private static void ConfigurarOrigemDaData(EntityTypeBuilder<FaseCanonica> builder)
    {
        builder.Property(f => f.OrigemData)
            .HasConversion<OrigemDataFaseValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();
    }

    // Snapshot-copy (ADR-0061) da unidade sugerida: o id sem FK e a cópia de sigla e nome,
    // que o domínio grava e lê juntos — nulos quando a fase não tem sugestão.
    private static void ConfigurarUnidadeSugerida(EntityTypeBuilder<FaseCanonica> builder)
    {
        builder.Property(f => f.UnidadeSugeridaOrigemId);
        builder.Property(f => f.UnidadeSugeridaSigla).HasMaxLength(UnidadeSiglaMaxLength);
        builder.Property(f => f.UnidadeSugeridaNome).HasMaxLength(UnidadeNomeMaxLength);
        builder.Ignore(f => f.UnidadeSugerida);
    }

    // Auditoria (IAuditableEntity)
    private static void ConfigurarAuditoria(EntityTypeBuilder<FaseCanonica> builder)
    {
        builder.Property(f => f.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(f => f.UpdatedBy).HasMaxLength(AuditUserMaxLength);
    }

    // Unicidade do código entre fases vivas (índice parcial) — uma fase viva por
    // código; soft-delete libera o slot para recriação.
    private static void ConfigurarIndices(EntityTypeBuilder<FaseCanonica> builder)
    {
        builder.HasIndex(f => f.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(RestricoesDoBanco.FaseCanonicaCodigoVivo);
    }

    // Os sinalizadores são um valor da fase, gravado como as quatro colunas da própria linha (table
    // splitting): o domínio os lê e os confere juntos, e os CHECKs os cruzam com o código.
    private static void ConfigurarSinalizadores(EntityTypeBuilder<FaseCanonica> builder)
    {
        builder.OwnsOne(f => f.Sinalizadores, ConfigurarColunasDosSinalizadores);
        builder.Navigation(f => f.Sinalizadores).IsRequired();
    }

    private static void ConfigurarColunasDosSinalizadores(OwnedNavigationBuilder<FaseCanonica, SinalizadoresDaFase> sinalizadores)
    {
        sinalizadores.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        sinalizadores.Property(s => s.AgrupaEtapas)
            .HasColumnName("agrupa_etapas").IsRequired().HasDefaultValue(false);
        sinalizadores.Property(s => s.PermiteComplementacao)
            .HasColumnName("permite_complementacao").IsRequired().HasDefaultValue(false);
        sinalizadores.Property(s => s.ColetaInscricao)
            .HasColumnName("coleta_inscricao").IsRequired().HasDefaultValue(false);
        sinalizadores.Property(s => s.ColetaSolicitacaoIsencao)
            .HasColumnName("coleta_solicitacao_isencao").IsRequired().HasDefaultValue(false);
        sinalizadores.HasData(SinalizadoresDoSeed());
    }

    private static IEnumerable<object> SinalizadoresDoSeed() =>
        FaseCanonicaSeed.Itens.Select(item => new
        {
            FaseCanonicaId = item.Id,
            item.AgrupaEtapas,
            item.PermiteComplementacao,
            item.ColetaInscricao,
            item.ColetaSolicitacaoIsencao,
        });

    /// <summary>
    /// Projeta o seed (<see cref="FaseCanonicaSeed.Itens"/>) para linhas que o
    /// <c>HasData</c> congela como literais na migration inicial. O instante é fixo
    /// (as linhas não passam pelo <c>AuditableInterceptor</c>/<c>SoftDeleteInterceptor</c>),
    /// e <c>CreatedBy</c> fica nulo — a marca honesta de quem não tem autor a
    /// registrar, per ADR-0062 Emenda 2.
    /// </summary>
    /// <remarks>
    /// O <c>InsertData</c> da migration inicial só roda em banco vazio, onde nenhuma fase
    /// viva ocupa o código. Alterar linha existente é ato administrativo, com autor, não
    /// efeito de deploy: a lista muda com a migration inicial gerada de novo, nunca como
    /// <c>UpdateData</c> sobre um banco em uso.
    /// </remarks>
    private static IEnumerable<object> MaterializarSeed() =>
        FaseCanonicaSeed.Itens.Select(item => new
        {
            item.Id,
            Codigo = CodigoFase.Criar(item.Codigo).Value!,
            item.Nome,
            item.Descricao,
            item.OrigemData,
            item.BaseLegal,
            CreatedAt = InstanteDoSeed,
            IsDeleted = false,
        });

    private static void ConfigurarChecks(TableBuilder<FaseCanonica> table)
    {
        ConfigurarChecksDoCodigoEDaOrigemDaData(table);
        ConfigurarChecksDosSinalizadoresDaAvaliacao(table);
        ConfigurarChecksDosSinalizadoresDaIsencao(table);
    }

    // Formato fechado do código (alinhado ao value object CodigoFase), domínio fechado das
    // fases canônicas e da origem da data, que tem dois valores (defesa em profundidade).
    private static void ConfigurarChecksDoCodigoEDaOrigemDaData(TableBuilder<FaseCanonica> table)
    {
        table.HasCheckConstraint(
            "ck_fases_canonicas_codigo_formato",
            "codigo ~ '^[A-Z_]+$'");

        table.HasCheckConstraint(
            "ck_fases_canonicas_codigo_canonico",
            $"codigo IN ({TokensSql(FaseCanonicaCatalogo.Codigos)})");

        table.HasCheckConstraint(
            "ck_fases_canonicas_origem_data",
            $"origem_data IN ({TokensSql(OrigensDataFase.TokensCanonicos)})");
    }

    // Coerência: agrupa_etapas verdadeiro apenas para a fase de avaliação, e
    // permite_complementacao verdadeiro apenas nas fases legalmente permitidas.
    private static void ConfigurarChecksDosSinalizadoresDaAvaliacao(TableBuilder<FaseCanonica> table)
    {
        table.HasCheckConstraint(
            "ck_fases_canonicas_agrupa_etapas",
            $"agrupa_etapas = false OR codigo = '{FaseCanonicaCatalogo.CodigoAvaliacao}'");

        table.HasCheckConstraint(
            "ck_fases_canonicas_complementacao",
            $"permite_complementacao = false OR codigo IN ({TokensSql(FaseCanonicaCatalogo.CodigosComComplementacaoPermitida)})");
    }

    // Coerência: a marca de solicitação de isenção existe na fase de isenção, e só nela;
    // coletar inscrição e coletar solicitação de isenção são janelas distintas.
    private static void ConfigurarChecksDosSinalizadoresDaIsencao(TableBuilder<FaseCanonica> table)
    {
        table.HasCheckConstraint(
            "ck_fases_canonicas_solicitacao_isencao",
            $"coleta_solicitacao_isencao = (codigo = '{FaseCanonicaCatalogo.CodigoSolicitacaoIsencao}')");

        table.HasCheckConstraint(
            "ck_fases_canonicas_inscricao_e_isencao_exclusivas",
            "NOT (coleta_inscricao AND coleta_solicitacao_isencao)");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));
}
