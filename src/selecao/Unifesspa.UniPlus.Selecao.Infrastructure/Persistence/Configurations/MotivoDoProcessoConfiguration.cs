namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core dos motivos escolhidos pelo processo (UNI-REQ-0285): cópia por valor, sem chave estrangeira
/// para o cadastro (ADR-0061), para que a linha viva possa mudar sem alcançar o processo, e sem chave estrangeira para a
/// fase ou a etapa do lugar, porque a escolha acompanha a identidade delas entre as versões do edital.
/// </summary>
internal sealed class MotivoDoProcessoConfiguration : IEntityTypeConfiguration<MotivoDoProcesso>
{
    public void Configure(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("motivos_do_processo", MapearRestricoes);
        builder.HasKey(m => m.Id);

        MapearCopia(builder);
        MapearEscolha(builder);
        MapearLugar(builder);
        MapearVigencia(builder);

        // O índice único da escolha atual de cada motivo em cada lugar não se expressa no modelo (índice parcial com
        // NULLS NOT DISTINCT): vive no script do módulo ux_motivos_do_processo_vigente.
        builder.HasIndex(m => m.ProcessoSeletivoId).HasDatabaseName("ix_motivos_do_processo_processo");
    }

    private static void MapearRestricoes(TableBuilder<MotivoDoProcesso> tabela)
    {
        tabela.HasCheckConstraint("ck_motivos_do_processo_lugar", "num_nonnulls(fase_canonica_origem_id, etapa_processo_id) = 1");
        tabela.HasCheckConstraint(
            "ck_motivos_do_processo_vigencia",
            "versao_do_edital_de_entrada >= 1 AND (versao_do_edital_de_saida IS NULL OR versao_do_edital_de_saida > versao_do_edital_de_entrada)");
    }

    private static void MapearLugar(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        builder.OwnsOne(m => m.Lugar, lugar =>
        {
            lugar.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            lugar.Property(l => l.FaseCanonicaOrigemId).HasColumnName("fase_canonica_origem_id");
            lugar.Property(l => l.EtapaProcessoId).HasColumnName("etapa_processo_id");
        });
        builder.Navigation(m => m.Lugar).IsRequired();
    }

    private static void MapearVigencia(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        builder.OwnsOne(m => m.Vigencia, vigencia =>
        {
            vigencia.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            vigencia.Property(v => v.VersaoDoEditalDeEntrada).HasColumnName("versao_do_edital_de_entrada").IsRequired();
            vigencia.Property(v => v.VersaoDoEditalDeSaida).HasColumnName("versao_do_edital_de_saida");
        });
        builder.Navigation(m => m.Vigencia).IsRequired();
    }

    private static void MapearCopia(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        builder.Property(m => m.Tipo).HasMaxLength(MotivoDeDecisaoConfiguration.EnumMaxLength).IsRequired();
        builder.Property(m => m.Codigo)
            .HasConversion(codigo => codigo.Valor, valor => CodigoMotivoDecisao.Reidratar(valor))
            .HasMaxLength(MotivoDeDecisaoConfiguration.CodigoMaxLength)
            .IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(ValidacaoDoMotivo.DescricaoMaxLength).IsRequired();
        builder.Property(m => m.Subclassificacao).HasMaxLength(MotivoDeDecisaoConfiguration.SubclassificacaoMaxLength);
        builder.Property(m => m.Sentido).HasConversion<string>().HasMaxLength(MotivoDeDecisaoConfiguration.EnumMaxLength).IsRequired();
    }

    private static void MapearEscolha(EntityTypeBuilder<MotivoDoProcesso> builder)
    {
        builder.Property(m => m.ProcessoSeletivoId).IsRequired();
        builder.Property(m => m.MotivoDeDecisaoOrigemId).IsRequired();

        // Justificativa interna da inclusão depois da publicação (retificação): não vai ao candidato.
        builder.Property(m => m.JustificativaDaInclusao).HasMaxLength(ConferenciaDosMotivosDaRetificacao.JustificativaMaxLength);
        builder.Property<uint>("Version").IsRowVersion();
        builder.Property(m => m.CreatedBy).HasMaxLength(MotivoDeDecisaoConfiguration.AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(MotivoDeDecisaoConfiguration.AuditUserMaxLength);
    }
}
