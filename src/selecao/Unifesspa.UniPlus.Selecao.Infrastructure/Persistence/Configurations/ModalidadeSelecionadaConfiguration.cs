namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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

        // criterios_cumulativos: lista de strings serializada como jsonb —
        // mesmo padrão de ModalidadeConfiguration (Configuracao), consistente
        // com a origem do snapshot-copy.
        builder.Property(m => m.CriteriosCumulativos)
            .HasConversion(CriteriosConverter, CriteriosComparer)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();

        builder.Property(m => m.AcaoQuandoIndeferido).HasMaxLength(TokenMaxLength);
        builder.Property(m => m.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();
    }

    private static readonly ValueConverter<IReadOnlyList<string>, string> CriteriosConverter =
        new(
            criterios => JsonSerializer.Serialize(criterios, JsonOptions),
            json => (IReadOnlyList<string>)DeserializeCriterios(json));

    private static readonly ValueComparer<IReadOnlyList<string>> CriteriosComparer =
        new(
            (a, b) => SerializeCriterios(a) == SerializeCriterios(b),
            v => v == null ? 0 : SerializeCriterios(v).GetHashCode(StringComparison.Ordinal),
            v => (IReadOnlyList<string>)DeserializeCriterios(SerializeCriterios(v)));

    private static string SerializeCriterios(IReadOnlyList<string>? v) =>
        v is null ? "[]" : JsonSerializer.Serialize(v, JsonOptions);

    private static List<string> DeserializeCriterios(string json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
