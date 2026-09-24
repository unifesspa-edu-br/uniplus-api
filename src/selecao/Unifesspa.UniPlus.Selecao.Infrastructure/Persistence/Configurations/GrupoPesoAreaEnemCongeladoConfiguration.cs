namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Configuração EF Core de <see cref="GrupoPesoAreaEnemCongelado"/> — um grupo de área do
/// quadro de pesos por área congelado na <see cref="ConfiguracaoClassificacao"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class GrupoPesoAreaEnemCongeladoConfiguration : IEntityTypeConfiguration<GrupoPesoAreaEnemCongelado>
{
    public void Configure(EntityTypeBuilder<GrupoPesoAreaEnemCongelado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("grupos_peso_area_enem_congelados", t => t.HasComment(
            "Quadro de pesos por área do ENEM congelado na classificação do processo seletivo: uma linha " +
            "por grupo de área, copiada por valor da resolução de Pesos por Área declarada. Substituída " +
            "por inteiro quando a classificação é redefinida."));
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .ValueGeneratedNever()
            .HasComment("Identificador interno (UUIDv7) do grupo congelado.");

        builder.Property(g => g.ConfiguracaoClassificacaoId)
            .HasComment("Id da configuração de classificação dona do quadro (FK, cascade delete).");

        builder.Property(g => g.CreatedAt)
            .HasComment("Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor).");
        builder.Property(g => g.UpdatedAt)
            .HasComment("Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).");

        // Um grupo aparece uma vez por classificação. O índice único que garante isso no banco
        // (configuracao_classificacao_id, grupo_area_enem_codigo) combina a FK com uma coluna do
        // tipo owned, o que o modelo do EF não expressa: vive na migration, em SQL.
        builder.OwnsOne(g => g.GrupoAreaEnem, grupo =>
        {
            grupo.Property(v => v.Codigo)
                .HasColumnName("grupo_area_enem_codigo")
                .HasMaxLength(LimitesDoEnvelope.GrupoAreaEnemCodigo)
                .IsRequired()
                .HasComment("Código do grupo de área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área; casa a oferta com a linha do quadro.");
            grupo.Property(v => v.Rotulo)
                .HasColumnName("grupo_area_enem_rotulo")
                .HasMaxLength(LimitesDoEnvelope.GrupoAreaEnemRotulo)
                .IsRequired()
                .HasComment("Rótulo do grupo de área do ENEM, copiado junto do código.");
        });
        builder.Navigation(g => g.GrupoAreaEnem).IsRequired();

        builder.Property(g => g.BaseLegal)
            .HasColumnName("base_legal")
            .HasMaxLength(LimitesDoEnvelope.BaseLegalPesoAreaEnem)
            .IsRequired()
            .HasComment("Dispositivo legal que fundamenta os pesos do grupo, copiado da resolução de Pesos por Área.");

        builder.HasMany(g => g.Areas)
            .WithOne()
            .HasForeignKey(a => a.GrupoPesoAreaEnemCongeladoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Areas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
