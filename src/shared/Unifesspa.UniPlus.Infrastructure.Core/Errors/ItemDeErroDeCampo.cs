namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um item do <c>errors[]</c> da ADR-0023: o campo, o código publicado do erro e a mensagem. Os nomes no fio são fixos
/// pelos atributos, para que a resposta de recusa e o diagnóstico de uma resposta de sucesso tenham a mesma forma
/// qualquer que seja a política de nomes do serializador.
/// </summary>
/// <param name="Field">O campo, ou o JSON Pointer do item quando o corpo é um documento; <see langword="null"/> quando o erro não é de campo.</param>
/// <param name="Code">O código publicado do erro, resolvido pelo catálogo de erros.</param>
/// <param name="Message">A mensagem do erro, que nunca repete o valor recusado.</param>
public sealed record ItemDeErroDeCampo(
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message)
{
    public static ItemDeErroDeCampo De(FieldError erro, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(erro);
        ArgumentNullException.ThrowIfNull(mapper);

        (int _, string _, string _, string codigo, bool _) = DomainErrorProblemDetailsFactory.Resolve(erro.Error, mapper);
        return new ItemDeErroDeCampo(erro.Field, codigo, erro.Error.Message);
    }
}
