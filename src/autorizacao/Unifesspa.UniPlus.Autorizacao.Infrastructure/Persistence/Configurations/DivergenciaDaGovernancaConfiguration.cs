namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das divergências dos perfis de governança, registro só acrescentado.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DivergenciaDaGovernancaConfiguration : IEntityTypeConfiguration<DivergenciaDaGovernanca>
{
    private const int CodigoMaxLength = 120;
    private const int TipoMaxLength = 40;
    private const int ValorMaxLength = 40;

    private static readonly ValueConverter<TipoDeDivergencia, string> TipoConverter = new(
        tipo => tipo.ToCodigo(),
        codigo => TipoDeDivergenciaCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<DivergenciaDaGovernanca> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("divergencias_da_governanca", tabela => tabela.HasCheckConstraint(
            "ck_divergencias_da_governanca_tipo",
            $"tipo IN ('{TipoDeDivergenciaCodigo.ComposicaoFaltante}', '{TipoDeDivergenciaCodigo.ComposicaoExcedente}',"
            + $" '{TipoDeDivergenciaCodigo.Classificacao}', '{TipoDeDivergenciaCodigo.Escopo}')"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Tipo).HasConversion(TipoConverter).HasMaxLength(TipoMaxLength).IsRequired();
        builder.Property(d => d.De).HasMaxLength(ValorMaxLength);
        builder.Property(d => d.Para).HasMaxLength(ValorMaxLength);
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.OrdemNaTrilha).IsRequired();
        builder.Ignore(d => d.OrdinalLocal);

        builder.HasOne<ImplantacaoDasPermissoes>().WithMany().HasForeignKey(d => d.ImplantacaoId)
            .HasConstraintName("fk_divergencias_da_governanca_implantacao").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Perfil>().WithMany().HasForeignKey(d => d.PerfilId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_divergencias_da_governanca_ordem");
    }
}
