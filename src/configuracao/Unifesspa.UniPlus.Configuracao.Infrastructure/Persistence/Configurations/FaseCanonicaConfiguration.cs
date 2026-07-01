namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class FaseCanonicaConfiguration : IEntityTypeConfiguration<FaseCanonica>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 300;
    private const int EnumTokenMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<FaseCanonica> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("fase_canonica", ConfigurarChecks);

        builder.HasKey(f => f.Id);

        // Codigo é value object imutável — persistido por valor como varchar via
        // CodigoFaseValueConverter (reidratação fail-fast). O CHECK de formato, o
        // CHECK de conjunto canônico e o índice único parcial protegem a coluna.
        builder.Property(f => f.Codigo)
            .HasConversion<CodigoFaseValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(f => f.Nome).HasMaxLength(NomeMaxLength).IsRequired();

        builder.Property(f => f.Descricao).HasMaxLength(DescricaoMaxLength);

        builder.Property(f => f.DonoTipico)
            .HasConversion<DonoTipicoValueConverter>()
            .HasMaxLength(EnumTokenMaxLength)
            .IsRequired();

        builder.Property(f => f.AgrupaEtapas).IsRequired().HasDefaultValue(false);

        builder.Property(f => f.PermiteComplementacao).IsRequired().HasDefaultValue(false);

        builder.Property(f => f.BaseLegal).HasMaxLength(BaseLegalMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(f => f.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(f => f.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade do código entre fases vivas (índice parcial) — uma fase viva por
        // código; soft-delete libera o slot para recriação.
        builder.HasIndex(f => f.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_fase_canonica_codigo_vivo");
    }

    private static void ConfigurarChecks(TableBuilder<FaseCanonica> table)
    {
        // Formato fechado do código — alinhado ao value object CodigoFase.
        table.HasCheckConstraint(
            "ck_fase_canonica_codigo_formato",
            "codigo ~ '^[A-Z_]+$'");

        // Domínio fechado das quatorze fases canônicas (defesa em profundidade).
        table.HasCheckConstraint(
            "ck_fase_canonica_codigo_canonico",
            $"codigo IN ({TokensSql(FaseCanonicaCatalogo.Codigos)})");

        // Domínio fechado do dono típico (quatro valores).
        table.HasCheckConstraint(
            "ck_fase_canonica_dono_tipico",
            $"dono_tipico IN ({TokensSql(DonosTipicos.TokensCanonicos)})");

        // Coerência: agrupa_etapas verdadeiro apenas para a fase de avaliação.
        table.HasCheckConstraint(
            "ck_fase_canonica_agrupa_etapas",
            $"agrupa_etapas = false OR codigo = '{FaseCanonicaCatalogo.CodigoAvaliacao}'");

        // Coerência: permite_complementacao verdadeiro apenas nas fases legalmente permitidas.
        table.HasCheckConstraint(
            "ck_fase_canonica_complementacao",
            $"permite_complementacao = false OR codigo IN ({TokensSql(FaseCanonicaCatalogo.CodigosComComplementacaoPermitida)})");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));
}
