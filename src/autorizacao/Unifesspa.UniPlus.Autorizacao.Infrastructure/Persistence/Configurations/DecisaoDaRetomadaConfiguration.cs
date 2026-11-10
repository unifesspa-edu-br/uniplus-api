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

/// <summary>Configuração EF Core das decisões das etapas da retomada, registro só acrescentado.</summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DecisaoDaRetomadaConfiguration : IEntityTypeConfiguration<DecisaoDaRetomada>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    private static readonly ValueConverter<EtapaDaVersao, string> EtapaConverter = new(
        etapa => etapa.ToCodigo(),
        codigo => EtapaDaVersaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<DecisaoDaRetomada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("decisoes_da_retomada", tabela => tabela.HasCheckConstraint(
            "ck_decisoes_da_retomada_etapa",
            $"etapa IN ('{EtapaDaVersaoCodigo.GestaoDeAcessos}', '{EtapaDaVersaoCodigo.ProtecaoDeDados}')"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Etapa).HasConversion(EtapaConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.DecisorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(d => d.DecisorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(d => d.DecisorTemConcessao).IsRequired();
        builder.Property(d => d.Motivo).HasMaxLength(MotivosDaVersao.MaxLength).IsRequired();
        builder.Property(d => d.PerfilProtegidoNaDecisao).IsRequired();
        builder.Property(d => d.Instante).IsRequired();
        builder.Property(d => d.OrdemNaTrilha).IsRequired();

        builder.Ignore(d => d.Decisor);
        builder.Ignore(d => d.OrdinalLocal);

        // Uma decisão por etapa em cada suspensão: a segunda, simultânea, é barrada aqui se escapar da trava do perfil.
        builder.HasIndex(d => new { d.SuspensaoId, d.Etapa }).IsUnique().HasDatabaseName("ux_decisoes_da_retomada_etapa");
        builder.HasIndex(d => d.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_decisoes_da_retomada_ordem");
    }
}
