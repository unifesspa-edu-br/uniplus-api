namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Configuração EF Core das vigências do perfil: os intervalos em que cada versão foi a composição em vigor.</summary>
public sealed class VigenciaDoPerfilConfiguration : IEntityTypeConfiguration<VigenciaDoPerfil>
{
    public void Configure(EntityTypeBuilder<VigenciaDoPerfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("vigencias_do_perfil");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Desde).IsRequired();
        builder.Ignore(v => v.Aberta);

        // A chave composta impede a vigência de apontar para a versão de outro perfil.
        builder.HasOne<VersaoDoPerfil>()
            .WithMany()
            .HasForeignKey(v => new { v.PerfilId, v.VersaoId })
            .HasPrincipalKey(v => new { v.PerfilId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Um perfil tem no máximo uma vigência aberta.
        builder.HasIndex(v => v.PerfilId)
            .IsUnique()
            .HasFilter("ate IS NULL")
            .HasDatabaseName("ux_vigencias_do_perfil_aberta");

        builder.Property<uint>("Version").IsRowVersion();
    }
}
