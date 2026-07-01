namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de um <see cref="Entities.TipoBanca"/> (UNI-REQ-0064, módulo
/// Configuração) — chave natural classificatória da banca (ex.:
/// <c>BANCA_ANALISE_DOCUMENTAL</c>, <c>BANCA_ENTREVISTA</c>). Value object com
/// formato fechado: <c>^[A-Z_]+$</c> — apenas letras maiúsculas e sublinhado (sem
/// hífen e sem dígito). Persistido como <c>varchar</c> via conversor de valor
/// (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// O value object valida apenas o <b>formato</b>. A pertença ao conjunto canônico
/// fechado das quatro bancas é guarda de domínio da entidade
/// (<c>TipoBanca.Criar</c>). O código é <b>imutável</b>: o comando de atualização
/// não o aceita.
/// </remarks>
public sealed partial record CodigoBanca
{
    private const int TamanhoMaximo = 60;

    public string Valor { get; }

    private CodigoBanca(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoBanca"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato (ou acima
    /// do tamanho máximo) retorna <c>CodigoFormatoInvalido</c>. O valor é
    /// normalizado por <c>Trim</c> antes da validação.
    /// </summary>
    public static Result<CodigoBanca> Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoBanca>.Failure(new DomainError(
                TipoBancaErrorCodes.CodigoObrigatorio,
                "Código do tipo de banca é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (normalizado.Length > TamanhoMaximo || !FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoBanca>.Failure(new DomainError(
                TipoBancaErrorCodes.CodigoFormatoInvalido,
                "Código do tipo de banca deve conter apenas letras maiúsculas e sublinhado "
                + $"(sem hífen e sem dígito), com no máximo {TamanhoMaximo} caracteres "
                + "(ex.: BANCA_ENTREVISTA)."));
        }

        return Result<CodigoBanca>.Success(new CodigoBanca(normalizado));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar value object.</summary>
    public static bool EhValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor)
        && valor.Trim().Length <= TamanhoMaximo
        && FormatoValido().IsMatch(valor.Trim());

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
