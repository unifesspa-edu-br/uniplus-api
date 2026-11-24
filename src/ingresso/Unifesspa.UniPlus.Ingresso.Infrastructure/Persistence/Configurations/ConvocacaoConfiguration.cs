namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ConvocacaoConfiguration : IEntityTypeConfiguration<Convocacao>
{
    private const int CodigoDeContratoMaxLength = 30;
    private const string IndiceProtocolo = "ux_convocacoes_protocolo";

    public void Configure(EntityTypeBuilder<Convocacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("convocacoes", static tabela => tabela.HasCheckConstraint(
            "ck_convocacoes_status",
            $"status IN ({SqlDosCodigosDeContrato.Lista<StatusConvocacao>()})"));
        builder.HasKey(c => c.Id);

        builder.OwnsOne(c => c.Protocolo, pb =>
        {
            pb.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);
            pb.Property(p => p.Valor).HasColumnName("protocolo").HasMaxLength(50).IsRequired();
            pb.HasIndex(p => p.Valor).IsUnique().HasDatabaseName(IndiceProtocolo);
        });

        builder.Property(c => c.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da convocação. Códigos: PENDENTE, ACEITA, RECUSADA, NAO_COMPARECEU, EXPIRADA.")
            .IsRequired();
        builder.Property(c => c.Posicao).IsRequired();
        builder.Property(c => c.CodigoCurso).HasMaxLength(50).IsRequired();
    }
}
