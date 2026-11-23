namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração EF Core dos lançamentos de nota: uma linha por área do ENEM de cada inscrito do
/// lote. Sem cifra, sem nome, sem CPF e sem data (ADR-0149).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LancamentoDeNotaConfiguration : IEntityTypeConfiguration<LancamentoDeNota>
{
    private const int ItemMaxLength = 40;
    private const int PrecisaoDaNota = 7;
    private const int EscalaDaNota = 3;

    public void Configure(EntityTypeBuilder<LancamentoDeNota> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("lancamentos_de_nota", t =>
        {
            t.HasComment("Uma nota de uma inscrição em uma área do ENEM, com sua situação. Os registros não são alterados ou excluídos; a correção exige importar outro lote.");
            t.HasCheckConstraint("ck_lancamentos_de_nota_nota", "nota BETWEEN 0 AND 1000");
            t.HasCheckConstraint("ck_lancamentos_de_nota_situacao", $"situacao IN ({(int)SituacaoDaProvaNoEnem.Presente}, {(int)SituacaoDaProvaNoEnem.Ausente})");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever().HasComment("Identificador do lançamento da nota.");

        builder.Property(l => l.LoteId).HasColumnName("importacao_id").IsRequired().HasComment("Identificador da importação que originou o lançamento.");
        builder.Property(l => l.InscricaoId).IsRequired().HasComment("Identificador da inscrição à qual pertence a nota.");
        builder.Property(l => l.EtapaProcessoId)
            .IsRequired()
            .HasComment("A etapa do lote, repetida para ler o lançamento sem o lote; a chave estrangeira composta com o lote impede que divirja.");

        builder.Property(l => l.ItemCodigo)
            .HasMaxLength(ItemMaxLength)
            .IsRequired()
            .HasComment("Área do ENEM: REDACAO, CIENCIAS_DA_NATUREZA, CIENCIAS_HUMANAS, LINGUAGENS ou MATEMATICA.");

        builder.Property(l => l.Nota)
            .HasPrecision(PrecisaoDaNota, EscalaDaNota)
            .IsRequired()
            .HasComment("Nota da área do ENEM, de zero a mil, com até três casas decimais.");

        builder.Property(l => l.Situacao)
            .HasConversion<short>()
            .IsRequired()
            .HasComment("1 = presente, 2 = ausente. Quem faltou tem nota zero, que não vale como nota obtida.");

        builder.HasOne<InscritoDoLoteDeNotas>()
            .WithMany()
            .HasForeignKey(l => new { l.LoteId, l.InscricaoId })
            .HasConstraintName("fk_lancamentos_de_nota_inscrito_da_importacao")
            .HasPrincipalKey(i => new { i.LoteId, i.InscricaoId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.LoteId, l.InscricaoId, l.ItemCodigo })
            .IsUnique()
            .HasDatabaseName("ux_lancamentos_de_nota_importacao_inscricao_item");

        builder.HasIndex(l => new { l.InscricaoId, l.EtapaProcessoId })
            .HasDatabaseName("ix_lancamentos_de_nota_inscricao_etapa");
    }
}
