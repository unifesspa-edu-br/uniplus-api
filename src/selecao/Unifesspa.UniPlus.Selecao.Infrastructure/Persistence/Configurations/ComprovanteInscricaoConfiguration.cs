namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// Configuração EF Core de <see cref="ComprovanteInscricao"/> (issue #1858) — <c>IForensicEntity</c>
/// append-only, mapeada direto (sem record de tradução): nenhuma propriedade carrega número de
/// documento, e as respostas de cifra individual vão cifradas em <c>SensiveisCifrados</c> pelo
/// repositório — o EF não cifra, porque a cifra é assíncrona.
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

        builder.Property(c => c.OfertaCursoSegundaOpcaoId)
            .HasComment("A oferta de curso da 2ª opção; nulo quando a inscrição não a declarou nesta submissão.");

        builder.Property(c => c.Respostas)
            .HasConversion(RespostasConverter, RespostasComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        // Em claro só em memória: o repositório as grava cifradas em SensiveisCifrados.
        builder.Ignore(c => c.RespostasSensiveis);

        builder.Property(c => c.SensiveisCifrados)
            .HasColumnType("bytea")
            .HasComment("Respostas de dado pessoal sensível com cifra individual (identidade de gênero, orientação sexual), cifradas (AES-GCM, ADR-0121, chave dos sensíveis) — nunca texto claro.");

        builder.Property(c => c.SensiveisHmac)
            .HasColumnType("bytea")
            .HasComment("HMAC do id do comprovante e das respostas sensíveis, que prende o envelope cifrado a este comprovante.");

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

        builder.Property(c => c.Modalidades)
            .HasConversion(ModalidadesConverter, ModalidadesComparer)
            .HasColumnType("jsonb")
            .HasComment("As modalidades em que o candidato concorre nesta submissão, com o nome congelado e a ordem das ofertadas; nulo quando a versão não deriva a modalidade.");

        builder.Property(c => c.ModalidadesSegundaOpcao)
            .HasConversion(ModalidadesConverter, ModalidadesComparer)
            .HasColumnType("jsonb")
            .HasComment("As modalidades em que o candidato concorre na 2ª opção, sobre a distribuição de vagas da oferta dela; nulo quando não há 2ª opção ou a versão não deriva a modalidade.");

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

    private static readonly ValueConverter<IReadOnlyList<ModalidadeDeConcorrencia>?, string?> ModalidadesConverter =
        new(valor => valor == null ? null : JsonSerializer.Serialize(valor, (JsonSerializerOptions?)null),
            json => json == null ? null : JsonSerializer.Deserialize<List<ModalidadeDeConcorrencia>>(json, (JsonSerializerOptions?)null));

    private static readonly ValueComparer<IReadOnlyList<ModalidadeDeConcorrencia>?> ModalidadesComparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            v => v == null ? null : JsonSerializer.Deserialize<List<ModalidadeDeConcorrencia>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null));

    private static readonly ValueConverter<IReadOnlyList<AceiteDeTermo>, string> AceitesConverter =
        new(valor => JsonSerializer.Serialize(valor, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<List<AceiteDeTermo>>(json, (JsonSerializerOptions?)null)!);

    private static readonly ValueComparer<IReadOnlyList<AceiteDeTermo>> AceitesComparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            v => JsonSerializer.Deserialize<List<AceiteDeTermo>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);
}
