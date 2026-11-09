namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core do registro de avisos emitidos. A gravação é o <c>INSERT ... ON CONFLICT DO NOTHING</c> do
/// <see cref="RegistroDeAvisos"/>; o mapeamento existe para a tabela e a classificação entrarem no modelo.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AvisoEmitidoConfiguration : IEntityTypeConfiguration<AvisoEmitido>
{
    private const int TipoMaxLength = 60;

    public void Configure(EntityTypeBuilder<AvisoEmitido> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // O identificador é pseudonimizado: quem conhece o fato e uma identidade candidata recalcula o hash.
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("avisos_emitidos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("mensagem_id").ValueGeneratedNever();

        builder.Property(a => a.Tipo).HasMaxLength(TipoMaxLength).IsRequired();
        builder.Property(a => a.FatoId).IsRequired();
        builder.Property(a => a.EmitidoEm).IsRequired();

        builder.HasIndex(a => new { a.Tipo, a.FatoId }).HasDatabaseName("ix_avisos_emitidos_tipo_fato");
    }
}
