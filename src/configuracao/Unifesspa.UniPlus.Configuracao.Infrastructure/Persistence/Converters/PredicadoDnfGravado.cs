namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma gravada de um predicado: lista de cláusulas, cada uma lista de condições
/// <c>{fato, operador, valor}</c>. A leitura remonta o predicado pelas factories do domínio e falha
/// alto se o conteúdo gravado não as satisfaz.
/// </summary>
internal static class PredicadoDnfGravado
{
    public static IReadOnlyList<IReadOnlyList<CondicaoGravada>> Gravar(PredicadoDnf predicado) =>
        [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoGravada>)
            [.. c.Condicoes.Select(static d => new CondicaoGravada(d.Fato, d.Operador.ToCodigo(), d.Valor))])];

    public static PredicadoDnf Remontar(IReadOnlyList<IReadOnlyList<CondicaoGravada>> clausulas)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        for (int i = 0; i < clausulas.Count; i++)
        {
            linhas.AddRange(clausulas[i].Select(c =>
                (i, Exigir(CondicaoDnf.Criar(c.Fato, OperadorCodigo.FromCodigo(c.Operador), c.Valor)))));
        }

        return Exigir(PredicadoDnf.CriarDeCondicoesAgrupadas(linhas));
    }

    internal static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException($"Predicado gravado inválido: {resultado.Error!.Message}");

    internal sealed record CondicaoGravada(string Fato, string Operador, JsonElement Valor);
}
