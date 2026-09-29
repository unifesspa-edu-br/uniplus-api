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
/// A avaliação é a do avaliador de formulário compartilhado (ADR-0135): a coleta vira uma etapa
/// única, com os fatos na ordem de coleta, a pré-condição como exibição e a obrigatoriedade do
/// campo. Nenhuma etapa é dada como concluída, então um campo aplicável sem resposta fica pendente.
/// </para>
/// </remarks>
public static class ResolvedorEstadoFatos
{
    /// <summary>Código da etapa única em que a coleta é descrita para o avaliador.</summary>
    private const string EtapaDaColeta = "COLETA";

    /// <summary>
    /// Resolve todos os fatos coletados pelo processo.
    /// </summary>
    /// <param name="fatosColetados">O grafo de coleta — fatos, ordem e pré-condições.</param>
    /// <param name="respostasBrutas">
    /// O que o candidato respondeu, por código de fato. Uma chave que não corresponde a fato
    /// coletado é <b>ignorada</b>: o grafo é a autoridade sobre o que existe, e uma resposta órfã
    /// não deve criar um fato que a configuração não prevê.
    /// </param>
    public static IReadOnlyDictionary<string, FatoResolvido> Resolver(
        IReadOnlyCollection<FatoColetado> fatosColetados,
        IReadOnlyDictionary<string, JsonElement> respostasBrutas)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);
        ArgumentNullException.ThrowIfNull(respostasBrutas);

        DefinicaoEtapa coleta = new(
            EtapaDaColeta,
            exibicao: null,
            [.. fatosColetados.OrderBy(static f => f.Ordem).Select(static fato => new DefinicaoItem(
                fato.FatoCodigo,
                fato.ParaPredicado(),
                fato.Obrigatorio ? Obrigatoriedade.Sempre : Obrigatoriedade.Nunca,
                restricoes: []))]);

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            new DefinicaoFormulario([coleta], termos: [], derivacoes: []),
            new EntradaAvaliacaoFormulario(
                respostasBrutas,
                EtapasConcluidas: new HashSet<string>(StringComparer.Ordinal),
                FatosConhecidos: new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)));

        return avaliacao.Fatos;
    }
}
