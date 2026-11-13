namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core do registro único da vigência do termo de responsabilidade. A linha nasce com a carga inicial, sem
/// versão vigente, para que a trava da vigência exista antes da primeira publicação.
/// </summary>
public sealed class TermoDeResponsabilidadeConfiguration : IEntityTypeConfiguration<TermoDeResponsabilidade>
{
    private static readonly DateTimeOffset CargaInicial = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<TermoDeResponsabilidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("termos_de_responsabilidade", tabela =>
            tabela.HasCheckConstraint(
                "ck_termos_de_responsabilidade_vigente",
                "(versao_vigente_id IS NULL) = (ultimo_numero = 0)"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.UltimoNumero).IsRequired();

        // A vigente é sempre uma versão deste termo.
        builder.HasOne<VersaoDoTermo>()
            .WithMany()
            .HasForeignKey(t => t.VersaoVigenteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119), além da trava pessimista da publicação.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasData(new
        {
            Id = TermoDeResponsabilidade.RegistroUnicoId,
            UltimoNumero = 0,
            CreatedAt = CargaInicial,
        });
    }
}
