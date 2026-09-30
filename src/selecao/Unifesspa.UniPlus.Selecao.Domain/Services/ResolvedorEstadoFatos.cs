namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Resolve o estado de cada fato do candidato a partir do grafo de coleta congelado e das
/// respostas que ele efetivamente deu (Story #926). É a ponte entre a configuração — quais campos
/// existem e sob que pré-condição — e a álgebra que avalia gatilhos.
/// </summary>
/// <remarks>
/// <para>
/// Função pura: a mesma entrada produz sempre a mesma saída, sem tocar em banco nem em relógio.
/// Isso é o que torna a <b>invalidação de resposta obsoleta</b> uma propriedade da resolução em
/// vez de uma operação de escrita — quando a pré-condição de um campo passa a ser falsa, o fato
/// resolve não-aplicável e a resposta que estava lá simplesmente não entra no resultado, sem
/// precisar apagá-la de lugar nenhum.
/// </para>
/// <para>
/// A avaliação é a do avaliador de formulário compartilhado (ADR-0135): cada formulário é uma
/// etapa, na ordem das finalidades, com os fatos na ordem de coleta, a pré-condição como exibição
/// e a obrigatoriedade do campo. Nenhuma etapa é dada como concluída, então um campo aplicável sem resposta fica pendente.
/// </para>
/// </remarks>
public static class ResolvedorEstadoFatos
{
    /// <summary>
    /// Resolve todos os fatos coletados pelo processo.
    /// </summary>
    /// <param name="fatosColetados">O grafo de coleta — fatos, ordem e pré-condições.</param>
    /// <param name="respostasBrutas">
    /// O que o candidato respondeu, por código de fato. Uma chave que não corresponde a fato
    /// coletado é <b>ignorada</b>: o grafo é a autoridade sobre o que existe, e uma resposta órfã
    /// não deve criar um fato que a configuração não prevê.
    /// </param>
    /// <param name="valoresOfertados">
    /// Os valores que o processo oferta a cada fato categórico coletado, congelados no edital. Uma
    /// resposta com valor fora da oferta <b>não vale</b>: o fato fica sem resposta e nunca satisfaz
    /// uma condição, mesmo que a condição cite aquele valor — por exemplo, uma opção retirada numa
    /// retificação ou um município que deixou a área do bônus.
    /// </param>
    public static IReadOnlyDictionary<string, FatoResolvido> Resolver(
        IReadOnlyCollection<FatoColetado> fatosColetados,
        IReadOnlyDictionary<string, JsonElement> respostasBrutas,
        IReadOnlyDictionary<string, IReadOnlySet<string>> valoresOfertados)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);
        ArgumentNullException.ThrowIfNull(respostasBrutas);
        ArgumentNullException.ThrowIfNull(valoresOfertados);

        // Uma etapa por formulário, na ordem das finalidades: a ordem dos itens é única dentro de
        // cada formulário, e não entre eles.
        DefinicaoEtapa[] etapas = [.. fatosColetados
            .GroupBy(static f => f.Finalidade)
            .OrderBy(static g => g.Key)
            .Select(static g => new DefinicaoEtapa(
                g.Key.ToString(),
                exibicao: null,
                [.. g.OrderBy(static f => f.Ordem).Select(static fato => new DefinicaoItem(
                    fato.FatoCodigo,
                    fato.ParaPredicado(),
                    fato.Obrigatoriedade,
                    restricoes: []))]))];

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            new DefinicaoFormulario(etapas, termos: [], derivacoes: []),
            new EntradaAvaliacaoFormulario(
                RespostasDentroDaOferta(respostasBrutas, valoresOfertados),
                EtapasConcluidas: new HashSet<string>(StringComparer.Ordinal),
                FatosConhecidos: new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)));

        return avaliacao.Fatos;
    }

    private static Dictionary<string, JsonElement> RespostasDentroDaOferta(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlySet<string>> valoresOfertados) =>
        respostas
            .Where(resposta => !valoresOfertados.TryGetValue(resposta.Key, out IReadOnlySet<string>? oferta)
                || RespostaDeCampo.EstaVazia(resposta.Value)
                || RespostaDeCampo.Codigos(resposta.Value) is { } codigos && codigos.All(oferta.Contains))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);
}
