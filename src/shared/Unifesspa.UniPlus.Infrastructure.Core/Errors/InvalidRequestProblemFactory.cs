namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Globalization;
using System.Text.RegularExpressions;

using Kernel.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resposta canônica para a carga que não chega a ser lida — o corpo que o desserializador
/// recusa, e o campo obrigatório que ele não encontra.
/// </summary>
/// <remarks>
/// <para>
/// Sem isto, essas recusas saem no envelope padrão do framework, que difere do nosso em tudo
/// que a ADR-0023 fixa: sem <c>code</c>, sem <c>traceId</c>, <c>type</c> apontando para a RFC
/// do status em vez do catálogo, título em inglês. E, o mais grave, o corpo carrega o <b>nome
/// completo do tipo CLR</b> que falhou a desserialização — namespace, camada e nome do record
/// da Application entregues a qualquer cliente que mande uma carga incompleta.
/// </para>
/// <para>
/// <strong>A mensagem do framework nunca é repassada.</strong> É dela que o nome do tipo vem, e
/// repassá-la filtrando o que parece nome de tipo seria apostar no formato de um texto que não
/// controlamos. O que se aproveita são os <b>nomes dos campos</b> — a única informação ali que
/// serve ao cliente — extraídos da lista que a mensagem de propriedade obrigatória enumera.
/// </para>
/// <para>
/// <strong>O que este tipo NÃO responde, e por quê.</strong> Ele devolve <see langword="null"/>
/// para tudo que não seja leitura da requisição — e validação não é leitura —, deixando o
/// factory anterior da cadeia responder. É deliberado: uma falha de <c>[RegularExpression]</c> num parâmetro de rota
/// acontece DEPOIS de o binding ter dado certo, e a mensagem dela — escrita por nós, em pt-BR,
/// dizendo ao cliente qual é o formato esperado — é muito melhor do que qualquer coisa que se
/// possa dizer genericamente. Capturá-la aqui trocaria orientação útil por "o corpo não pôde
/// ser lido", que além de inútil seria falso.
/// </para>
/// <para>
/// <strong>Valor de rota ou query que não converte é respondido aqui</strong>, mas o sinal que
/// permite afirmá-lo não vem do <c>ModelState</c>: depois que ele normaliza a exceção do binder
/// numa mensagem segura, uma conversão que falhou e uma validação que reprovou ficam idênticas
/// — mesma chave, mesmo valor tentado, sem exceção guardada. O que as separa é <b>quando</b> o
/// erro entra, e isso é anotado no instante do binding por
/// <see cref="ValueConversionTrackingModelBinderProvider"/>. Nenhuma decisão aqui depende do
/// texto da mensagem do framework.
/// </para>
/// <para>
/// A recusa de conversão só é reivindicada quando <b>toda</b> chave reprovada está anotada.
/// Basta uma que não esteja para este tipo devolver <see langword="null"/> — essa uma reprovou
/// por validação, e a mensagem dela vale mais do que qualquer recusa genérica.
/// </para>
/// </remarks>
public static class InvalidRequestProblemFactory
{
    /// <summary>
    /// Nomes entre aspas simples que a mensagem de propriedade obrigatória enumera depois de
    /// <c>including:</c>. O recorte começa DEPOIS dessa palavra de propósito: antes dela vem o
    /// nome do tipo, também entre aspas simples, que é justamente o que não pode sair daqui.
    /// </summary>
    private static readonly Regex NamesAfterIncluding = new(
        @"including:(?<list>.*)$",
        RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex SingleQuoted = new(
        @"'(?<name>[^']{1,128})'",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Chave sintética que o desserializador usa para apontar posição no documento
    /// (<c>$</c>, <c>$[0]</c>, <c>$.campo</c>). Não é nome de campo do contrato, e a presença
    /// dela é o que identifica a falha como de LEITURA do corpo.
    /// </summary>
    private static readonly Regex SyntheticKey = new(
        @"^\$",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// A resposta canônica, ou <see langword="null"/> quando a falha não é de leitura — caso em
    /// que o caller delega ao factory anterior da cadeia.
    /// </summary>
    public static IActionResult? TryBuild(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HashSet<string> bodyKeys = BodyKeys(context);
        HashSet<string> binderKeys = BinderKeys(context);

        // A ordem das três perguntas importa, e é a do afunilamento. Quando o corpo veio e não
        // desserializa, o MVC reprova TAMBÉM o parâmetro que o receberia — então perguntar
        // "faltou corpo?" antes de "o corpo é ilegível?" responde "faltou corpo" para um
        // documento que chegou. O sinal de que houve documento é a entrada que aponta posição
        // dentro dele; ela desempata.
        bool unreadable = HasDeserializationFailure(context.ModelState);
        IReadOnlyList<string> missing = MissingFields(context.ModelState, binderKeys);
        bool bodyAbsent = !unreadable
            && BodyProvablyAbsent(context.HttpContext.Request)
            && HasAbsentBody(context.ModelState, bodyKeys);

        IReadOnlyList<string> naoConvertidos = NaoConvertidos(context, binderKeys);

        if (!unreadable && !bodyAbsent && missing.Count == 0 && naoConvertidos.Count == 0)
        {
            return null;
        }

        IDomainErrorMapper mapper = context.HttpContext.RequestServices.GetRequiredService<IDomainErrorMapper>();

        // Nomear os campos é o que mais serve a quem chamou, então vem primeiro quando há o que
        // nomear. Corpo ausente fica por último porque é o caso em que não há documento onde
        // procurar campo nenhum.
        (string code, string detail) =
            missing.Count > 0 ? (InvalidRequestErrorCodes.MissingRequiredField, $"A requisição não declara {Enumerate(missing)}.")
            : unreadable ? (InvalidRequestErrorCodes.Malformed, "A requisição não pôde ser lida: o corpo não está no formato que o contrato declara.")
            : naoConvertidos.Count > 0 ? (InvalidRequestErrorCodes.InvalidValue, $"A requisição traz {EnumerateParameters(naoConvertidos)} com valor que não corresponde ao tipo declarado.")
            : (InvalidRequestErrorCodes.MissingBody, "A requisição não traz corpo, e este recurso exige um.");

        return Result.Failure(new DomainError(code, detail)).ToActionResult(mapper);
    }

    /// <summary>
    /// Os parâmetros de rota ou query cujo valor não virou o tipo declarado — e só eles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A anotação vem do binder, no instante da falha, e não de leitura do texto da mensagem: o
    /// que separa conversão de validação é QUANDO o erro entra no <c>ModelState</c>, e isso não
    /// sobrevive à normalização que o dicionário faz.
    /// </para>
    /// <para>
    /// <b>Exige que toda chave reprovada esteja anotada.</b> Basta uma que não esteja para este
    /// factory devolver nada e deixar a cadeia responder — porque essa uma reprovou por
    /// validação, e a mensagem dela diz ao cliente qual é o formato esperado. Trocar orientação
    /// específica por recusa genérica é regressão, e é o defeito que já apareceu ao fechar a
    /// recusa de leitura do corpo. Na dúvida, quem responde é quem tem mais a dizer.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> NaoConvertidos(ActionContext context, HashSet<string> binderKeys)
    {
        IReadOnlySet<string> anotadas = ValueConversionFailures.Of(context.HttpContext);
        if (anotadas.Count == 0)
        {
            return [];
        }

        SortedSet<string> nomes = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ModelStateEntry> entry in context.ModelState)
        {
            if (entry.Value.ValidationState != ModelValidationState.Invalid)
            {
                continue;
            }

            if (!anotadas.Contains(entry.Key) || !binderKeys.Contains(entry.Key))
            {
                return [];
            }

            nomes.Add(entry.Key);
        }

        return [.. nomes];
    }

    /// <summary>
    /// As chaves sob as quais uma falha diz respeito ao CORPO INTEIRO, e não a um campo dele: a
    /// chave vazia, que o binder usa quando não há o que nomear, e o nome do parâmetro que o
    /// endpoint declara vir do corpo.
    /// </summary>
    /// <remarks>
    /// Sem isto, a requisição sem corpo era anunciada como "campo obrigatório não declarado",
    /// nomeando o parâmetro da action como se fosse campo do contrato. O cliente sairia
    /// procurando no documento um campo que falta — só que documento não houve.
    /// </remarks>
    private static HashSet<string> BodyKeys(ActionContext context)
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase) { string.Empty };
        keys.UnionWith(context.ActionDescriptor.Parameters
            .Where(static p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(BindingKey));

        return keys;
    }

    /// <summary>
    /// As chaves que correspondem a parâmetros que o <b>binder</b> preenche — rota, query,
    /// header, formulário. São as únicas em que "sem valor tentado e sem valor cru" significa
    /// mesmo que o cliente não mandou nada.
    /// </summary>
    /// <remarks>
    /// Dentro do corpo o mesmo sinal não vale: o input formatter não preenche valor tentado nem
    /// valor cru para propriedade nenhuma do modelo, inclusive quando a chave VEIO com
    /// <c>null</c>. Tratar isso como ausência diria ao cliente que ele não declarou um campo que
    /// ele declarou — e, pior, engoliria a mensagem do validador que reprovou, que é do mesmo
    /// tipo que este factory deixa passar de propósito.
    /// </remarks>
    private static HashSet<string> BinderKeys(ActionContext context)
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(context.ActionDescriptor.Parameters
            .Where(static p => p.BindingInfo?.BindingSource is { } source && source != BindingSource.Body)
            .Select(BindingKey));

        return keys;
    }

