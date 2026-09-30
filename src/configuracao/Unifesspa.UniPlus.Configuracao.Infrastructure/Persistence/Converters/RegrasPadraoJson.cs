namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras padrão de um derivado numa coluna <c>jsonb</c>: cada regra com o que contribui (nulo
/// no booleano) e o predicado como lista de cláusulas, cada uma lista de condições
/// <c>{fato, operador, valor}</c>. A leitura remonta as regras pelas mesmas factories do domínio e
/// falha alto se o conteúdo gravado não as satisfaz.
/// </summary>
internal static class RegrasPadraoJson
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<IReadOnlyList<RegraDerivacao>, string> Converter =
        new(regras => Serializar(regras), json => Desserializar(json));

    public static readonly ValueComparer<IReadOnlyList<RegraDerivacao>> Comparer =
        new(
            (a, b) => Serializar(a!) == Serializar(b!),
            regras => Serializar(regras).GetHashCode(StringComparison.Ordinal),
            regras => Desserializar(Serializar(regras)));

    public static string Serializar(IReadOnlyList<RegraDerivacao> regras) =>
        JsonSerializer.Serialize(
            regras.Select(static r => new RegraGravada(
                r.Contribui,
                [.. r.Quando.Clausulas.Select(static c => (IReadOnlyList<CondicaoGravada>)
                    [.. c.Condicoes.Select(static d => new CondicaoGravada(d.Fato, d.Operador.ToCodigo(), d.Valor))])])),
            Opcoes);

    public static IReadOnlyList<RegraDerivacao> Desserializar(string json)
    {
        IReadOnlyList<RegraGravada> gravadas = JsonSerializer.Deserialize<IReadOnlyList<RegraGravada>>(json, Opcoes) ?? [];
        return [.. gravadas.Select(Remontar)];
    }

    private static RegraDerivacao Remontar(RegraGravada gravada)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        for (int i = 0; i < gravada.Quando.Count; i++)
        {
            linhas.AddRange(gravada.Quando[i].Select(c =>
                (i, Exigir(CondicaoDnf.Criar(c.Fato, OperadorCodigo.FromCodigo(c.Operador), c.Valor)))));
        }

        PredicadoDnf quando = Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas(linhas));
        return gravada.Contribui is null ? RegraDerivacao.CriarBooleana(quando) : Exigir(RegraDerivacao.Criar(quando, gravada.Contribui));
    }

    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"Regra padrão gravada inválida: {resultado.Error!.Message}");

    private sealed record RegraGravada(string? Contribui, IReadOnlyList<IReadOnlyList<CondicaoGravada>> Quando);

    private sealed record CondicaoGravada(string Fato, string Operador, JsonElement Valor);
}
