namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.Configurations;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class MatriculaConfiguration : IEntityTypeConfiguration<Matricula>
{
    private const int CodigoDeContratoMaxLength = 30;

    public void Configure(EntityTypeBuilder<Matricula> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        ConfigurarTabela(builder);
        ComentarColunas(builder);
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status)
            .HasMaxLength(CodigoDeContratoMaxLength)
            .HasComment("Situação da matrícula.")
            .IsRequired();
        ConfigurarOferta(builder);
        builder.Property(m => m.Observacoes).HasMaxLength(2000);

        builder.HasMany(m => m.Documentos).WithOne().HasForeignKey(d => d.MatriculaId);
    }

    private static void ConfigurarOferta(EntityTypeBuilder<Matricula> builder)
    {
        builder.OwnsOne(m => m.OfertaCurso, oferta => CopiaDaOfertaDeCursoOwnedConfiguration.Configure(
            oferta, "da convocação que originou a matrícula", ClassificacaoDeProtecao.Pessoal));
        builder.Navigation(m => m.OfertaCurso).IsRequired();
    }

    private static void ConfigurarTabela(EntityTypeBuilder<Matricula> builder)
    {
        builder.ToTable("matriculas", static tabela =>
        {
            tabela.HasComment("Matrículas originadas de convocações.");
            tabela.HasCheckConstraint("ck_matriculas_status", $"status IN ({SqlDosCodigosDeContrato.Lista<StatusMatricula>()})");
            CopiaDaOfertaDeCursoOwnedConfiguration.ConfigurarRestricao(tabela, "matriculas");
        });
    }

    private static void ComentarColunas(EntityTypeBuilder<Matricula> builder)
    {
        builder.Property(m => m.Id).HasComment("Identificador do registro.");
        builder.Property(m => m.ConvocacaoId).HasComment("Identificador da convocação.");
        builder.Property(m => m.CandidatoId).HasComment("Identificador do candidato.");
        builder.Property(m => m.Observacoes).HasComment("Informações registradas durante a análise.");
    }
}
