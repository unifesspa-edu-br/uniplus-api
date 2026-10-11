namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ModalidadeSelecionada"/> (Story #773) —
/// entidade filha de <see cref="ConfiguracaoDistribuicaoVagas"/>, snapshot-copy
/// por valor da <c>Modalidade</c> viva do módulo Configuração.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ModalidadeSelecionadaConfiguration : IEntityTypeConfiguration<ModalidadeSelecionada>
{
    private const int CodigoMaxLength = 60;
    private const int DescricaoMaxLength = 300;
    private const int TokenMaxLength = 30;
    private const int BaseLegalMaxLength = 500;

    public void Configure(EntityTypeBuilder<ModalidadeSelecionada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("modalidades_selecionadas");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(m => m.Descricao).HasMaxLength(DescricaoMaxLength);
        builder.Property(m => m.NaturezaLegal).HasConversion<string>().HasMaxLength(TokenMaxLength).IsRequired();
        builder.Property(m => m.ComposicaoVagas).HasConversion<string>().HasMaxLength(TokenMaxLength).IsRequired();
        builder.Property(m => m.ComposicaoOrigemCodigo).HasMaxLength(CodigoMaxLength);
        builder.Property(m => m.RegraRemanejamento).HasConversion<string>().HasMaxLength(TokenMaxLength).IsRequired();
        builder.Property(m => m.RemanejamentoDestino).HasMaxLength(CodigoMaxLength);
        builder.Property(m => m.RemanejamentoPar).HasMaxLength(CodigoMaxLength);
        builder.Property(m => m.RemanejamentoFallback).HasMaxLength(CodigoMaxLength);

        // A regra é a cópia por valor da regra do cadastro, lida e gravada sempre inteira com a
        // modalidade, na mesma forma jsonb das regras do formulário.
        builder.Property(m => m.RegraDeAplicabilidade)
            .HasConversion(ConversoresDeRegras.PredicadoObrigatorio, ConversoresDeRegras.ComparadorDePredicadoObrigatorio)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.AcaoQuandoIndeferido).HasMaxLength(TokenMaxLength);
        builder.Property(m => m.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();
    }
}
