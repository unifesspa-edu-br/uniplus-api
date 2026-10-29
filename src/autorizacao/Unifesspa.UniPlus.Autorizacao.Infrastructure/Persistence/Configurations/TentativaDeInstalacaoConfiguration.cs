namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Configuração EF Core das tentativas de confirmar a instalação.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TentativaDeInstalacaoConfiguration : IEntityTypeConfiguration<TentativaDeInstalacao>
{
    private const int IdentidadeMaxLength = 255;
    private const int ResultadoMaxLength = 20;

    private static readonly ValueConverter<ResultadoDaTentativaDeInstalacao, string> ResultadoConverter = new(
        resultado => resultado.ToCodigo(),
        codigo => ResultadoDaTentativaDeInstalacaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<TentativaDeInstalacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("instalacao_tentativas");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.AutorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(t => t.AutorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(t => t.Resultado).HasConversion(ResultadoConverter).HasMaxLength(ResultadoMaxLength).IsRequired();
        builder.Property(t => t.Instante).IsRequired();

        // A instalação é do mesmo módulo; a tentativa sem pendência não aponta para nenhuma.
        builder.HasOne<Instalacao>()
            .WithMany()
            .HasForeignKey(t => t.InstalacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.InstalacaoId).HasDatabaseName("ix_instalacao_tentativas_instalacao");
    }
}
