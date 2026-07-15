namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Átomo tipado <c>{ Fato, Operador, Valor }</c> de um predicado sobre o
/// candidato — a unidade compartilhada por <see cref="ClausulaDnf"/>/
/// <see cref="PredicadoDnf"/> e reusada literalmente por
/// <see cref="ArgsDesempatePredicadoFato"/> (ADR-0111, Story #847).
/// </summary>
/// <remarks>
/// <para>
/// Validação de <b>forma</b>, sem I/O: <see cref="Fato"/> não vazio,
/// <see cref="Operador"/> um dos quatro valores do domínio fechado (nunca o
/// sentinela <see cref="Enums.Operador.Nenhuma"/>), e a forma de
/// <see cref="Valor"/> coerente com o operador — <see cref="Enums.Operador.Em"/>
/// exige array JSON não vazio; os demais exigem escalar (rejeitam array e
/// objeto). A validação <b>semântica</b> (fato existe no vocabulário, operador
/// compatível com o domínio do fato, valor pertence ao domínio) é
/// responsabilidade de <see cref="Services.PredicadoDnfValidador"/>, que
/// recebe o vocabulário como dado — por isso vive separada desta factory.
/// </para>
/// </remarks>
public sealed record CondicaoDnf
{
    /// <remarks>
    /// Privado, mas anotado com <see cref="JsonConstructorAttribute"/>: é o construtor
    /// que o <c>System.Text.Json</c> usa para materializar <c>CriterioDesempate.Args</c>
    /// a partir da coluna <c>json</c> (EF <c>ValueComparer</c>/reidratação de change
    /// tracking) — o mesmo tratamento que os demais registros de
    /// <see cref="ArgsCriterioDesempate"/> já recebem implicitamente (construtor
    /// posicional público). O dado já foi validado por <see cref="Criar"/> quando
    /// escrito; esta materialização não revalida, no mesmo espírito das demais
    /// variantes de <see cref="ArgsCriterioDesempate"/>.
    /// </remarks>
    [JsonConstructor]
    private CondicaoDnf(string fato, Operador operador, JsonElement valor)
    {
        Fato = fato;
        Operador = operador;
        Valor = valor;
    }

    public string Fato { get; }

    public Operador Operador { get; }

    public JsonElement Valor { get; }

    /// <summary>
    /// Cria a condição validando a forma — sem I/O e sem conhecimento do
    /// vocabulário de fatos.
    /// </summary>
    public static Result<CondicaoDnf> Criar(string fato, Operador operador, JsonElement valor)
    {
        if (string.IsNullOrWhiteSpace(fato))
        {
            return Result<CondicaoDnf>.Failure(new DomainError(
                "CondicaoDnf.FatoObrigatorio", "O fato da condição é obrigatório."));
        }

        if (operador == Operador.Nenhuma || !Enum.IsDefined(operador))
        {
            return Result<CondicaoDnf>.Failure(new DomainError(
                "CondicaoDnf.OperadorInvalido", "O operador da condição não é reconhecido."));
        }

        bool formaCoerente = operador == Operador.Em
            ? valor.ValueKind == JsonValueKind.Array && valor.GetArrayLength() > 0 && NenhumItemEmBranco(valor)
            : valor.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object) && !EscalarEmBranco(valor);

        if (!formaCoerente)
        {
            return Result<CondicaoDnf>.Failure(new DomainError(
                "CondicaoDnf.FormaIncoerenteComOperador",
                operador == Operador.Em
                    ? "O operador EM exige um array JSON não vazio, sem itens de texto em branco."
                    : "Os operadores IGUAL, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco."));
        }

        return Result<CondicaoDnf>.Success(new CondicaoDnf(fato.Trim(), operador, valor.Clone()));
    }

    /// <summary>
    /// Uma string vazia/em branco satisfaria a checagem de forma (é escalar,
    /// não é array nem objeto) e faria round-trip perfeito pelo envelope —
    /// mas não decide nada. Só strings são checadas: bool/number JSON não têm
    /// representação "em branco".
    /// </summary>
    private static bool EscalarEmBranco(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valor.GetString());

    private static bool NenhumItemEmBranco(JsonElement valorArray) =>
        !valorArray.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(item.GetString()));
}