    /// <summary>
    /// Se dá para PROVAR que a requisição não traz documento algum.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separar "não mandou corpo" de "mandou o corpo <c>null</c>" importa porque nos dois o
    /// parâmetro fica nulo e a exigência implícita reprova igual — pelo <c>ModelState</c> são
    /// indistinguíveis. Mandar acrescentar corpo a quem já mandou um é orientação que não leva
    /// a lugar nenhum.
    /// </para>
    /// <para>
    /// A prova vem do servidor, que sabe se o protocolo permite corpo nesta requisição. Tamanho
    /// <b>desconhecido</b> não é tamanho zero: um documento em chunked, ou um corpo HTTP/2 sem
    /// comprimento declarado, chega sem <c>Content-Length</c> e existe — e HTTP/2 nem usa
    /// codificação de transferência, então deduzir dos cabeçalhos erra justamente ali. Sem o
    /// sinal do servidor, só o comprimento zero declarado prova ausência. Na dúvida esta função
    /// diz não, e a recusa segue para a cadeia: afirmar ausência que não se pode provar é
    /// exatamente o erro que este factory existe para não cometer.
    /// </para>
    /// </remarks>
    private static bool BodyProvablyAbsent(HttpRequest request)
    {
        // O servidor sabe se o protocolo permite corpo nesta requisição — inclusive em HTTP/2,
        // que não usa codificação de transferência e cujo corpo chega sem comprimento
        // declarado. Deduzir isso dos cabeçalhos erra exatamente aí.
        IHttpRequestBodyDetectionFeature? detection =
            request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>();

        return detection is not null
            ? !detection.CanHaveBody
            : request.ContentLength == 0;
    }

