namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core da providência sobre concessão. Pessoal, porque fundamento e motivo são texto livre sobre o acesso
/// de uma pessoa e há nome de quem agiu. O banco confere o que o agregado já garante: o estado gravado não admite
/// "prejudicada", o desfecho é gravado inteiro ou não é, e há no máximo uma providência aberta por concessão.
/// </summary>
public sealed class ProvidenciaConfiguration : IEntityTypeConfiguration<Providencia>
{
    /// <summary>O índice único da providência aberta, que a gravação concorrente traduz em "já há aberta".</summary>
    public const string AbertaUnicaPorConcessao = "ux_providencias_concessao_aberta";

    private const int IdentidadeMaxLength = 255;
    private const int CodigoMaxLength = 20;

    private static readonly ValueConverter<TipoDaProvidencia, string> TipoConverter = new(
        tipo => tipo.ToCodigo(),
        codigo => TipoDaProvidenciaCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<EstadoDaProvidencia, string> EstadoConverter = new(
        estado => estado.ToCodigo(),
        codigo => EstadoDaProvidenciaCodigo.FromCodigo(codigo));

    public void Configure(EntityTypeBuilder<Providencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Pessoal);

        builder.ToTable("providencias", RestricoesDaTabela);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        ConfigurarSolicitacao(builder);
        ConfigurarDesfecho(builder);

        builder.Ignore(p => p.SolicitadaPor);
        builder.Ignore(p => p.DesfechoPor);

        // A concessão é do mesmo módulo e nunca é excluída; a chave estrangeira mantém a providência apontando para ela.
        builder.HasOne<Concessao>()
            .WithMany()
            .HasForeignKey(p => p.ConcessaoId)
            .OnDelete(DeleteBehavior.Restrict);

        ConfigurarIndices(builder);

        // Concorrência otimista pela coluna de sistema xmin (ADR-0119): duas respostas da mesma providência não se misturam.
        builder.Property<uint>("Version").IsRowVersion();
    }

    private static void RestricoesDaTabela(TableBuilder<Providencia> tabela)
    {
        tabela.HasCheckConstraint("ck_providencias_tipo", "tipo IN ('retirada', 'revisao')");
        tabela.HasCheckConstraint("ck_providencias_estado", "estado IN ('aberta', 'respondida', 'atendida')");
        tabela.HasCheckConstraint(
            "ck_providencias_fundamento",
            $"char_length(fundamento) BETWEEN {SolicitacaoDaProvidencia.FundamentoMinLength} AND {SolicitacaoDaProvidencia.FundamentoMaxLength}");
        tabela.HasCheckConstraint(
            "ck_providencias_desfecho_completo",
            "(estado = 'aberta' AND desfecho_em IS NULL AND desfecho_por_emissor IS NULL AND desfecho_por_sub IS NULL"
            + " AND desfecho_por_nome IS NULL AND motivo_do_desfecho IS NULL)"
            + " OR (estado <> 'aberta' AND desfecho_em IS NOT NULL AND desfecho_por_emissor IS NOT NULL"
            + " AND desfecho_por_sub IS NOT NULL AND motivo_do_desfecho IS NOT NULL)");
    }

    private static void ConfigurarSolicitacao(EntityTypeBuilder<Providencia> builder)
    {
        builder.Property(p => p.Tipo).HasConversion(TipoConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(p => p.Fundamento).HasMaxLength(SolicitacaoDaProvidencia.FundamentoMaxLength).IsRequired();
        builder.Property(p => p.Estado).HasConversion(EstadoConverter).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(p => p.AbertaEm).IsRequired();
        builder.Property(p => p.SolicitadaPorEmissor).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(p => p.SolicitadaPorSub).HasMaxLength(IdentidadeMaxLength).IsRequired();
        builder.Property(p => p.SolicitadaPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(p => p.SolicitadaPorEmail).HasMaxLength(PedidoDeConcessao.EmailMaxLength);
    }

    private static void ConfigurarDesfecho(EntityTypeBuilder<Providencia> builder)
    {
        builder.Property(p => p.DesfechoPorEmissor).HasMaxLength(IdentidadeMaxLength);
        builder.Property(p => p.DesfechoPorSub).HasMaxLength(IdentidadeMaxLength);
        builder.Property(p => p.DesfechoPorNome).HasMaxLength(NomeDeExibicao.MaxLength);
        builder.Property(p => p.MotivoDoDesfecho).HasMaxLength(Concessao.MotivoMaxLength);
    }

    private static void ConfigurarIndices(EntityTypeBuilder<Providencia> builder)
    {
        // No máximo uma providência aberta por concessão: a guarda final da abertura concorrente.
        builder.HasIndex(p => p.ConcessaoId, AbertaUnicaPorConcessao)
            .IsUnique()
            .HasFilter("estado = 'aberta'")
            .HasDatabaseName(AbertaUnicaPorConcessao);

        // A trilha da concessão e o filtro por concessão.
        builder.HasIndex(p => p.ConcessaoId, "ix_providencias_concessao").HasDatabaseName("ix_providencias_concessao");

        // A fila por estado, em ordem de abertura: o seek do cursor compara (aberta_em, id) dentro do estado.
        builder.HasIndex(p => new { p.Estado, p.AbertaEm, p.Id }, "ix_providencias_estado_aberta_em_id").HasDatabaseName("ix_providencias_estado_aberta_em_id");
    }
}
