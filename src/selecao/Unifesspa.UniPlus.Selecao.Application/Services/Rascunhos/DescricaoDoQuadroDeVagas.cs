namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// Escreve, em cada distribuição de vagas do documento, o quadro por modalidade e o total de vagas do edital, calculados dos
/// cadastros vivos pela mesma regra da configuração. A distribuição que não pode ser calculada fica sem os dois, e só se escreve o
/// que o esquema marca como só de leitura.
/// </summary>
internal static class DescricaoDoQuadroDeVagas
{
    private const string Lista = "distribuicaoVagas";
    private const string Quadro = "quadro";
    private const string Total = "totalDeVagasDoEdital";

    internal static void Descrever(JsonObject documento, VagasDoRascunhoComCadastrosVivos vagas, FormaDoNo forma)
    {
        if (documento[Lista] is not JsonArray itens || FormaDoItemComCampoSomenteLeitura(forma) is not { } formaDoItem)
        {
            return;
        }

        foreach ((int posicao, ConfiguracaoDistribuicaoVagas distribuicao) in vagas.DistribuicoesCalculadas())
        {
            if (ItemNaPosicao(itens, posicao) is { } item)
            {
                Reescrever(item, formaDoItem, distribuicao);
            }
        }
    }

    private static JsonObject? ItemNaPosicao(JsonArray itens, int posicao) =>
        posicao < itens.Count ? itens[posicao] as JsonObject : null;

    private static void Reescrever(JsonObject item, FormaDoNo forma, ConfiguracaoDistribuicaoVagas distribuicao)
    {
        Reescrever(item, forma, Quadro, () => LinhasDoQuadro(distribuicao.Quadro));
        Reescrever(item, forma, Total, () => JsonValue.Create(distribuicao.TotalDeVagasDoEdital));
    }

    private static FormaDoNo? FormaDoItemComCampoSomenteLeitura(FormaDoNo forma) =>
        forma.Propriedades.GetValueOrDefault(Lista)?.Itens is { } item && (item.DeclaraSomenteLeitura(Quadro) || item.DeclaraSomenteLeitura(Total))
            ? item
            : null;

    private static void Reescrever(JsonObject item, FormaDoNo forma, string nome, Func<JsonNode> valor)
    {
        if (forma.DeclaraSomenteLeitura(nome))
        {
            item.Remove(nome);
            item.Add(nome, valor());
        }
    }

    private static JsonArray LinhasDoQuadro(QuadroDeVagas quadro) =>
        new([.. quadro.VagasPeloCodigoDaModalidade().Select(static linha => (JsonNode)new JsonObject
        {
            ["modalidadeCodigo"] = linha.ModalidadeCodigo,
            ["quantidade"] = linha.Quantidade,
        })]);
}
