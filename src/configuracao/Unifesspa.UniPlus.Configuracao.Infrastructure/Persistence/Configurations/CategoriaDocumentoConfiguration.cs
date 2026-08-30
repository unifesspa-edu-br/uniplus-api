namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CategoriaDocumentoConfiguration : IEntityTypeConfiguration<CategoriaDocumento>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<CategoriaDocumento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "categoria_documento",
            t =>
            {
                // Formato fechado do código (UPPER_SNAKE iniciando por letra) — defesa
                // em profundidade do invariante de domínio (CodigoCategoriaDocumento)
                // contra inserts crus. Case-sensitive, alinhado ao value object.
                t.HasCheckConstraint(
                    "ck_categoria_documento_codigo_formato",
                    "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");

                // Ordem de exibição é posição, não pode ser negativa — mesmo guarda do
                // agregado, aplicado também contra inserts crus.
                t.HasCheckConstraint(
                    "ck_categoria_documento_ordem_nao_negativa",
                    "ordem >= 0");
            });

        builder.HasKey(c => c.Id);

        // Codigo é value object — persistido por valor como varchar via
        // CodigoCategoriaDocumentoValueConverter (reidratação fail-fast). O nome de
        // coluna snake_case vem da convenção global; o CHECK acima restringe o formato.
        builder.Property(c => c.Codigo)
            .HasConversion<CodigoCategoriaDocumentoValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(c => c.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(c => c.Descricao).HasMaxLength(DescricaoMaxLength);
        builder.Property(c => c.Ordem).IsRequired();

        // Auditoria (IAuditableEntity)
        builder.Property(c => c.CreatedBy).HasMaxLength(255);
        builder.Property(c => c.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre categorias vivas (índice parcial) — uma categoria
        // viva por código; soft-delete libera o slot para recriação.
        builder.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_categoria_documento_codigo_vivo");

        // Exibição do catálogo é sempre por (ordem, codigo) — a ordem sozinha não
        // desempata, e o código é a chave natural estável para o desempate.
        builder.HasIndex(c => new { c.Ordem, c.Codigo })
            .HasDatabaseName("ix_categoria_documento_ordem_codigo");
    }
}
