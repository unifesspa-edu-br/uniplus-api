namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Configuração EF Core de <see cref="GrupoPesoAreaEnemCongelado"/> — um grupo de área do
/// quadro de pesos por área congelado na <see cref="ConfiguracaoClassificacao"/>.
/// </summary>
internal sealed class GrupoPesoAreaEnemCongeladoConfiguration : IEntityTypeConfiguration<GrupoPesoAreaEnemCongelado>
{
    public void Configure(EntityTypeBuilder<GrupoPesoAreaEnemCongelado> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("grupos_do_quadro_de_pesos", t => t.HasComment(
            "Quadro de pesos por área do ENEM copiado da resolução quando a classificação é definida e substituído quando ela muda; uma linha por grupo de área."));
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .ValueGeneratedNever()
            .HasComment("Identificador interno (UUIDv7) do grupo do quadro de pesos.");

        builder.Property(g => g.ConfiguracaoClassificacaoId)
            .HasComment("Id da configuração de classificação dona do quadro (FK, cascade delete).");


        // Um grupo aparece uma vez por classificação. O índice único que garante isso no banco
        // (configuracao_classificacao_id, grupo_area_enem_codigo) combina a FK com uma coluna do
        // tipo owned, o que o modelo do EF não expressa: vive em script SQL (002_grupo_unico_no_quadro_de_pesos.sql).
        builder.OwnsOne(g => g.GrupoAreaEnem, grupo =>
        {
            grupo.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
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
            .HasConstraintName("fk_areas_do_quadro_de_pesos_grupo_do_quadro_de_pesos")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Areas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
