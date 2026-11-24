namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ChamadaConfiguration : IEntityTypeConfiguration<Chamada>
{
    private const int CodigoDeContratoMaxLength = 30;

    public void Configure(EntityTypeBuilder<Chamada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("chamadas", static tabela => tabela.HasCheckConstraint(
            "ck_chamadas_status",
            $"status IN ({SqlDosCodigosDeContrato.Lista<StatusChamada>()})"));
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Numero).IsRequired();
        builder.Property(c => c.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da chamada. Códigos: AGENDADA, EM_ANDAMENTO, CONCLUIDA, CANCELADA.")
            .IsRequired();
        builder.Property(c => c.DataPublicacao).IsRequired();
        builder.Property(c => c.PrazoManifestacao).IsRequired();

        builder.HasMany(c => c.Convocacoes).WithOne().HasForeignKey(cv => cv.ChamadaId);

        builder.HasIndex(c => new { c.EditalId, c.Numero }).IsUnique();
    }
}
