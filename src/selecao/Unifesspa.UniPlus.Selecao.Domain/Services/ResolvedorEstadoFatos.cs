namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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
/// A ordem de avaliação é a ordem de coleta, que o agregado já garante ser total e coerente com
/// as dependências: quando um fato é avaliado, todo fato que a sua pré-condição cita já está
/// resolvido. Não há segunda passada nem ponto fixo a calcular.
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
    public static IReadOnlyDictionary<string, FatoResolvido> Resolver(
        IReadOnlyCollection<FatoColetado> fatosColetados,
        IReadOnlyDictionary<string, JsonElement> respostasBrutas)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);
        ArgumentNullException.ThrowIfNull(respostasBrutas);

        Dictionary<string, FatoResolvido> resolvidos = new(StringComparer.Ordinal);

        foreach (FatoColetado fato in fatosColetados.OrderBy(static f => f.Ordem))
        {
            resolvidos[fato.FatoCodigo] = ResolverFato(fato, respostasBrutas, resolvidos);
        }

        return resolvidos;
    }

    private static FatoResolvido ResolverFato(
        FatoColetado fato,
        IReadOnlyDictionary<string, JsonElement> respostasBrutas,
        IReadOnlyDictionary<string, FatoResolvido> jaResolvidos)
    {
        if (fato.ParaPredicado() is { } precondicao)
        {
            switch (precondicao.Avaliar(jaResolvidos))
            {
                case Ternario.Falso:
                    // O campo não é apresentado. Estado resolvido e definitivo — e é aqui que uma
                    // resposta gravada antes, quando a pré-condição ainda era verdadeira, deixa de
                    // valer: ela nem chega a ser lida.
                    return FatoResolvido.NaoAplicavel();

                case Ternario.Indeterminado:
                    // Ainda não se sabe se o campo se aplica, porque algum fato de que ele depende
                    // não foi respondido. Propagar a indeterminação é o comportamento fail-closed:
                    // decidir por não-aplicável dispensaria o que talvez venha a ser exigido.
                    return FatoResolvido.Indeterminado();

                case Ternario.Verdadeiro:
                default:
                    break;
            }
        }

        // O campo se aplica: o estado passa a depender só de o candidato ter respondido ou não.
        if (!respostasBrutas.TryGetValue(fato.FatoCodigo, out JsonElement resposta)
            || resposta.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return FatoResolvido.Indeterminado();
        }

        return FatoResolvido.Resolvido(resposta);
    }
}
