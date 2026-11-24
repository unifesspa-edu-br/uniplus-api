namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class MatriculaConfiguration : IEntityTypeConfiguration<Matricula>
{
    private const int CodigoDeContratoMaxLength = 30;

    public void Configure(EntityTypeBuilder<Matricula> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("matriculas", static tabela => tabela.HasCheckConstraint(
            "ck_matriculas_status",
            $"status IN ({SqlDosCodigosDeContrato.Lista<StatusMatricula>()})"));
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da matrícula. Códigos: PENDENTE, DOCUMENTACAO_ENVIADA, EM_ANALISE, DEFERIDA, INDEFERIDA, EFETIVADA, CANCELADA.")
            .IsRequired();
        builder.Property(m => m.CodigoCurso).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Observacoes).HasMaxLength(2000);

        builder.HasMany(m => m.Documentos).WithOne().HasForeignKey(d => d.MatriculaId);
    }
}
