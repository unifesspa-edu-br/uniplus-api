namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Configuração EF Core de <see cref="TermoExigidoFormulario"/> (UNI-REQ-0086), filha de
/// <c>ProcessoSeletivo</c> substituída por inteiro com ele. A exibição e a obrigatoriedade são
/// lidas e gravadas sempre inteiras, junto do termo, por isso ficam em <c>jsonb</c>.
/// </summary>
public sealed class TermoExigidoFormularioConfiguration : IEntityTypeConfiguration<TermoExigidoFormulario>
{
    private const int NomeMaxLength = 200;
    private const int TextoMaxLength = 20_000;
    private const int BaseLegalMaxLength = 500;
    private const int FormaAceiteMaxLength = 40;
    private const int HashMaxLength = 64;

    public void Configure(EntityTypeBuilder<TermoExigidoFormulario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("termos_exigidos_formulario");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Codigo).HasMaxLength(TermoExigidoFormulario.CodigoMaxLength).IsRequired();
        builder.Property(t => t.Ordem).IsRequired();
        builder.Property(t => t.Finalidade).HasConversion<int>().IsRequired();
        builder.Property(t => t.TermoId).IsRequired();
        builder.Property(t => t.VersaoId).IsRequired();
        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(t => t.Texto).HasMaxLength(TextoMaxLength).IsRequired();
        builder.Property(t => t.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();
        builder.Property(t => t.FormaAceite).HasMaxLength(FormaAceiteMaxLength).IsRequired();
        builder.Property(t => t.HashVersao).HasMaxLength(HashMaxLength).IsRequired();

        builder.Property(t => t.Exibicao)
            .HasConversion(PredicadoConverter, PredicadoComparer)
            .HasColumnType("jsonb");
        builder.Property(t => t.Obrigatoriedade)
            .HasConversion(ObrigatoriedadeConverter, ObrigatoriedadeComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Ignore(t => t.SemFormaDeAceite);
        builder.Ignore(t => t.Condicoes);
        builder.Ignore(t => t.FatosCitados);

        // Invariantes do agregado, garantidas também contra escrita concorrente.
        builder.HasIndex(t => new { t.ProcessoSeletivoId, t.Finalidade, t.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_termos_exigidos_formulario_processo_finalidade_codigo");
        builder.HasIndex(t => new { t.ProcessoSeletivoId, t.Finalidade, t.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_termos_exigidos_formulario_processo_finalidade_ordem");
    }

    // O EF não chama o conversor para nulo: a coluna nula é a exibição sempre.
    private static readonly ValueConverter<PredicadoDnf?, string?> PredicadoConverter =
        new(predicado => SerializarPredicado(predicado!), json => LerPredicado(json!));

    private static readonly ValueComparer<PredicadoDnf?> PredicadoComparer =
        new(
            (a, b) => (a == null ? null : SerializarPredicado(a)) == (b == null ? null : SerializarPredicado(b)),
            p => p == null ? 0 : SerializarPredicado(p).GetHashCode(StringComparison.Ordinal),
            p => p == null ? null : LerPredicado(SerializarPredicado(p)));

    private static readonly ValueConverter<Obrigatoriedade, string> ObrigatoriedadeConverter =
        new(obrigatoriedade => SerializarObrigatoriedade(obrigatoriedade), json => LerObrigatoriedade(json));

    private static readonly ValueComparer<Obrigatoriedade> ObrigatoriedadeComparer =
        new(
            (a, b) => SerializarObrigatoriedade(a!) == SerializarObrigatoriedade(b!),
            o => SerializarObrigatoriedade(o).GetHashCode(StringComparison.Ordinal),
            o => LerObrigatoriedade(SerializarObrigatoriedade(o)));

    private static string SerializarPredicado(PredicadoDnf predicado) => PredicadoDnfJson.ParaJson(predicado).ToJsonString();

    private static string SerializarObrigatoriedade(Obrigatoriedade obrigatoriedade) =>
        PredicadoDnfJson.ParaJson(obrigatoriedade).ToJsonString();

    private static PredicadoDnf LerPredicado(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return Exigir(PredicadoDnfJson.DeJson(documento.RootElement));
    }

    private static Obrigatoriedade LerObrigatoriedade(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return Exigir(PredicadoDnfJson.ObrigatoriedadeDeJson(documento.RootElement));
    }

    private static T Exigir<T>(Kernel.Results.Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"Condição gravada de termo exigido inválida: {resultado.Error!.Message}");
}
