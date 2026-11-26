namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das tentativas de confirmar a instalação.</summary>
internal sealed class TentativaDeInstalacaoConfiguration : IEntityTypeConfiguration<TentativaDeInstalacao>
{
    private const int IdentidadeMaxLength = 255;
    private const int ResultadoMaxLength = 20;

    public void Configure(EntityTypeBuilder<TentativaDeInstalacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("tentativas_de_instalacao");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.AutorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(t => t.AutorContaId).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(t => t.Resultado).HasMaxLength(ResultadoMaxLength).IsRequired();
        builder.Property(t => t.Instante).IsRequired();

        // A instalação é do mesmo módulo; a tentativa sem pendência não aponta para nenhuma.
        builder.HasOne<Instalacao>()
            .WithMany()
            .HasForeignKey(t => t.InstalacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.InstalacaoId).HasDatabaseName("ix_tentativas_de_instalacao_instalacao");
    }
}
