namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Text.Json;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Predicado, obrigatoriedade, restrições de valor e impedimento das regras do formulário gravados em
/// <c>jsonb</c>, na forma de <see cref="PredicadoDnfJson"/> e <see cref="RestricaoValorJson"/>: são
/// lidos e gravados sempre inteiros, junto do item ou do termo que os declara.
/// </summary>
internal static class ConversoresDeRegras
{
    // O EF não chama o conversor para nulo: a coluna nula é a ausência de condição.
    public static readonly ValueConverter<PredicadoDnf?, string?> Predicado =
        new(predicado => SerializarPredicado(predicado!), json => LerPredicado(json!));

    public static readonly ValueComparer<PredicadoDnf?> ComparadorDePredicado =
        new(
            (a, b) => (a == null ? null : SerializarPredicado(a)) == (b == null ? null : SerializarPredicado(b)),
            p => p == null ? 0 : SerializarPredicado(p).GetHashCode(StringComparison.Ordinal),
            p => p == null ? null : LerPredicado(SerializarPredicado(p)));

    public static readonly ValueConverter<Obrigatoriedade, string> Obrigatoriedade =
        new(obrigatoriedade => SerializarObrigatoriedade(obrigatoriedade), json => LerObrigatoriedade(json));

    public static readonly ValueComparer<Obrigatoriedade> ComparadorDeObrigatoriedade =
        new(
            (a, b) => SerializarObrigatoriedade(a!) == SerializarObrigatoriedade(b!),
            o => SerializarObrigatoriedade(o).GetHashCode(StringComparison.Ordinal),
            o => LerObrigatoriedade(SerializarObrigatoriedade(o)));

    public static readonly ValueConverter<IReadOnlyList<RestricaoValor>, string> Restricoes =
        new(restricoes => SerializarRestricoes(restricoes), json => LerRestricoes(json));

    public static readonly ValueComparer<IReadOnlyList<RestricaoValor>> ComparadorDeRestricoes =
        new(
            (a, b) => SerializarRestricoes(a!) == SerializarRestricoes(b!),
            r => SerializarRestricoes(r).GetHashCode(StringComparison.Ordinal),
            r => LerRestricoes(SerializarRestricoes(r)));

    // O EF não chama o conversor para nulo: a coluna nula é o item sem impedimento.
    public static readonly ValueConverter<Impedimento?, string?> Impedimento =
        new(impedimento => SerializarImpedimento(impedimento!), json => LerImpedimento(json!));

    public static readonly ValueComparer<Impedimento?> ComparadorDeImpedimento =
        new(
            (a, b) => (a == null ? null : SerializarImpedimento(a)) == (b == null ? null : SerializarImpedimento(b)),
            i => i == null ? 0 : SerializarImpedimento(i).GetHashCode(StringComparison.Ordinal),
            i => i == null ? null : LerImpedimento(SerializarImpedimento(i)));

    private static string SerializarImpedimento(Impedimento impedimento) => PredicadoDnfJson.ParaJson(impedimento).ToJsonString();

    private static Impedimento LerImpedimento(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return Exigir(PredicadoDnfJson.ImpedimentoDeJson(documento.RootElement))!;
    }

    private static string SerializarRestricoes(IReadOnlyList<RestricaoValor> restricoes) =>
        RestricaoValorJson.ParaJson(restricoes).ToJsonString();

    private static IReadOnlyList<RestricaoValor> LerRestricoes(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return Exigir(RestricaoValorJson.ListaDeJson(documento.RootElement));
    }

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

    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"Regra gravada do formulário inválida: {resultado.Error!.Message}");
}
