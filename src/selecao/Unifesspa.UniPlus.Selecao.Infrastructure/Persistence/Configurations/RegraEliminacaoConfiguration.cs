namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Configuração EF Core de <see cref="RegraEliminacao"/> (Story #775) —
/// entidade filha de <see cref="ConfiguracaoClassificacao"/>, cardinalidade
/// múltipla.
/// </summary>
internal sealed class RegraEliminacaoConfiguration : IEntityTypeConfiguration<RegraEliminacao>
{

    public void Configure(EntityTypeBuilder<RegraEliminacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("regras_eliminacao");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.OwnsOne(r => r.Regra, regra => regra.ConfigurarReferenciaRegra("regra"));
        builder.Navigation(r => r.Regra).IsRequired();

        // Parâmetros polimórficos (união fechada, uma variante por regra de eliminação) — mesmo padrão de
        // CriterioDesempateConfiguration (Story #774): coluna "json" (NÃO
        // "jsonb"), porque o desserializador polimórfico do System.Text.Json
        // exige o discriminador "$tipo" como primeira propriedade, e o
        // "jsonb" do Postgres reordena chaves por tamanho na escrita.
        builder.Property(r => r.Parametros)
            .HasColumnName("parametros")
            .HasConversion(ParametrosConverter, ParametrosComparer)
            .HasColumnType("json")
            .IsRequired();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private static readonly ValueConverter<ParametrosRegraEliminacao, string> ParametrosConverter =
        new(
            parametros => JsonSerializer.Serialize(parametros, JsonOptions),
            json => JsonSerializer.Deserialize<ParametrosRegraEliminacao>(json, JsonOptions)!);

    private static readonly ValueComparer<ParametrosRegraEliminacao> ParametrosComparer =
        new(
            (a, b) => Serialize(a) == Serialize(b),
            v => v == null ? 0 : Serialize(v).GetHashCode(StringComparison.Ordinal),
            v => Deserialize(Serialize(v)));

    private static string Serialize(ParametrosRegraEliminacao? v) =>
        v is null ? string.Empty : JsonSerializer.Serialize(v, JsonOptions);

    private static ParametrosRegraEliminacao Deserialize(string json) =>
        JsonSerializer.Deserialize<ParametrosRegraEliminacao>(json, JsonOptions)!;
}
