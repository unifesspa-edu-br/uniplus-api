namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Domain.Entities;
using Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="CondicaoGatilho"/> (Story #554, PR #896) — entidade
/// filha de <see cref="DocumentoExigido"/>, <c>EntityBase</c> puro (sem soft-delete).
/// </summary>
public sealed class CondicaoGatilhoConfiguration : IEntityTypeConfiguration<CondicaoGatilho>
{
    private const int FatoMaxLength = 60;

    public void Configure(EntityTypeBuilder<CondicaoGatilho> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("condicoes_gatilho");
        builder.HasKey(c => c.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // DocumentoExigido/FaseCronograma.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Clausula).IsRequired();
        builder.Property(c => c.Fato).HasMaxLength(FatoMaxLength).IsRequired();

        // Mesmo mapeamento canônico de OperadorCodigo usado no wire — nunca enum.ToString()
        // cru (ADR-0111): a coluna carrega o mesmo token IGUAL/EM/MAIOR_IGUAL/MENOR_IGUAL.
        builder.Property(c => c.Operador)
            .HasConversion(OperadorConverter)
            .HasMaxLength(OperadorCodigoMaxLength)
            .IsRequired();

        // Mesmo padrão jsonb de RegraCatalogoConfiguration (EsquemaArgs/Invariantes) —
        // serializa pelo texto bruto do JsonElement, reidrata desanexado do JsonDocument
        // de origem.
        builder.Property(c => c.Valor)
            .HasConversion(JsonElementConverter, JsonElementComparer)
            .HasColumnType("jsonb")
            .IsRequired();
    }

    private const int OperadorCodigoMaxLength = 20;

    private static readonly ValueConverter<Operador, string> OperadorConverter =
        new(operador => operador.ToCodigo(), codigo => OperadorCodigo.FromCodigo(codigo));

    private static readonly ValueConverter<JsonElement, string> JsonElementConverter =
        new(element => element.GetRawText(), json => Parse(json));

    private static readonly ValueComparer<JsonElement> JsonElementComparer =
        new(
            (a, b) => a.GetRawText() == b.GetRawText(),
            v => v.GetRawText().GetHashCode(StringComparison.Ordinal),
            v => Parse(v.GetRawText()));

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
