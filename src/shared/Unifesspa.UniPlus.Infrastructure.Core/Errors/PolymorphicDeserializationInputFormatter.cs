namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;

/// <summary>
/// Fecha o único buraco por onde uma recusa de leitura do corpo ainda saía como 5xx: a união
/// polimórfica cujo documento não declara o discriminador de tipo.
/// </summary>
/// <remarks>
/// <para>
/// O <see cref="SystemTextJsonInputFormatter"/> converte <see cref="JsonException"/> em erro de
/// model state — e é por isso que discriminador <b>desconhecido</b> e corpo não-objeto já
/// respondiam 400. Mas discriminador <b>ausente</b> é a única recusa que o desserializador
/// reporta como <see cref="NotSupportedException"/>, e essa escapa do formatter, chega ao
/// tratador global como falha inesperada e vira 500. São dois erros de forma equivalentes do
/// cliente, no mesmo campo, com status incoerentes; nenhum endpoint declara 500 nesse contexto.
/// </para>
/// <para>
/// <b>O recorte é pelo contrato, não pelo texto da exceção.</b> A recusa só é convertida em erro
/// de model state quando o caminho do erro endereça, no contrato do modelo, um tipo que de fato
/// é união polimórfica — e é isso que confirma que quem errou foi o documento. O mesmo
/// <see cref="NotSupportedException"/> sai de um tipo abstrato que ninguém declarou como união:
/// aí o defeito é de configuração do servidor, a exceção segue em frente e o 5xx continua
/// acusando o que só o servidor pode corrigir. Ler a frase do framework para decidir isso seria
/// apostar justamente na parte que menos se controla.
/// </para>
/// <para>
/// <b>A mensagem da exceção não entra no <c>ModelState</c>.</b> É dela que sairia o nome completo
/// do tipo CLR que falhou a desserialização, e <see cref="InvalidRequestProblemFactory"/> existe
/// justamente para que isso nunca chegue ao cliente. O que entra é a chave — o caminho do erro
/// dentro do documento —, que é o sinal de que houve documento e quem o reprovou foi o
/// desserializador.
/// </para>
/// <para>
/// O campo e o discriminador esperado são anotados à parte, em
/// <see cref="PolymorphicDeserializationFailures"/>, para a resposta poder dizer o que corrigir
/// em vez de só "o corpo não pôde ser lido". O discriminador é lido do <b>contrato do modelo</b>,
/// não de texto: o caminho localiza o tipo dentro do <see cref="JsonTypeInfo"/> da ação e o nome
/// vem de <see cref="JsonPolymorphismOptions.TypeDiscriminatorPropertyName"/>. Assim vale para
/// união na raiz, aninhada e dentro de coleção — e continua valendo se alguma união do projeto
/// deixar de usar <c>$tipo</c>.
/// </para>
/// </remarks>
internal sealed class PolymorphicDeserializationInputFormatter :
    IInputFormatter,
    IInputFormatterExceptionPolicy,
    IApiRequestFormatMetadataProvider
{
    /// <summary>
    /// O caminho do erro dentro do documento, no sufixo que o desserializador acrescenta a toda
    /// exceção que emite.
    /// </summary>
    /// <remarks>
    /// Esse sufixo é interpolado em código pelo próprio desserializador, e não vem de recurso
    /// traduzível — é a única parte da mensagem que não muda com a cultura. E é a única que se
    /// lê: o caminho é composto dos nomes das propriedades do <b>contrato</b>, enquanto o nome
    /// do tipo CLR fica no trecho que este recorte deixa de fora.
    /// </remarks>
    private static readonly Regex CaminhoNoSufixo = new(
        @" Path: (?<caminho>\$[^|]*) \| LineNumber: ",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private readonly SystemTextJsonInputFormatter _inner;

    public PolymorphicDeserializationInputFormatter(SystemTextJsonInputFormatter inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <summary>
    /// A mesma política do formatter envolvido: o body model binder continua tratando só o que
    /// ele já tratava, e o que este tipo acrescenta é resolvido aqui dentro.
    /// </summary>
    public InputFormatterExceptionPolicy ExceptionPolicy =>
        ((IInputFormatterExceptionPolicy)_inner).ExceptionPolicy;

    public bool CanRead(InputFormatterContext context) => _inner.CanRead(context);

    /// <summary>
    /// Delega os content types consumidos. Sem isto o ApiExplorer perderia a resposta do
    /// formatter envolvido, e os <c>requestBody</c> do OpenAPI sairiam sem media type.
    /// </summary>
    public IReadOnlyList<string>? GetSupportedContentTypes(string? contentType, Type objectType) =>
        _inner.GetSupportedContentTypes(contentType, objectType);

    public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            return await _inner.ReadAsync(context).ConfigureAwait(false);
        }
        catch (NotSupportedException excecao)
        {
            string caminho = Caminho(excecao);

            if (Discriminador(_inner.SerializerOptions, context.ModelType, caminho) is not { } discriminador)
            {
                // O contrato não declara união naquele ponto, então não foi o documento que
                // errou: é o modelo do servidor que o desserializador não sabe construir.
                // Rebaixar isso a 400 tiraria do alerta o único defeito aqui que o cliente não
                // tem como corrigir.
                throw;
            }

            // Mensagem vazia de propósito: a chave já diz tudo o que a resposta precisa saber, e
            // é o factory que escreve o texto que o cliente lê.
            context.ModelState.TryAddModelError(caminho, string.Empty);
            PolymorphicDeserializationFailures.Record(context.HttpContext, Campo(caminho), discriminador);

            return InputFormatterResult.Failure();
        }
    }

    /// <summary>
    /// O caminho que o desserializador reporta, ou a raiz quando o sufixo não está no formato
    /// esperado — caso em que a recusa ainda sai 400, só sem nomear o campo.
    /// </summary>
    private static string Caminho(NotSupportedException excecao)
    {
        Match encontrado = CaminhoNoSufixo.Match(excecao.Message);

        return encontrado.Success && encontrado.Groups["caminho"].Value is { Length: <= 512 } caminho
            ? caminho
            : "$";
    }

    /// <summary>O caminho sem a raiz — vazio quando é o corpo inteiro que é a união.</summary>
    private static string Campo(string caminho) =>
        caminho.Length > 1 && caminho[1] == '.' ? caminho[2..] : caminho[1..];

    /// <summary>
    /// O nome da propriedade discriminadora do tipo que <paramref name="caminho"/> endereça, ou
    /// <see langword="null"/> quando o caminho não é navegável no contrato — um dicionário, por
    /// exemplo. Na dúvida não afirma, e a resposta cai na recusa genérica de corpo ilegível.
    /// </summary>
    private static string? Discriminador(JsonSerializerOptions opcoes, Type tipoDoModelo, string caminho)
    {
        JsonTypeInfo? info = TipoDe(opcoes, tipoDoModelo);

        foreach (string segmento in Segmentos(caminho))
        {
            if (info is null)
            {
                return null;
            }

            info = segmento.Length == 0
                ? TipoDe(opcoes, info.ElementType)
                : TipoDe(opcoes, Propriedade(info, segmento, opcoes.PropertyNameCaseInsensitive)?.PropertyType);
        }

        return info?.PolymorphismOptions?.TypeDiscriminatorPropertyName;
    }

    private static JsonPropertyInfo? Propriedade(JsonTypeInfo info, string nome, bool ignorandoCaixa) =>
        info.Properties.FirstOrDefault(propriedade => string.Equals(
            propriedade.Name,
            nome,
            ignorandoCaixa ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    private static JsonTypeInfo? TipoDe(JsonSerializerOptions opcoes, Type? tipo)
    {
        if (tipo is null)
        {
            return null;
        }

        try
        {
            return opcoes.GetTypeInfo(Nullable.GetUnderlyingType(tipo) ?? tipo);
        }
        catch (Exception excecao) when (excecao is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Os passos do caminho depois da raiz: nome de propriedade, ou vazio para o elemento de uma
    /// coleção. Caminho que não casa com essa forma interrompe a navegação.
    /// </summary>
    private static IEnumerable<string> Segmentos(string caminho)
    {
        int posicao = 1;

        while (posicao < caminho.Length)
        {
            char atual = caminho[posicao];

            if (atual == '.')
            {
                int fim = caminho.IndexOfAny(['.', '['], posicao + 1);
                fim = fim < 0 ? caminho.Length : fim;
                yield return caminho[(posicao + 1)..fim];
                posicao = fim;
                continue;
            }

            if (atual != '[')
            {
                yield break;
            }

            int fechamento = caminho.IndexOf(']', posicao);
            if (fechamento < 0)
            {
                yield break;
            }

            // `$['nome']` é como o desserializador escreve a propriedade cujo nome não cabe na
            // forma com ponto; `$[0]` é o elemento de uma coleção.
            string conteudo = caminho[(posicao + 1)..fechamento];
            yield return conteudo.Length > 1 && conteudo[0] == '\'' && conteudo[^1] == '\''
                ? conteudo[1..^1]
                : string.Empty;

            posicao = fechamento + 1;
        }
    }
}
