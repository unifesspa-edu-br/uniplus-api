namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core do estado de entrega dos avisos da reclassificação: a unicidade por origem e fato deduplica o aviso.
/// </summary>
internal sealed class AvisoDaReclassificacaoConfiguration : IEntityTypeConfiguration<AvisoDaReclassificacao>
{
    private const int OrigemMaxLength = 40;

    public void Configure(EntityTypeBuilder<AvisoDaReclassificacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("avisos_da_reclassificacao");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Origem).HasMaxLength(OrigemMaxLength).IsRequired();
        builder.Property(a => a.OrigemId).IsRequired();
        builder.Property(a => a.EnviadoEm).IsRequired();

        builder.HasIndex(a => new { a.Origem, a.OrigemId }).IsUnique().HasDatabaseName("ux_avisos_da_reclassificacao_fato");
    }
}