    /// <summary>
    /// O nome sob o qual o <c>ModelState</c> registra o parâmetro, que é o nome configurado no
    /// binding quando há um, e o do parâmetro quando não há.
    /// </summary>
    /// <remarks>
    /// Um parâmetro declarado como <c>includeTotal</c> e exposto como <c>include_total</c>
    /// aparece no <c>ModelState</c> pelo segundo nome. Comparar pelo primeiro não encontra a
    /// entrada, e a recusa volta ao envelope do framework sem que nada avise.
    /// </remarks>
    private static string BindingKey(ParameterDescriptor parameter) =>
        parameter.BindingInfo?.BinderModelName ?? parameter.Name;

    private static bool HasAbsentBody(ModelStateDictionary modelState, HashSet<string> bodyKeys) =>
        modelState.Any(entry =>
            entry.Value is { ValidationState: ModelValidationState.Invalid, AttemptedValue: null, RawValue: null }
            && bodyKeys.Contains(entry.Key));

    /// <summary>
    /// Os nomes de campo que a recusa consegue afirmar, sem nenhum texto do framework junto.
    /// </summary>
    private static IReadOnlyList<string> MissingFields(ModelStateDictionary modelState, HashSet<string> binderKeys)
    {
        SortedSet<string> names = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ModelStateEntry> entry in modelState)
        {
            // Entrada que casou não descreve defeito nenhum: o ModelState traz TODAS as
            // propriedades da requisição, não só as reprovadas.
            if (entry.Value.ValidationState != ModelValidationState.Invalid)
            {
                continue;
            }

            if (SyntheticKey.IsMatch(entry.Key))
            {
                foreach (ModelError error in entry.Value.Errors)
                {
                    // A mensagem chega ora no campo de texto, ora na exceção que o formatter
                    // guardou — depende de por onde a recusa passou. Ler as duas evita depender
                    // de um detalhe de configuração que não controlamos; o recorte é o mesmo, e
                    // é ele que mantém o nome do tipo de fora.
                    Match list = NamesAfterIncluding.Match(
                        string.IsNullOrEmpty(error.ErrorMessage)
                            ? error.Exception?.Message ?? string.Empty
                            : error.ErrorMessage);
                    if (!list.Success)
                    {
                        continue;
                    }

                    names.UnionWith(SingleQuoted
                        .Matches(list.Groups["list"].Value)
                        .Select(static match => match.Groups["name"].Value));
                }

                continue;
            }

            // Fora do corpo, o que caracteriza ausência é não haver valor tentado nem valor
            // cru: não houve o que converter, porque nada veio. `?vigentes=abc` também reprova,
            // mas ali o campo FOI declarado — só não converte —, e quem responde por ele é o
            // factory anterior da cadeia.
            //
            // A checagem vale só para chave de parâmetro do BINDER. Propriedade de dentro do
            // corpo satisfaz a mesma condição mesmo tendo vindo com `null`, porque o input
            // formatter não preenche esses valores para o modelo — dizer "não declarou" ali
            // seria falso, e engoliria a mensagem de quem de fato reprovou.
            if (binderKeys.Contains(entry.Key)
                && entry.Value.AttemptedValue is null
                && entry.Value.RawValue is null)
            {
                names.Add(entry.Key);
            }
        }

