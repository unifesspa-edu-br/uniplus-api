namespace Unifesspa.UniPlus.Regras.ValueObjects;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Átomo tipado <c>{ Fato, Operador, Valor }</c> de um predicado sobre o
/// candidato — a unidade compartilhada por <see cref="ClausulaDnf"/>/
/// <see cref="PredicadoDnf"/> e reusada literalmente por
/// <c>ParametrosDesempatePredicadoFato</c> (ADR-0111).
/// </summary>
/// <remarks>
/// <para>
/// Validação de <b>forma</b>, sem I/O: <see cref="Fato"/> não vazio,
/// <see cref="Operador"/> um dos valores do domínio fechado, e a forma de
/// <see cref="Valor"/> coerente com o operador — <see cref="Regras.Enums.Operador.AlgumDe"/> e
/// <see cref="Regras.Enums.Operador.NenhumDe"/> exigem array JSON, sem itens de
/// texto em branco; array <b>vazio</b> é aceito na forma (a semântica de avaliar
/// <c>ALGUM_DE []</c>/<c>NENHUM_DE []</c> é de <see cref="PredicadoDnf"/>, não desta factory). Os
/// demais operadores (<see cref="Regras.Enums.Operador.Igual"/>, <see cref="Regras.Enums.Operador.Diferente"/>,
/// <see cref="Regras.Enums.Operador.MaiorIgual"/>, <see cref="Regras.Enums.Operador.MenorIgual"/>) exigem
/// escalar (rejeitam array e objeto). A validação <b>semântica</b> (fato existe no
/// vocabulário, operador compatível com o domínio do fato, valor pertence ao domínio) é
/// responsabilidade de <see cref="Regras.Services.PredicadoDnfValidador"/>, que
/// recebe o vocabulário como dado — por isso vive separada desta factory.
/// </para>
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CondicaoDnf
{
    /// <remarks>
    /// Privado, mas anotado com <see cref="JsonConstructorAttribute"/>: é o construtor
    /// que o <c>System.Text.Json</c> usa para materializar <c>CriterioDesempate.Parametros</c>
    /// a partir da coluna <c>json</c> (EF <c>ValueComparer</c>/reidratação de change
    /// tracking) — o mesmo tratamento que os demais registros de
    /// <c>ParametrosCriterioDesempate</c> já recebem implicitamente (construtor
    /// posicional público). O dado já foi validado por <see cref="Criar"/> quando
    /// escrito; esta materialização não revalida, no mesmo espírito das demais
    /// variantes de <c>ParametrosCriterioDesempate</c>.
    /// </remarks>
    [JsonConstructor]
    private CondicaoDnf(string fato, Operador operador, JsonElement valor)
    {
        Fato = fato;
        Operador = operador;
        Valor = valor;
    }

    [JsonPropertyName("codigoDoDado")]
    [JsonInclude]
    [JsonRequired]
    public string Fato { get; private init; }

    public Operador Operador { get; }

    public JsonElement Valor { get; }

    /// <summary>
    /// Cria a condição validando a forma — sem I/O e sem conhecimento do
    /// vocabulário de fatos. Devolve a primeira recusa.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">O operador não é membro do enum, como num cast de inteiro.</exception>
    public static Result<CondicaoDnf> Criar(string fato, Operador operador, JsonElement valor)
    {
        if (!Enum.IsDefined(operador))
        {
            throw new ArgumentOutOfRangeException(nameof(operador), operador, "O operador da condição não é membro do enum de operadores.");
        }

        DomainError? recusa = RecusaDoFato(fato) ?? RecusaDaForma(operador, valor);

        return recusa is null
            ? Result<CondicaoDnf>.Success(new CondicaoDnf(fato.Trim(), operador, valor.Clone()))
            : Result<CondicaoDnf>.Failure(recusa);
    }

    /// <summary>
    /// Cria a condição a partir do que o pedido ou o documento informou: o operador chega como o código que o contrato
    /// publica. Acumula a recusa do fato e a do operador, e confere a forma do valor só quando o operador foi lido, porque
    /// a forma depende dele. Cada recusa vem no campo de origem: <c>fato</c>, <c>operador</c> e <c>valor</c>.
    /// </summary>
    public static Result<CondicaoDnf> CriarInformada(string? fato, string? operador, JsonElement valor)
    {
        Operador? lido = CatalogoDe<Operador>.TentarObter(operador, out Operador encontrado) ? encontrado : null;
        List<FieldError> erros = [.. RecusasDaCondicaoInformada(fato, lido, valor)];

        return erros.Count == 0 && fato is not null && lido is { } operadorLido
            ? Result<CondicaoDnf>.Success(new CondicaoDnf(fato.Trim(), operadorLido, valor.Clone()))
            : Result<CondicaoDnf>.ValidationFailure(erros);
    }

    private static IEnumerable<FieldError> RecusasDaCondicaoInformada(string? fato, Operador? operador, JsonElement valor)
    {
        if (RecusaDoFato(fato) is { } recusaDoFato)
        {
            yield return new("fato", recusaDoFato);
        }

        if (operador is not { } lido)
        {
            yield return new("operador", OperadorNaoReconhecido());
        }
        else if (RecusaDaForma(lido, valor) is { } recusaDaForma)
        {
            yield return new("valor", recusaDaForma);
        }
    }

    private static DomainError? RecusaDoFato(string? fato) =>
        string.IsNullOrWhiteSpace(fato)
            ? new DomainError("CondicaoDnf.FatoObrigatorio", "O fato da condição é obrigatório.")
            : null;

    private static DomainError OperadorNaoReconhecido() =>
        new("CondicaoDnf.OperadorInvalido",
            $"O operador da condição não é reconhecido. Os operadores aceitos são {string.Join(", ", CatalogoDe<Operador>.Todos().Select(static d => d.Codigo))}.");

    private static DomainError? RecusaDaForma(Operador operador, JsonElement valor)
    {
        bool ehOperadorDeLista = operador is Operador.AlgumDe or Operador.NenhumDe;
        bool formaCoerente = ehOperadorDeLista
            ? valor.ValueKind == JsonValueKind.Array && NenhumItemEmBranco(valor)
            : valor.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object) && !EscalarEmBranco(valor);

        return formaCoerente
            ? null
            : new DomainError(
                "CondicaoDnf.FormaIncoerenteComOperador",
                ehOperadorDeLista
                    ? "Os operadores ALGUM_DE e NENHUM_DE exigem um array JSON, sem itens de texto em branco (array vazio é aceito)."
                    : "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.");
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
