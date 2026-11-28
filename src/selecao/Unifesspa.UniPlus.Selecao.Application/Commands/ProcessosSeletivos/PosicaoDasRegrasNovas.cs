namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Globalization;
using System.Text.RegularExpressions;

using Kernel.Results;

/// <summary>
/// O processo confere as regras que manteve antes das novas, e a recusa aponta a posição na lista inteira. Quem
/// enviou o pedido conhece só a lista das novas: a posição volta a ser a dela, e a recusa de uma regra mantida
/// perde o campo, porque o pedido não a tem.
/// </summary>
internal static partial class PosicaoDasRegrasNovas
{
    public static FieldError SemAsMantidas(FieldError erro, int quantidadeMantidas)
    {
        Match posicao = PosicaoNaLista().Match(erro.Field ?? string.Empty);
        if (!posicao.Success)
        {
            return erro;
        }

        int regra = int.Parse(posicao.Groups["regra"].Value, CultureInfo.InvariantCulture) - quantidadeMantidas;
        return erro with { Field = regra < 0 ? null : $"regrasDeComprovacao[{regra}]{posicao.Groups["resto"].Value}" };
    }

    [GeneratedRegex(@"^regrasDeComprovacao\[(?<regra>\d+)\](?<resto>.*)$")]
    private static partial Regex PosicaoNaLista();
}
