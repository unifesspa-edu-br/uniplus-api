namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="ModalidadeSelecionada"/> —
/// entidade filha de <see cref="ConfiguracaoDistribuicaoVagas"/>, snapshot-copy
/// por valor da <c>Modalidade</c> viva do módulo Configuração.
/// </summary>
internal sealed class ModalidadeSelecionadaConfiguration : IEntityTypeConfiguration<ModalidadeSelecionada>
{
    private const int CodigoMaxLength = 60;
    private const int DescricaoMaxLength = 300;
    private const int TokenMaxLength = 30;
    private const int BaseLegalMaxLength = 500;

    public void Configure(EntityTypeBuilder<ModalidadeSelecionada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("modalidades_selecionadas", t =>
        {
            t.HasCheckConstraint("ck_modalidades_selecionadas_natureza_legal", $"natureza_legal IN ({SqlDosCodigosDeContrato.Lista<NaturezaLegalModalidade>()})");
            t.HasCheckConstraint("ck_modalidades_selecionadas_composicao_vagas", $"composicao_vagas IN ({SqlDosCodigosDeContrato.Lista<ComposicaoVagasModalidade>()})");
            t.HasCheckConstraint("ck_modalidades_selecionadas_regra_remanejamento", $"regra_remanejamento IN ({SqlDosCodigosDeContrato.Lista<RegraRemanejamentoModalidade>()})");
            t.HasComment("Modalidades escolhidas para a distribuição de vagas, com dados copiados do cadastro.");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasComment("Identificador da modalidade selecionada.").ValueGeneratedNever();

        builder.Property(m => m.Codigo).HasComment("Código da modalidade copiado do cadastro.").HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(m => m.Descricao).HasComment("Descrição da modalidade copiada do cadastro, quando disponível.").HasMaxLength(DescricaoMaxLength);
        builder.Property(m => m.NaturezaLegal).HasComment("Natureza legal da modalidade.").HasMaxLength(TokenMaxLength).IsRequired();
        builder.Property(m => m.ComposicaoVagas).HasComment("Composição das vagas da modalidade.").HasMaxLength(TokenMaxLength).IsRequired();
        builder.Property(m => m.ComposicaoOrigemCodigo).HasComment("Código da modalidade de origem da composição, quando aplicável.").HasMaxLength(CodigoMaxLength);
        builder.Property(m => m.ConfiguracaoDistribuicaoVagasId).HasComment("Identificador da configuração de distribuição à qual pertence.");
        builder.Property(m => m.ModalidadeOrigemId).HasComment("Identificador da modalidade no cadastro de origem.");
        builder.Property(m => m.QuantidadeDeclarada).HasComment("Quantidade de vagas declarada, quando informada.");
        builder.Property(m => m.RegraRemanejamento).HasComment("Regra de remanejamento da modalidade.").HasColumnName("regra_remanejamento").HasMaxLength(TokenMaxLength);
        builder.Property(m => m.RemanejamentoDestino).HasComment("Código da modalidade de destino do remanejamento, quando definido.").HasMaxLength(CodigoMaxLength);
        builder.Property(m => m.RemanejamentoPar).HasComment("Código da modalidade pareada para remanejamento, quando definido.").HasMaxLength(CodigoMaxLength);

        builder.HasIndex(m => m.ConfiguracaoDistribuicaoVagasId)
            .HasDatabaseName("ix_modalidades_selecionadas_configuracao_de_vagas");
        builder.Property(m => m.RemanejamentoFallback).HasComment("Código da modalidade de destino final do remanejamento, quando definido.").HasColumnName("remanejamento_destino_final").HasMaxLength(CodigoMaxLength);

        // A regra é a cópia por valor da regra do cadastro, lida e gravada sempre inteira com a
        // modalidade, na mesma forma jsonb das regras do formulário.
        builder.Property(m => m.RegraDeAplicabilidade).HasComment("Condições de aplicabilidade da modalidade, copiadas do cadastro em JSON.")
            .HasConversion(ConversoresDeRegras.PredicadoObrigatorio, ConversoresDeRegras.ComparadorDePredicadoObrigatorio)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.AcaoQuandoIndeferido).HasComment("Ação prevista quando o pedido é indeferido, quando definida.").HasMaxLength(TokenMaxLength);
        builder.Property(m => m.BaseLegal).HasComment("Fundamento legal da modalidade copiado do cadastro.").HasMaxLength(BaseLegalMaxLength).IsRequired();
    }
}
