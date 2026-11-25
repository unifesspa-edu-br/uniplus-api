namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código da condição que habilita atendimento especializado (UNI-REQ-0012,
/// módulo Configuração) — chave natural classificatória (ex.: <c>PCD</c>,
/// <c>DISLEXIA</c>, <c>LACTANTE</c>). Value object com formato fechado:
/// <c>^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$</c> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de no máximo 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// O código <see cref="Pcd"/> é <b>reservado</b>: a condição cujo código é
/// <c>PCD</c> não pode ser renomeada nem removida (a ADR-0067 a referencia
/// literalmente). A proteção é exposta por <see cref="EhProtegido"/> e aplicada
/// pelo agregado/handlers, não pelo banco.
/// </remarks>
public sealed record CodigoCondicao
{
    private const int TamanhoMaximo = 50;

    /// <summary>Código reservado da condição de pessoa com deficiência (ADR-0067).</summary>
    public const string Pcd = "PCD";

    public string Valor { get; }

    private CodigoCondicao(string valor) => Valor = valor;

    /// <summary>
    /// Indica se este é o código reservado <see cref="Pcd"/> — comparação
    /// case-sensitive (<see cref="StringComparison.Ordinal"/>), alinhada ao
    /// formato canônico do value object (maiúsculas).
    /// </summary>
    public bool EhProtegido => string.Equals(Valor, Pcd, StringComparison.Ordinal);

    /// <summary>
    /// Cria um <see cref="CodigoCondicao"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato retorna
    /// <c>CodigoFormatoInvalido</c>. O valor é conferido como recebido.
    /// </summary>
    public static Result<CodigoCondicao> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoCondicao>.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.CodigoObrigatorio,
                "Código da condição de atendimento especializado é obrigatório."));
        }

        return EhValido(valor)
            ? Result<CodigoCondicao>.Success(new CodigoCondicao(valor!))
            : Result<CodigoCondicao>.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido,
                "Código da condição deve iniciar com letra maiúscula e conter apenas letras "
                + $"maiúsculas, dígitos e sublinhado, com no máximo {TamanhoMaximo} "
                + "caracteres (ex.: PCD, DISLEXIA, LACTANTE)."));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar value object.</summary>
    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
