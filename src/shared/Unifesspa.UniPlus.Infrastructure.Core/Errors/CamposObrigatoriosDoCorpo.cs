namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Os campos do corpo que o ASP.NET recusou só porque o tipo declara o texto como não anulável e
/// o cliente o omitiu ou o mandou <c>null</c>.
/// </summary>
/// <remarks>
/// <para>
/// O ASP.NET trata a propriedade de tipo de referência não anulável como se tivesse
/// <c>[Required]</c>. Depois que o corpo é lido, a propriedade vazia reprova no <c>ModelState</c>
/// com uma chave que nomeia o caminho C# (<c>[0].Documento.Condicoes[0].Fato</c>), e a resposta
/// padrão do framework sai fora do envelope da ADR-0023.
/// </para>
/// <para>
/// O reconhecimento NÃO lê a mensagem do framework. A chave é percorrida na descrição do tipo do
/// corpo — <c>[i]</c> entra no elemento da lista e <c>Nome</c> na propriedade —, e só vale quando a
/// propriedade alcançada tem como única validação a obrigatoriedade implícita. Uma propriedade com
/// outra validação, ou com <c>[Required]</c> que traz mensagem própria, reprova por um motivo que
/// este tipo não conhece, e a mensagem dela vale mais do que a recusa genérica.
/// </para>
/// <para>
/// Só se responde quando TODA chave reprovada é desse tipo; basta uma que não seja para devolver
/// vazio e deixar a cadeia responder.
/// </para>
/// </remarks>
internal static class CamposObrigatoriosDoCorpo
{
    /// <summary>
    /// Os campos no casing do payload e na ordem em que o framework os reprovou; vazio quando
    /// alguma chave reprovada não é recusa só da obrigatoriedade implícita.
    /// </summary>
    public static IReadOnlyList<string> Identificar(ActionContext context, IReadOnlySet<string> chavesDoCorpo)
    {
        if (DescricaoDoCorpo(context) is not { } raiz)
        {
            return [];
        }

        string?[] campos = [.. context.ModelState
            .Where(static entrada => entrada.Value?.ValidationState == ModelValidationState.Invalid)
            .Select(entrada => CampoObrigatorio(raiz, entrada.Key, chavesDoCorpo))];

        return campos.Contains(null) ? [] : [.. campos.Distinct(StringComparer.Ordinal)!];
    }

    private static ModelMetadata? DescricaoDoCorpo(ActionContext context)
    {
        ParameterDescriptor? corpo = context.ActionDescriptor.Parameters
            .FirstOrDefault(static p => p.BindingInfo?.BindingSource == BindingSource.Body);

        return corpo is null
            ? null
            : context.HttpContext.RequestServices.GetRequiredService<IModelMetadataProvider>()
                .GetMetadataForType(corpo.ParameterType);
    }

    private static string? CampoObrigatorio(ModelMetadata raiz, string chave, IReadOnlySet<string> chavesDoCorpo)
    {
        foreach (string caminho in CaminhosPossiveis(chave, chavesDoCorpo))
        {
            if (Percorrer(raiz, caminho) is { MetadataKind: ModelMetadataKind.Property } propriedade
                && ApenasObrigatoriedadeImplicita(propriedade))
            {
                return NomeDeCampoNoPayload.De(caminho);
            }
        }

        return null;
    }

    /// <summary>
    /// A chave como veio e, quando o framework a prefixou com o nome do parâmetro do corpo, a chave
    /// sem esse prefixo.
    /// </summary>
    private static IEnumerable<string> CaminhosPossiveis(string chave, IReadOnlySet<string> chavesDoCorpo)
    {
        yield return chave;

        foreach (string prefixo in chavesDoCorpo.Where(static c => c.Length > 0))
        {
            if (chave.Length > prefixo.Length
                && chave.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
                && chave[prefixo.Length] is '.' or '[')
            {
                yield return chave[prefixo.Length..].TrimStart('.');
            }
        }
    }

    private static ModelMetadata? Percorrer(ModelMetadata raiz, string caminho)
    {
        ModelMetadata? atual = raiz;

        foreach (string segmento in caminho.Split('.'))
        {
            atual = atual is null ? null : DescerUmSegmento(atual, segmento);
        }

        return atual;
    }

    /// <summary>
    /// Um segmento é <c>Nome</c>, <c>Nome[i]</c> ou <c>[i]</c>: o nome escolhe a propriedade e cada
    /// índice entra no elemento da lista.
    /// </summary>
    private static ModelMetadata? DescerUmSegmento(ModelMetadata atual, string segmento)
    {
        int abre = segmento.IndexOf('[', StringComparison.Ordinal);
        string nome = abre < 0 ? segmento : segmento[..abre];
        ModelMetadata? metadata = nome.Length == 0 ? atual : atual.Properties[nome];

        for (int indices = abre < 0 ? 0 : segmento.Count(static c => c == '['); indices > 0 && metadata is not null; indices--)
        {
            metadata = metadata.ElementMetadata;
        }

        return metadata;
    }

    private static bool ApenasObrigatoriedadeImplicita(ModelMetadata propriedade) =>
        propriedade.ValidatorMetadata.Count > 0
        && propriedade.ValidatorMetadata.All(static v =>
            v is RequiredAttribute { ErrorMessage: null, ErrorMessageResourceName: null });
}
