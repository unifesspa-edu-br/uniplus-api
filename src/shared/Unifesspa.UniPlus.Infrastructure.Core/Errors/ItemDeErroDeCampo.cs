namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um item do <c>errors[]</c> da ADR-0023: o campo, o código publicado do erro e a mensagem. Os nomes no fio são fixos
/// pelos atributos, para que a resposta de recusa e o diagnóstico de uma resposta de sucesso tenham a mesma forma
/// qualquer que seja a política de nomes do serializador.
/// </summary>
/// <param name="Field">O campo, ou o JSON Pointer do item quando o corpo é um documento; <see langword="null"/> quando o erro não é de campo, e então a chave não vai no fio.</param>
/// <param name="Code">O código publicado do erro, resolvido pelo catálogo de erros.</param>
/// <param name="Message">A mensagem do erro, que nunca repete o valor recusado.</param>
/// <param name="Identificadores">O identificador estável de cada item de lista que o campo atravessa, na ordem do caminho; <see langword="null"/> quando a causa não aponta item com identificador, e então a chave não vai no fio.</param>
public sealed record ItemDeErroDeCampo(
    [property: JsonPropertyName("field"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Field,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("identificadores"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<IdentificadorDoItemNoFio>? Identificadores = null)
{
    public static ItemDeErroDeCampo De(FieldError erro, IDomainErrorMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(erro);
        ArgumentNullException.ThrowIfNull(mapper);

        (int _, string _, string _, string codigo, bool _) = DomainErrorProblemDetailsFactory.Resolve(erro.Error, mapper);
        return new ItemDeErroDeCampo(
            erro.Field,
            codigo,
            erro.Error.Message,
            erro.Identificadores is { Count: > 0 } identificadores ? [.. identificadores.Select(IdentificadorDoItemNoFio.De)] : null);
    }
}
