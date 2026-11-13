namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core da exceção do termo de responsabilidade na instalação, uma por instalação confirmada.</summary>
public sealed class TermoNaInstalacaoConfiguration : IEntityTypeConfiguration<TermoNaInstalacao>
{
    private const int SituacaoMaxLength = 40;

    private static readonly ValueConverter<SituacaoDoTermoNaInstalacao, string> SituacaoConverter = new(
        situacao => situacao.ToCodigo(),
        codigo => SituacaoDoTermoNaInstalacaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<TermoNaInstalacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("termos_na_instalacao", tabela =>
            tabela.HasCheckConstraint(
                "ck_termos_na_instalacao_versao",
                "(situacao = 'sem_termo_publicado') = (versao_do_termo_id IS NULL) AND (versao_do_termo_id IS NULL) = (numero IS NULL)"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Situacao).HasConversion(SituacaoConverter).HasMaxLength(SituacaoMaxLength).IsRequired();
        builder.Property(t => t.RegistradaEm).IsRequired();

        builder.HasOne<Instalacao>()
            .WithMany()
            .HasForeignKey(t => t.InstalacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VersaoDoTermo>()
            .WithMany()
            .HasForeignKey(t => t.VersaoDoTermoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.InstalacaoId).IsUnique().HasDatabaseName("ux_termos_na_instalacao_instalacao");
    }
}
