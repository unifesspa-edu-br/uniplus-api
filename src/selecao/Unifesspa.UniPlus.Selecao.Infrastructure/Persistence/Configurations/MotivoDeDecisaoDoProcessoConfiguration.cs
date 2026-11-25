namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core dos motivos escolhidos pelo processo (UNI-REQ-0285): cópia por valor, sem chave estrangeira
/// para o cadastro (ADR-0061), para que a linha viva possa mudar sem alcançar o processo, e sem chave estrangeira para a
/// fase ou a etapa do lugar, porque a escolha acompanha a identidade delas entre as versões do edital.
/// </summary>
internal sealed class MotivoDeDecisaoDoProcessoConfiguration : IEntityTypeConfiguration<MotivoDeDecisaoDoProcesso>
{
    public void Configure(EntityTypeBuilder<MotivoDeDecisaoDoProcesso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("motivos_de_decisao_do_processo", MapearRestricoes);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasComment("Identificador da escolha do motivo no processo.");

        MapearCopia(builder);
        MapearEscolha(builder);
        MapearLugar(builder);
        MapearVigencia(builder);

        // O índice único da escolha atual de cada motivo em cada lugar não se expressa no modelo (índice parcial com
        // NULLS NOT DISTINCT): vive no script do módulo ux_motivos_de_decisao_do_processo_vigente.
        builder.HasIndex(m => m.ProcessoSeletivoId).HasDatabaseName("ix_motivos_de_decisao_do_processo_processo");
    }

    private static void MapearRestricoes(TableBuilder<MotivoDeDecisaoDoProcesso> tabela)
    {
        tabela.HasComment("Motivos escolhidos para decisões do processo seletivo, com os dados do cadastro no momento da escolha e sua vigência no edital.");
        tabela.HasCheckConstraint("ck_motivos_de_decisao_do_processo_lugar", "num_nonnulls(tipo_fase_origem_id, etapa_processo_id) = 1");
        tabela.HasCheckConstraint(
            "ck_motivos_de_decisao_do_processo_vigencia",
            "versao_do_edital_de_entrada >= 1 AND (versao_do_edital_de_saida IS NULL OR versao_do_edital_de_saida > versao_do_edital_de_entrada)");
    }

    private static void MapearLugar(EntityTypeBuilder<MotivoDeDecisaoDoProcesso> builder)
    {
        builder.OwnsOne(m => m.Lugar, lugar =>
        {
            lugar.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            lugar.Property(l => l.FaseCanonicaOrigemId).HasColumnName("tipo_fase_origem_id").HasComment("Identificador da fase à qual a escolha se aplica, quando a decisão é de uma fase.");
            lugar.Property(l => l.EtapaProcessoId).HasColumnName("etapa_processo_id").HasComment("Identificador da etapa à qual a escolha se aplica, quando a decisão é de uma etapa.");
        });
        builder.Navigation(m => m.Lugar).IsRequired();
    }

    private static void MapearVigencia(EntityTypeBuilder<MotivoDeDecisaoDoProcesso> builder)
    {
        builder.OwnsOne(m => m.Vigencia, vigencia =>
        {
            vigencia.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            vigencia.Property(v => v.VersaoDoEditalDeEntrada).HasColumnName("versao_do_edital_de_entrada").IsRequired().HasComment("Número da primeira versão do edital em que a escolha vale.");
            vigencia.Property(v => v.VersaoDoEditalDeSaida).HasColumnName("versao_do_edital_de_saida").HasComment("Número da versão do edital a partir da qual a escolha deixa de valer; vazio enquanto vigente.");
        });
        builder.Navigation(m => m.Vigencia).IsRequired();
    }

    private static void MapearCopia(EntityTypeBuilder<MotivoDeDecisaoDoProcesso> builder)
    {
        builder.Property(m => m.Tipo).HasMaxLength(MotivoDeDecisaoConfiguration.EnumMaxLength).IsRequired().HasComment("Tipo de decisão.");
        builder.Property(m => m.Codigo)
            .HasConversion(codigo => codigo.Valor, valor => CodigoMotivoDecisao.Reidratar(valor))
            .HasComment("Código do motivo copiado do cadastro na escolha.")
            .HasMaxLength(MotivoDeDecisaoConfiguration.CodigoMaxLength)
            .IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(ValidacaoDoMotivo.DescricaoMaxLength).IsRequired().HasComment("Descrição do motivo copiada do cadastro na escolha.");
        builder.Property(m => m.Subclassificacao).HasMaxLength(MotivoDeDecisaoConfiguration.SubclassificacaoMaxLength).HasComment("Subclassificação do motivo copiada do cadastro, quando houver.");
        builder.Property(m => m.Sentido).HasMaxLength(12).IsRequired().HasComment("Sentido da decisão associado ao motivo.");
    }

    private static void MapearEscolha(EntityTypeBuilder<MotivoDeDecisaoDoProcesso> builder)
    {
        builder.Property(m => m.ProcessoSeletivoId).IsRequired().HasComment("Identificador do processo seletivo que escolheu o motivo.");
        builder.Property(m => m.MotivoDeDecisaoOrigemId).IsRequired().HasComment("Identificador do motivo no cadastro de origem.");

        // Justificativa interna da inclusão depois da publicação (retificação): não vai ao candidato.
        builder.Property(m => m.JustificativaDaInclusao).HasMaxLength(ConferenciaDosMotivosDaRetificacao.JustificativaMaxLength).HasComment("Justificativa da inclusão após a publicação, vazia para escolhas anteriores.");
        builder.Property<uint>("Version").IsRowVersion();
        builder.Property(m => m.CreatedBy).HasMaxLength(MotivoDeDecisaoConfiguration.AuditUserMaxLength);
        builder.Property(m => m.UpdatedBy).HasMaxLength(MotivoDeDecisaoConfiguration.AuditUserMaxLength);
    }
}
