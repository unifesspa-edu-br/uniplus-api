namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TipoDocumentoConfiguration
    : IEntityTypeConfiguration<TipoDocumento>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int CategoriaMaxLength = 30;
    private const int FormatosAceitosMaxLength = 200;
    private const int TipoEquivalenteMaxLength = 60;

    public void Configure(EntityTypeBuilder<TipoDocumento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "tipo_documento",
            t =>
            {
                // Domínio fechado da categoria (sete tokens canônicos UPPER_SNAKE da #591).
                t.HasCheckConstraint(
                    "ck_tipo_documento_categoria",
                    $"categoria IN ({string.Join(", ", CategoriaDocumentos.TokensCanonicos.Select(token => $"'{token}'"))})");

                // Tipo equivalente é rótulo classificatório: nunca aponta para o próprio
                // código. Null-safe (a coluna é opcional). Case-sensitive, alinhado ao
                // guard de domínio (StringComparison.Ordinal).
                t.HasCheckConstraint(
                    "ck_tipo_documento_equivalente_diferente_codigo",
                    "tipo_equivalente IS NULL OR tipo_equivalente <> codigo");

                // Tamanho máximo (quando informado) é positivo — defesa em profundidade
                // do invariante de domínio contra inserts crus (espelha a proteção
                // numérica de peso_area_enem). Null-safe (a coluna é opcional).
                t.HasCheckConstraint(
                    "ck_tipo_documento_tamanho_maximo_mb_positivo",
                    "tamanho_maximo_mb IS NULL OR tamanho_maximo_mb > 0");
            });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(t => t.Descricao).HasMaxLength(DescricaoMaxLength);

        // Categoria é enum persistido por valor como o token canônico UPPER_SNAKE via
        // CategoriaDocumentoValueConverter (reidratação fail-fast). O CHECK acima
        // restringe a coluna ao domínio fechado.
        builder.Property(t => t.Categoria)
            .HasConversion<CategoriaDocumentoValueConverter>()
            .HasMaxLength(CategoriaMaxLength)
            .IsRequired();

        builder.Property(t => t.FormatosAceitos).HasMaxLength(FormatosAceitosMaxLength);
        builder.Property(t => t.TamanhoMaximoMb);
        builder.Property(t => t.TipoEquivalente).HasMaxLength(TipoEquivalenteMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(t => t.CreatedBy).HasMaxLength(255);
        builder.Property(t => t.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre tipos vivos (índice parcial) — um tipo vivo por
        // código; soft-delete libera o slot para recriação.
        builder.HasIndex(t => t.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_documento_codigo_vivo");

        // Índice de filtro por categoria na interface administrativa.
        builder.HasIndex(t => t.Categoria)
            .HasDatabaseName("ix_tipo_documento_categoria");
    }
}
