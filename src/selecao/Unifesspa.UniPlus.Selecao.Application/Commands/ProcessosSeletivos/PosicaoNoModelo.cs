namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Globalization;
using System.Text.RegularExpressions;

using Kernel.Results;

/// <summary>
/// Leva a recusa de volta à parte do modelo. O resolvedor indexa a lista que recebeu, sem o que saiu da
/// cópia, e o índice dele volta à posição da parte no modelo.
/// </summary>
internal static partial class PosicaoNoModelo
{
    /// <summary>Prefixa o campo da recusa com <c>modelo</c>.</summary>
    public static FieldError NoModelo(FieldError erro) =>
        erro with { Field = string.IsNullOrEmpty(erro.Field) ? "modelo" : $"modelo.{erro.Field}" };

    public static FieldError NoModelo(FieldError erro, string lista, List<int> indices) =>
        NoModelo(NaPosicaoDoModelo(erro, lista, indices));

    public static FieldError NaPosicaoDoModelo(FieldError erro, string lista, List<int> indices)
    {
        string campo = erro.Field ?? string.Empty;
        if (IndiceDaLista().Match(campo) is not { Success: true } posicao
            || !string.Equals(posicao.Groups["lista"].Value, lista, StringComparison.Ordinal))
        {
            return erro;
        }

        int indice = int.Parse(posicao.Groups["indice"].Value, CultureInfo.InvariantCulture);
        return erro with { Field = $"{lista}[{indices[indice]}]{campo[posicao.Length..]}" };
    }

    [GeneratedRegex(@"^(?<lista>[a-z]+)\[(?<indice>\d+)\]")]
    private static partial Regex IndiceDaLista();
}
