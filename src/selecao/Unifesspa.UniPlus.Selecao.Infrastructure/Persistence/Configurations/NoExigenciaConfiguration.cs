namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
public sealed class NoExigenciaConfiguration : IEntityTypeConfiguration<NoExigencia>
{
    private const int ConsequenciaMaxLength = 30;

    public void Configure(EntityTypeBuilder<NoExigencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("nos_da_arvore_de_exigencias", ConfigurarRestricoes);
        ConfigurarCampos(builder);
        ConfigurarRelacionamentos(builder);
    }

    private static void ConfigurarRestricoes(TableBuilder<NoExigencia> t)
    {
        // Story #921: quantidade_minima passa a ser NOT NULL também em folha (cardinalidade
        // de apresentações) — só grupo E permanece sem cardinalidade própria. chave_distincao/
        // data_referencia/ocorrencias_esperadas são exclusivas de folha (Story #921).
        t.HasCheckConstraint(
            "ck_nos_da_arvore_de_exigencias_tipo_campos_coerentes",
            "(tipo = 1 AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NOT NULL AND consequencia IS NULL) OR " +
            "(tipo = 2 AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL " +
            "AND chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR " +
            "(tipo = 3 AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL " +
            "AND chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL)");
        t.HasCheckConstraint("ck_nos_da_arvore_de_exigencias_id_diferente_de_no_pai_id", "id <> no_pai_id");
        t.HasCheckConstraint("ck_nos_da_arvore_de_exigencias_ordem_nao_negativa", "ordem >= 0");
        t.HasCheckConstraint(
            "ck_nos_da_arvore_de_exigencias_quantidade_minima_positiva", "quantidade_minima IS NULL OR quantidade_minima >= 1");
        // Defesa em profundidade de NoExigencia.CriarFolha — coerência chave×data×ocorrências
        // (replica em SQL a mesma tabela de decisão de ValidarSemChave/ValidarChaveDeCalendario/
        // ValidarChaveDeOcorrencia). CompetenciaMensal=1, ExercicioAnual=2, Ocorrencia=3.
        // `chave_distincao IS NOT NULL AND` explícito em cada ramo NOT NULL: sem isso, o
        // Postgres avalia `NULL IN (1,2)`/`NULL = 3` como UNKNOWN (não FALSE), e um CHECK só
        // rejeita quando a expressão inteira é FALSE — UNKNOWN passa, deixando
        // chave_distincao NULL com data_referencia preenchida escapar da constraint.
        t.HasCheckConstraint(
            "ck_nos_da_arvore_de_exigencias_chave_distincao_coerente",
            "(chave_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR " +
            "(chave_distincao IS NOT NULL AND chave_distincao IN (1, 2) AND data_referencia IS NOT NULL AND ocorrencias_esperadas IS NULL) OR " +
            "(chave_distincao IS NOT NULL AND chave_distincao = 3 AND data_referencia IS NULL AND (ocorrencias_esperadas IS NULL OR jsonb_array_length(ocorrencias_esperadas) > 0))");
    }

    private static void ConfigurarCampos(EntityTypeBuilder<NoExigencia> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Ordem).IsRequired();
        builder.Property(n => n.Tipo).HasConversion<int>().IsRequired();
        builder.Property(n => n.Consequencia).HasMaxLength(ConsequenciaMaxLength);

        builder.OwnsOne(n => n.Distincao, ConfigurarDistincao);
        builder.Property(n => n.RepetePorEntidade)
            .HasColumnName("repete_por_entidade")
            .HasMaxLength(FormaDoGrupo.CodigoMaxLength);
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<NoExigencia> builder)
    {
        builder.HasOne<NoExigencia>()
            .WithMany(n => n.Filhos)
            .HasForeignKey(n => n.NoPaiId)
            .HasConstraintName("fk_nos_da_arvore_de_exigencias_no_pai")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(n => n.Filhos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        ConfigurarDocumento(builder);
        ConfigurarBasesLegais(builder);
    }

    private static void ConfigurarDocumento(EntityTypeBuilder<NoExigencia> builder)
    {
        builder.HasOne(n => n.DocumentoExigido)
            .WithMany()
            .HasForeignKey(n => n.DocumentoExigidoId)
            .HasConstraintName("fk_nos_da_arvore_de_exigencias_documento_exigido")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.DocumentoExigidoId)
            .IsUnique()
            .HasFilter("documento_exigido_id IS NOT NULL")
            .HasDatabaseName("ux_nos_da_arvore_de_exigencias_documento_exigido_id");
    }

    private static void ConfigurarBasesLegais(EntityTypeBuilder<NoExigencia> builder)
    {
        builder.HasMany(n => n.BasesLegais)
            .WithOne()
            .HasForeignKey(b => b.NoExigenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(n => n.BasesLegais)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarDistincao(OwnedNavigationBuilder<NoExigencia, DistincaoDasApresentacoes> builder)
    {
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.Property(d => d.Chave).HasColumnName("chave_distincao").HasConversion<int?>();
        builder.Property(d => d.DataReferencia).HasColumnName("data_referencia");
        builder.Property(d => d.OcorrenciasEsperadas).HasColumnName("ocorrencias_esperadas")
            .HasConversion(OcorrenciasEsperadasConverter, OcorrenciasEsperadasComparer).HasColumnType("jsonb");
    }

    private static readonly ValueConverter<IReadOnlyList<string>?, string?> OcorrenciasEsperadasConverter =
        new(
            lista => SerializeOcorrenciasEsperadas(lista),
            json => DeserializeOcorrenciasEsperadas(json));

    private static readonly ValueComparer<IReadOnlyList<string>?> OcorrenciasEsperadasComparer =
        new(
            (a, b) => SerializeOcorrenciasEsperadas(a) == SerializeOcorrenciasEsperadas(b),
            v => v == null ? 0 : SerializeOcorrenciasEsperadas(v)!.GetHashCode(StringComparison.Ordinal),
            v => DeserializeOcorrenciasEsperadas(SerializeOcorrenciasEsperadas(v)));

    private static string? SerializeOcorrenciasEsperadas(IReadOnlyList<string>? lista) =>
        lista is null ? null : JsonSerializer.Serialize(lista);
    private static List<string>? DeserializeOcorrenciasEsperadas(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<string>>(json);
}
