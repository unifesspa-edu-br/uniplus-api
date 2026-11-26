namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das divergências dos perfis de governança, registro só acrescentado.</summary>
internal sealed class DivergenciaDaGovernancaConfiguration : IEntityTypeConfiguration<DivergenciaDaGovernanca>
{
    private const int CodigoMaxLength = 120;
    private const int TipoMaxLength = 40;
    private const int ValorMaxLength = 40;

    public void Configure(EntityTypeBuilder<DivergenciaDaGovernanca> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("divergencias_da_governanca", tabela => tabela.HasCheckConstraint(
            "ck_divergencias_da_governanca_tipo",
            $"tipo IN ({SqlDosCodigosDeContrato.Lista<TipoDeDivergencia>()})"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Tipo).HasMaxLength(TipoMaxLength).IsRequired();
        builder.Property(d => d.De).HasMaxLength(ValorMaxLength);
        builder.Property(d => d.Para).HasMaxLength(ValorMaxLength);
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.OrdemNoHistorico).IsRequired();
        builder.Ignore(d => d.OrdinalLocal);

        builder.HasOne<ImplantacaoDasPermissoes>().WithMany().HasForeignKey(d => d.ImplantacaoId)
            .HasConstraintName("fk_divergencias_da_governanca_implantacao").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Perfil>().WithMany().HasForeignKey(d => d.PerfilId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.OrdemNoHistorico).IsUnique().HasDatabaseName("ux_divergencias_da_governanca_ordem");
    }
}
