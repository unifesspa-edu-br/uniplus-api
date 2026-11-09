namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core dos recálculos da classe de aprovação da versão do perfil, registro só acrescentado.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RecalculoDaAprovacaoConfiguration : IEntityTypeConfiguration<RecalculoDaAprovacao>
{
    public void Configure(EntityTypeBuilder<RecalculoDaAprovacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("recalculos_da_aprovacao");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.CicloAnterior).IsRequired();
        builder.Property(r => r.ExigiaAntes).IsRequired();
        builder.Property(r => r.ExigeAgora).IsRequired();
        builder.Property(r => r.Instante).IsRequired();
    }
}
