namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Globalization;
using System.Text.RegularExpressions;

using Kernel.Results;

/// <summary>
/// Uma regra de comprovação copiada do modelo, com a posição dela no conteúdo do modelo: o documento avulso
/// ou o grupo de documentos, e os documentos do grupo, na ordem dos itens da regra.
/// </summary>
/// <param name="Regra">A regra entregue ao processo.</param>
/// <param name="Caminho">O caminho do documento avulso ou do grupo de documentos no conteúdo do modelo.</param>
/// <param name="CaminhosDosItens">O caminho de cada documento do grupo; vazio na regra de documento avulso.</param>
internal sealed partial record RegraCopiadaDoModelo(RegraDeComprovacaoInformada Regra, string Caminho, IReadOnlyList<string> CaminhosDosItens)
{
    /// <summary>
    /// Leva a recusa do processo de volta ao conteúdo do modelo: o processo a aponta pela posição da regra
    /// na lista que recebeu e pela do item dentro do grupo, e o modelo, pela do documento ou do grupo. A
    /// recusa sem essa posição segue como veio, e a de uma posição fora das copiadas perde o campo: o modelo não a tem.
    /// </summary>
    public static FieldError NoModelo(FieldError erro, IReadOnlyList<RegraCopiadaDoModelo> copiadas)
    {
        ArgumentNullException.ThrowIfNull(copiadas);
        Match posicao = PosicaoNaLista().Match(erro.Field ?? string.Empty);
        if (!posicao.Success)
        {
            return erro;
        }

        int regra = int.Parse(posicao.Groups["regra"].Value, CultureInfo.InvariantCulture);
        return erro with { Field = regra < copiadas.Count ? copiadas[regra].CampoNoModelo(posicao) : null };
    }

    // O documento do modelo não declara o formulário: a recusa dele aponta o próprio documento.
    private string CampoNoModelo(Match posicao)
    {
        string resto = posicao.Groups["resto"].Value.Replace(".documento.", ".", StringComparison.Ordinal);
        if (resto == "." + MomentoDaExigenciaInput.CaminhoDoFormulario)
        {
            resto = string.Empty;
        }

        return posicao.Groups["item"].Success
            ? DoItem(int.Parse(posicao.Groups["item"].Value, CultureInfo.InvariantCulture)) + resto
            : DaRegra(resto);
    }

    private string DoItem(int item) => item < CaminhosDosItens.Count ? CaminhosDosItens[item] : Caminho;

    // O grupo do modelo chama de quantidadeMinima e documentos o que o processo chama de cardinalidade.quantidadeMinima e itens.
    private string DaRegra(string resto) => Caminho + (CaminhosDosItens.Count == 0 ? resto : resto switch
    {
        ".cardinalidade.quantidadeMinima" => ".quantidadeMinima",
        ".itens" => ".documentos",
        _ => resto,
    });

    [GeneratedRegex(@"^regrasDeComprovacao\[(?<regra>\d+)\](?:\.itens\[(?<item>\d+)\])?(?<resto>.*)$")]
    private static partial Regex PosicaoNaLista();
}