        return [.. names];
    }

    /// <summary>
    /// Se alguma entrada aponta posição no documento — a marca de que HAVIA documento e quem
    /// o reprovou foi o desserializador, e não um validador que rodou depois do binding.
    /// </summary>
    private static bool HasDeserializationFailure(ModelStateDictionary modelState) =>
        modelState.Any(entry =>
            entry.Value is { ValidationState: ModelValidationState.Invalid }
            && SyntheticKey.IsMatch(entry.Key));

    private static string Enumerate(IReadOnlyList<string> fields) =>
        fields.Count == 1
            ? $"o campo obrigatório '{fields[0]}'"
            : $"os campos obrigatórios {Join(fields)}";

    /// <summary>
    /// A mesma enumeração, sem chamar o parâmetro de obrigatório.
    /// </summary>
    /// <remarks>
    /// Um parâmetro de query com valor default é opcional, e anunciá-lo como obrigatório manda o
    /// cliente procurar defeito onde não há: ele conclui que precisa passar a declará-lo, quando
    /// o que faltava era o valor caber no tipo. A recusa de conversão nada afirma sobre
    /// obrigatoriedade, e a frase não pode afirmar por ela.
    /// </remarks>
    private static string EnumerateParameters(IReadOnlyList<string> names) =>
        names.Count == 1
            ? $"o parâmetro '{names[0]}'"
            : $"os parâmetros {Join(names)}";

    private static string Join(IReadOnlyList<string> names) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{string.Join(", ", names.Take(names.Count - 1).Select(static f => $"'{f}'"))} e '{names[^1]}'");
}
