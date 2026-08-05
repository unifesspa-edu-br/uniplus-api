namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="ConfiguracaoDivulgacao"/> (UNI-REQ-0050, issue #563) —
/// entidade 0..1 do agregado <see cref="ProcessoSeletivo"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ConfiguracaoDivulgacaoConfiguration : IEntityTypeConfiguration<ConfiguracaoDivulgacao>
{
    private const int JustificativaMaxLength = ConfiguracaoDivulgacao.JustificativaMaxLength;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Configure(EntityTypeBuilder<ConfiguracaoDivulgacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("configuracoes_divulgacao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // campos_publicos: lista de strings serializada como jsonb — mesmo molde de
        // ModalidadeSelecionadaConfiguration.CriteriosCumulativos. SEM DEFAULT: uma lista vazia
        // é estado que ConfiguracaoDivulgacao.Criar nunca produz (viola o piso e a não-vacuidade
        // ao mesmo tempo) — a tabela é nova, sem linha a preencher, e a coluna exige valor
        // explícito em vez de um default que fabricaria um estado que o domínio proíbe.
        builder.Property(c => c.CamposPublicos)
            .HasConversion(CamposPublicosConverter, CamposPublicosComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.Justificativa).HasMaxLength(JustificativaMaxLength);
    }

    private static readonly ValueConverter<IReadOnlyList<string>, string> CamposPublicosConverter =
        new(
            campos => JsonSerializer.Serialize(campos, JsonOptions),
            json => (IReadOnlyList<string>)DeserializeCampos(json));

    private static readonly ValueComparer<IReadOnlyList<string>> CamposPublicosComparer =
        new(
            (a, b) => SerializeCampos(a) == SerializeCampos(b),
            v => v == null ? 0 : SerializeCampos(v).GetHashCode(StringComparison.Ordinal),
            v => (IReadOnlyList<string>)DeserializeCampos(SerializeCampos(v)));

    private static string SerializeCampos(IReadOnlyList<string>? v) =>
        v is null ? "[]" : JsonSerializer.Serialize(v, JsonOptions);

    private static List<string> DeserializeCampos(string json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
