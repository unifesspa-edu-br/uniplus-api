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

/// <summary>Configuração EF Core das suspensões do perfil: o registro de estado, com no máximo uma aberta por perfil.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class SuspensaoDoPerfilConfiguration : IEntityTypeConfiguration<SuspensaoDoPerfil>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    private static readonly ValueConverter<EtapaDaVersao, string> PapelConverter = new(
        papel => papel.ToCodigo(),
        codigo => EtapaDaVersaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<SuspensaoDoPerfil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("suspensoes_do_perfil", tabela => tabela.HasCheckConstraint(
            "ck_suspensoes_do_perfil_papel",
            $"papel_de_quem_suspendeu IN ('{EtapaDaVersaoCodigo.GestaoDeAcessos}', '{EtapaDaVersaoCodigo.ProtecaoDeDados}')"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.SuspensaEm).IsRequired();
        builder.Property(s => s.SuspensaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(s => s.SuspensaPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(s => s.SuspensaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(s => s.Papel).HasColumnName("papel_de_quem_suspendeu").HasConversion(PapelConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(s => s.Motivo).HasMaxLength(MotivosDaVersao.MaxLength).IsRequired();
        builder.Property(s => s.OrdemNaTrilha).IsRequired();

        builder.Ignore(s => s.Aberta);
        builder.Ignore(s => s.SuspensaPor);
        builder.Ignore(s => s.OrdinalLocal);

        // No máximo uma suspensão aberta por perfil: a segunda, simultânea, é barrada aqui se escapar da trava do perfil.
        builder.HasIndex(s => s.PerfilId).IsUnique().HasFilter("retomada_em IS NULL").HasDatabaseName("ux_suspensoes_do_perfil_aberta");
        builder.HasIndex(s => s.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_suspensoes_do_perfil_ordem");

        builder.HasMany(s => s.Decisoes).WithOne().HasForeignKey(d => d.SuspensaoId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(s => s.Decisoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<uint>("Version").IsRowVersion();
    }
}
