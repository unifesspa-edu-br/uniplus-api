namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Text.Json;

/// <summary>
/// Os dois pontos de entrada para que um documento lido por máquina (conteúdo gravado, coluna JSON, mensagem) escreva e
/// leia todo enum com código pelo código declarado. A precedência do conversor sobre o conversor de enum por nome vive só
/// aqui.
/// </summary>
public static class OpcoesJsonDeDocumento
{
    /// <summary>
    /// Opções novas, congeladas, com as escolhas do modelo (nomes, nulos, <c>Encoder</c>, conversores e demais) e o
    /// conversor de códigos de contrato à frente. O modelo recebido não muda: não fica congelado nem ganha o conversor.
    /// </summary>
    /// <exception cref="ArgumentNullException">O modelo é nulo.</exception>
    public static JsonSerializerOptions Criar(JsonSerializerOptions modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        JsonSerializerOptions opcoes = new(modelo);
        opcoes.AdicionarConversorDeCodigosDeContrato();
        opcoes.MakeReadOnly(populateMissingResolver: true);

        return opcoes;
    }

    /// <summary>
    /// Para as opções que já vêm prontas, como as do Wolverine: deixa uma só instância do conversor de códigos de
    /// contrato na coleção, na posição 0. O serializador usa o primeiro conversor que aceita o tipo, e por isso o
    /// conversor fica à frente de qualquer conversor de enum por nome, como <c>JsonStringEnumConverter</c>. Chamar de
    /// novo mantém uma instância só.
    /// </summary>
    /// <exception cref="ArgumentNullException">As opções são nulas.</exception>
    /// <exception cref="InvalidOperationException">As opções já foram usadas pelo serializador e não aceitam mudança.</exception>
    public static void AdicionarConversorDeCodigosDeContrato(this JsonSerializerOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        foreach (CodigoDeContratoJsonConverterFactory existente in opcoes.Converters.OfType<CodigoDeContratoJsonConverterFactory>().ToList())
        {
            opcoes.Converters.Remove(existente);
        }

        opcoes.Converters.Insert(0, new CodigoDeContratoJsonConverterFactory());
    }
}
