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

/// <summary>Configuração EF Core das implantações do catálogo de permissões e das permissões de cada uma, só acrescentadas.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ImplantacaoDasPermissoesConfiguration : IEntityTypeConfiguration<ImplantacaoDasPermissoes>, IEntityTypeConfiguration<PermissaoImplantada>
{
    private const int CodigoMaxLength = 120;
    private const int ModuloMaxLength = 60;
    private const int ClassificacaoMaxLength = 20;
    private const int ImpressaoLength = 64;

    public void Configure(EntityTypeBuilder<ImplantacaoDasPermissoes> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("implantacoes_das_permissoes", tabela => tabela.HasCheckConstraint("ck_implantacoes_das_permissoes_impressao", "impressao ~ '^[0-9a-f]{64}$'"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // A ordem das implantações é do banco; a impressão não é única, porque o mesmo conteúdo volta
        // numa reversão da implantação.
        builder.Property(c => c.Sequencia).UseIdentityAlwaysColumn().ValueGeneratedOnAdd();
        builder.Property(c => c.Impressao).HasColumnType("char(" + ImpressaoLength + ")").IsRequired();
        builder.Property(c => c.ImplantadaEm).IsRequired();

        builder.HasIndex(c => c.Sequencia).IsUnique().HasDatabaseName("ux_implantacoes_das_permissoes_sequencia");
        builder.HasMany(c => c.Permissoes).WithOne().HasForeignKey(p => p.ImplantacaoId)
            .HasConstraintName("fk_permissoes_implantadas_implantacao").OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(c => c.Permissoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    public void Configure(EntityTypeBuilder<PermissaoImplantada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("permissoes_implantadas");
        builder.HasKey(p => new { p.ImplantacaoId, p.Codigo });

        builder.Property(p => p.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(p => p.Modulo).HasMaxLength(ModuloMaxLength).IsRequired();
        builder.Property(p => p.Classificacao).HasConversion(ConversoresDoCatalogo.Classificacao).HasMaxLength(ClassificacaoMaxLength).IsRequired();
        builder.Property(p => p.DeUnidade).IsRequired();
        builder.Property(p => p.Reservada).IsRequired();
    }
}
