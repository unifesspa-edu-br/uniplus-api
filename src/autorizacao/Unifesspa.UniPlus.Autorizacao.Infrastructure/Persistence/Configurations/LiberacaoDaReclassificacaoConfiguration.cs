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

/// <summary>
/// Configuração EF Core das decisões das etapas da reclassificação, registro só acrescentado. O banco recusa a etapa
/// repetida na concessão e o motivo que não casa com o resultado.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class LiberacaoDaReclassificacaoConfiguration : IEntityTypeConfiguration<LiberacaoDaReclassificacao>
{
    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 40;

    private static readonly ValueConverter<EtapaDaVersao, string> EtapaConverter = new(
        etapa => etapa.ToCodigo(),
        codigo => EtapaDaVersaoCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<ResultadoDaLiberacao, string> ResultadoConverter = new(
        resultado => resultado.ToCodigo(),
        codigo => ResultadoDaLiberacaoCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<LiberacaoDaReclassificacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("liberacoes_da_reclassificacao", ConfigurarRestricoes);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Etapa).HasConversion(EtapaConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(l => l.Resultado).HasConversion(ResultadoConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(l => l.Motivo).HasMaxLength(MotivosDaVersao.MaxLength);
        builder.Property(l => l.DecisorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.DecisorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(l => l.DecisorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(l => l.DecisorTemConcessao).IsRequired();
        builder.Property(l => l.LoteId).IsRequired();
        builder.Property(l => l.Instante).IsRequired();
        builder.Property(l => l.OrdemNaTrilha).IsRequired();
        builder.Ignore(l => l.Decisor);
        builder.Ignore(l => l.OrdinalLocal);

        builder.HasOne<ReclassificacaoDaPermissao>().WithMany().HasForeignKey(l => l.ReclassificacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Concessao>()
            .WithMany()
            .HasForeignKey(l => new { l.PerfilId, l.ConcessaoId })
            .HasPrincipalKey(c => new { c.PerfilId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Uma decisão por etapa em cada concessão: a segunda, simultânea, é barrada aqui se escapar da trava do perfil.
        builder.HasIndex(l => new { l.ReclassificacaoId, l.ConcessaoId, l.Etapa }).IsUnique().HasDatabaseName("ux_liberacoes_da_reclassificacao_etapa");
        builder.HasIndex(l => l.OrdemNaTrilha).IsUnique().HasDatabaseName("ux_liberacoes_da_reclassificacao_ordem");
    }

    private static void ConfigurarRestricoes(TableBuilder<LiberacaoDaReclassificacao> tabela)
    {
        tabela.HasCheckConstraint(
            "ck_liberacoes_da_reclassificacao_etapa",
            $"etapa IN ('{EtapaDaVersaoCodigo.GestaoDeAcessos}', '{EtapaDaVersaoCodigo.ProtecaoDeDados}')");
        tabela.HasCheckConstraint(
            "ck_liberacoes_da_reclassificacao_motivo",
            $"(resultado = '{ResultadoDaLiberacaoCodigo.Mantida}') = (motivo IS NOT NULL) AND resultado IN ('{ResultadoDaLiberacaoCodigo.Liberada}', '{ResultadoDaLiberacaoCodigo.Mantida}')");
    }
}
