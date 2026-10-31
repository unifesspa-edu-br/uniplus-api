namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Configuração EF Core da revisão periódica dos acessos e dos itens dela.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RevisaoDeAcessosConfiguration : IEntityTypeConfiguration<RevisaoDeAcessos>, IEntityTypeConfiguration<ItemDaRevisao>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    private static readonly ValueConverter<TipoDeGovernanca, string> GovernancaConverter = new(
        governanca => governanca.ToCodigo(),
        codigo => TipoDeGovernancaCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<ResultadoDoItemDaRevisao, string> ResultadoConverter = new(
        resultado => resultado.ToCodigo(),
        codigo => ResultadoDoItemDaRevisaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<RevisaoDeAcessos> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("revisoes_de_acessos");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.AbertaEm).IsRequired();
        builder.Property(r => r.AbertaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(r => r.AbertaPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(r => r.AbertaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);

        builder.Ignore(r => r.AbertaPor);

        builder.Navigation(r => r.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(r => r.Itens)
            .WithOne()
            .HasForeignKey(i => i.RevisaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119).
        builder.Property<uint>("Version").IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<ItemDaRevisao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O banco confere o que o agregado já garante: a decisão é gravada inteira ou não é.
        builder.ToTable("revisao_itens", tabela => tabela.HasCheckConstraint(
            "ck_revisao_itens_decisao_completa",
            "(resultado IS NULL AND decidido_por_emissor IS NULL AND decidido_por_sub IS NULL AND decidido_em IS NULL)"
            + " OR (resultado IS NOT NULL AND decidido_por_emissor IS NOT NULL AND decidido_por_sub IS NOT NULL AND decidido_em IS NOT NULL)"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Decide).HasConversion(GovernancaConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(i => i.Resultado).HasConversion(ResultadoConverter).HasMaxLength(CodigoMaxLength);
        builder.Property(i => i.DecididoPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(i => i.DecididoPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(i => i.DecididoPorNome).HasMaxLength(NomeDeExibicao.MaxLength);

        builder.Ignore(i => i.Pendente);
        builder.Ignore(i => i.DecididoPor);

        // A concessão é do mesmo módulo. A chave estrangeira mantém o item sempre apontando para uma concessão que existe.
        builder.HasOne<Concessao>()
            .WithMany()
            .HasForeignKey(i => i.ConcessaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cada concessão aparece uma vez por revisão.
        builder.HasIndex(i => new { i.RevisaoId, i.ConcessaoId })
            .IsUnique()
            .HasDatabaseName("ux_revisao_itens_revisao_concessao");

        // Trilha da concessão e fila de pendentes.
        builder.HasIndex(i => i.ConcessaoId).HasDatabaseName("ix_revisao_itens_concessao");

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119): duas decisões do mesmo item não se misturam.
        builder.Property<uint>("Version").IsRowVersion();
    }
}
