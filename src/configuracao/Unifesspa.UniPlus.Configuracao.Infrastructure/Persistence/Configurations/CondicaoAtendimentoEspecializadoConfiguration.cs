namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CondicaoAtendimentoEspecializadoConfiguration
    : IEntityTypeConfiguration<CondicaoAtendimentoEspecializado>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<CondicaoAtendimentoEspecializado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "condicao_atendimento_especializado",
            t =>
            {
                // Formato fechado do código (UPPER_SNAKE iniciando por letra) — defesa
                // em profundidade do invariante de domínio (CodigoCondicao) contra
                // inserts crus. Case-sensitive, alinhado ao value object.
                t.HasCheckConstraint(
                    "ck_condicao_atendimento_especializado_codigo_formato",
                    "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
            });

        builder.HasKey(c => c.Id);

        // Codigo é value object — persistido por valor como varchar via
        // CodigoCondicaoValueConverter (reidratação fail-fast). O nome de coluna
        // snake_case vem da convenção global; o CHECK acima restringe o formato.
        builder.Property(c => c.Codigo)
            .HasConversion<CodigoCondicaoValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(c => c.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(c => c.Descricao).HasMaxLength(DescricaoMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(c => c.CreatedBy).HasMaxLength(255);
        builder.Property(c => c.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre condições vivas (índice parcial) — uma condição
        // viva por código; soft-delete libera o slot para recriação.
        builder.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_condicao_atendimento_especializado_codigo_vivo");
    }
}
