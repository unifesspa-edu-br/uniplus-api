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
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
public sealed class RegraDeComprovacaoConfiguration : IEntityTypeConfiguration<RegraDeComprovacao>
{
    private const int ConsequenciaMaxLength = 30;

    private static readonly JsonSerializerOptions OpcoesJson = OpcoesJsonDeDocumento.Criar(new());

    public void Configure(EntityTypeBuilder<RegraDeComprovacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.ToTable("regras_de_comprovacao", ConfigurarRestricoes);
        ConfigurarCampos(builder);
        ConfigurarRelacionamentos(builder);
    }

    private static void ConfigurarRestricoes(TableBuilder<RegraDeComprovacao> t)
    {
        t.HasCheckConstraint(
            "ck_regras_comprovacao_forma_coerente",
            "(forma = 'DOCUMENTO' AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NOT NULL AND consequencia IS NULL) OR " +
            "(forma = 'TODOS_OS_ITENS' AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL " +
            "AND criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR " +
            "(forma = 'QUANTIDADE_MINIMA_DE_ITENS' AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL " +
            "AND criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL)");
        t.HasCheckConstraint("ck_regras_de_comprovacao_id_diferente_de_regra_que_contem_id", "id <> regra_que_contem_id");
        t.HasCheckConstraint("ck_regras_de_comprovacao_ordem_nao_negativa", "ordem >= 0");
        t.HasCheckConstraint(
            "ck_regras_de_comprovacao_quantidade_minima_positiva", "quantidade_minima IS NULL OR quantidade_minima >= 1");
        // O teste explícito de nulo evita que UNKNOWN no PostgreSQL aceite uma combinação inválida.
        t.HasCheckConstraint(
            "ck_regras_de_comprovacao_criterio_de_distincao_coerente",
            "(criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR " +
            "(criterio_de_distincao IS NOT NULL AND criterio_de_distincao IN ('COMPETENCIA_MENSAL', 'EXERCICIO_ANUAL') AND data_referencia IS NOT NULL AND ocorrencias_esperadas IS NULL) OR " +
            "(criterio_de_distincao IS NOT NULL AND criterio_de_distincao = 'OCORRENCIA' AND data_referencia IS NULL AND (ocorrencias_esperadas IS NULL OR jsonb_array_length(ocorrencias_esperadas) > 0))");
    }

    private static void ConfigurarCampos(EntityTypeBuilder<RegraDeComprovacao> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Ordem).IsRequired();
        builder.Property(n => n.Forma).HasColumnName("forma").IsRequired();
        builder.Property(n => n.Consequencia).HasMaxLength(ConsequenciaMaxLength);

        builder.OwnsOne(n => n.Distincao, ConfigurarDistincao);
        builder.Property(n => n.GrupoDoFormularioParaRepeticao)
            .HasColumnName("grupo_para_repeticao")
            .HasMaxLength(FormaDoGrupo.CodigoMaxLength);
    }

    private static void ConfigurarRelacionamentos(EntityTypeBuilder<RegraDeComprovacao> builder)
    {
        builder.HasOne<RegraDeComprovacao>()
            .WithMany(n => n.Itens)
            .HasForeignKey(n => n.RegraQueContemId)
            .HasConstraintName("fk_regras_comprovacao_grupo")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(n => n.Itens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        ConfigurarDocumento(builder);
        ConfigurarBasesLegais(builder);
    }

    private static void ConfigurarDocumento(EntityTypeBuilder<RegraDeComprovacao> builder)
    {
        builder.HasOne(n => n.DocumentoExigido)
            .WithMany()
            .HasForeignKey(n => n.DocumentoExigidoId)
            .HasConstraintName("fk_regras_de_comprovacao_documento_exigido")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.DocumentoExigidoId)
            .IsUnique()
            .HasFilter("documento_exigido_id IS NOT NULL")
            .HasDatabaseName("ux_regras_de_comprovacao_documento_exigido_id");
    }

    private static void ConfigurarBasesLegais(EntityTypeBuilder<RegraDeComprovacao> builder)
    {
        builder.HasMany(n => n.BasesLegais)
            .WithOne()
            .HasForeignKey(b => b.RegraDeComprovacaoId)
            .HasConstraintName("fk_bases_legais_comprovacao_regra")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(n => n.BasesLegais)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarDistincao(OwnedNavigationBuilder<RegraDeComprovacao, DistincaoDasApresentacoes> builder)
    {
        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
        builder.Property(d => d.Criterio).HasColumnName("criterio_de_distincao").HasMaxLength(18);
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
        lista is null ? null : JsonSerializer.Serialize(lista, OpcoesJson);
    private static List<string>? DeserializeOcorrenciasEsperadas(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<string>>(json, OpcoesJson);
}
