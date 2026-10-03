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
/// A avaliação é a do avaliador de formulário compartilhado (ADR-0135): cada seção é uma etapa,
/// com a exibição da seção, na ordem das finalidades e das seções, e os fatos na ordem de coleta,
/// com a pré-condição como exibição, a obrigatoriedade e as restrições de valor do campo — a
/// resposta que viola uma restrição não vale. Nenhuma etapa é dada como concluída, então um campo aplicável sem resposta fica pendente.
/// </para>
/// </remarks>
public static class ResolvedorEstadoFatos
{
    /// <summary>
    /// Resolve todos os fatos coletados pelo processo.
    /// </summary>
    /// <param name="formularios">Os formulários do processo, com as seções e a exibição de cada uma.</param>
    /// <param name="fatosColetados">O grafo de coleta — fatos, ordem e pré-condições.</param>
    /// <param name="derivacoes">
    /// As derivações por regra do processo, avaliadas entre os campos: uma regra de campo pode citar
    /// o derivado cujas dependências são campos anteriores.
    /// </param>
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
        IReadOnlyCollection<FormularioProcesso> formularios,
        IReadOnlyCollection<FatoColetado> fatosColetados,
        IReadOnlyList<RegrasDerivacaoFato> derivacoes,
        IReadOnlyDictionary<string, JsonElement> respostasBrutas,
        IReadOnlyDictionary<string, IReadOnlySet<string>> valoresOfertados)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(fatosColetados);
        ArgumentNullException.ThrowIfNull(derivacoes);
        ArgumentNullException.ThrowIfNull(respostasBrutas);
        ArgumentNullException.ThrowIfNull(valoresOfertados);

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            DefinicaoDoProcesso.Montar(formularios, fatosColetados, grupos: [], termos: [], derivacoes, agregados: []),
            new EntradaAvaliacaoFormulario(
                RespostaDeCampo.DentroDaOferta(respostasBrutas, valoresOfertados),
                EtapasConcluidas: new HashSet<string>(StringComparer.Ordinal),
                FatosConhecidos: new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)));

        // Os derivados entram só na avaliação; o resultado é o estado dos fatos coletados.
        return fatosColetados.ToDictionary(static f => f.FatoCodigo, f => avaliacao.Fatos[f.FatoCodigo], StringComparer.Ordinal);
    }
}
