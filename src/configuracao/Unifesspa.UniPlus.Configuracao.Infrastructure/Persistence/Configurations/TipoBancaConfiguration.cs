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
internal sealed class TipoBancaConfiguration : IEntityTypeConfiguration<TipoBanca>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int FaseTipicaMaxLength = 60;
    private const int DescricaoMaxLength = 300;
    private const int AuditUserMaxLength = 255;

    public void Configure(EntityTypeBuilder<TipoBanca> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tipo_banca", ConfigurarChecks);

        builder.HasKey(b => b.Id);

        // Codigo é value object imutável — persistido por valor como varchar via
        // CodigoBancaValueConverter (reidratação fail-fast). O CHECK de formato, o
        // CHECK de conjunto canônico e o índice único parcial protegem a coluna.
        builder.Property(b => b.Codigo)
            .HasConversion<CodigoBancaValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(b => b.Nome).HasMaxLength(NomeMaxLength).IsRequired();

        // fase_tipica: rótulo de texto orientativo (NÃO é FK para fase_canonica).
        builder.Property(b => b.FaseTipica).HasMaxLength(FaseTipicaMaxLength);

        builder.Property(b => b.Descricao).HasMaxLength(DescricaoMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(b => b.CreatedBy).HasMaxLength(AuditUserMaxLength);
        builder.Property(b => b.UpdatedBy).HasMaxLength(AuditUserMaxLength);

        // Unicidade do código entre tipos de banca vivos (índice parcial).
        builder.HasIndex(b => b.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_banca_codigo_vivo");
    }

    private static void ConfigurarChecks(TableBuilder<TipoBanca> table)
    {
        // Formato fechado do código — alinhado ao value object CodigoBanca.
        table.HasCheckConstraint(
            "ck_tipo_banca_codigo_formato",
            "codigo ~ '^[A-Z_]+$'");

        // Domínio fechado das quatro bancas canônicas (defesa em profundidade).
        table.HasCheckConstraint(
            "ck_tipo_banca_codigo_canonico",
            $"codigo IN ({TokensSql(TipoBancaCatalogo.Codigos)})");
    }

    private static string TokensSql(IReadOnlyList<string> tokens) =>
        string.Join(", ", tokens.Select(token => $"'{token}'"));
}
