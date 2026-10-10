namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// Configuração EF Core de <see cref="ComprovanteInscricao"/> (issue #1858) — <c>IForensicEntity</c>
/// append-only, mapeada direto (sem record de tradução): nenhuma propriedade carrega número de
/// documento, então não há cifra na fronteira a fazer aqui — diferente de
/// <see cref="InscricaoRecordConfiguration"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ComprovanteInscricaoConfiguration : IEntityTypeConfiguration<ComprovanteInscricao>
{
    public void Configure(EntityTypeBuilder<ComprovanteInscricao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("comprovantes_inscricao", t => t.HasComment(
            "Comprovante de uma submissão da inscrição (issue #1858) — append-only, sem nenhum " +
            "número de documento; uma correção soma uma linha nova, nunca muda a anterior."));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.InscricaoId).IsRequired();

        builder.Property(c => c.NumeroInscricao)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Instante).IsRequired();

        builder.Property(c => c.HashIntegridade)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(c => c.OfertaCursoId);

        builder.Property(c => c.Respostas)
            .HasConversion(RespostasConverter, RespostasComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.Derivados)
            .HasConversion(RespostasConverter, RespostasComparer)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasComment("O que o sistema derivou das respostas nesta submissão, como a UF a partir do endereço.");

        builder.Property(c => c.Grupos)
            .HasConversion(GruposConverter, GruposComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.Aceites)
            .HasConversion(AceitesConverter, AceitesComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        // Mesmo padrão de ObrigatoriedadeLegalHistoricoConfiguration: sem navigation property —
        // a forensic não navega de volta para a Inscricao em queries de histórico.
        builder.HasOne<InscricaoRecord>()
            .WithMany()
            .HasForeignKey(c => c.InscricaoId)
            .HasConstraintName("fk_comprovantes_inscricao_inscricoes")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.InscricaoId, c.NumeroInscricao })
            .HasDatabaseName("ix_comprovantes_inscricao_inscricao_numero");
    }

    private static readonly ValueConverter<IReadOnlyDictionary<string, JsonElement>, string> RespostasConverter =
        new(valor => JsonSerializer.Serialize(valor, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, (JsonSerializerOptions?)null)!);

    private static readonly ValueComparer<IReadOnlyDictionary<string, JsonElement>> RespostasComparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

    private static readonly ValueConverter<IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>, string> GruposConverter =
        new(valor => JsonSerializer.Serialize(valor, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>(json, (JsonSerializerOptions?)null)!);

    private static readonly ValueComparer<IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> GruposComparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

    private static readonly ValueConverter<IReadOnlyList<AceiteDeTermo>, string> AceitesConverter =
        new(valor => JsonSerializer.Serialize(valor, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<List<AceiteDeTermo>>(json, (JsonSerializerOptions?)null)!);

    private static readonly ValueComparer<IReadOnlyList<AceiteDeTermo>> AceitesComparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<List<AceiteDeTermo>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);
}
