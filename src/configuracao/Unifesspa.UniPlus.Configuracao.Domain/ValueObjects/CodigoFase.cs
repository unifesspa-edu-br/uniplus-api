namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de uma <see cref="Entities.FaseCanonica"/> (UNI-REQ-0064, módulo
/// Configuração) — chave natural do vocabulário de cronograma (ex.: <c>INSCRICAO</c>,
/// <c>AVALIACAO</c>, <c>RECURSOS</c>). Value object com formato fechado:
/// <c>^[A-Z_]+$</c> — apenas letras maiúsculas e sublinhado (<b>sem hífen e sem
/// dígito</b>, diferente do <c>CodigoModalidade</c>). Persistido como <c>varchar</c>
/// via conversor de valor (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// O value object valida apenas o <b>formato</b>. A pertença ao conjunto canônico
/// fechado das quatorze fases é guarda de domínio da entidade (factory
/// <c>FaseCanonica.Criar</c>), não do value object — o mesmo código bem-formado
/// pode ou não ser canônico. O código é <b>imutável</b>: o comando de atualização
/// não o aceita como campo editável (vocabulário canônico fixo).
/// </remarks>
public sealed partial record CodigoFase
{
    private const int TamanhoMaximo = 60;

    public string Valor { get; }

    private CodigoFase(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoFase"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato (ou acima
    /// do tamanho máximo) retorna <c>CodigoFormatoInvalido</c>. O valor é
    /// normalizado por <c>Trim</c> antes da validação.
    /// </summary>
    public static Result<CodigoFase> Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoFase>.Failure(new DomainError(
                FaseCanonicaErrorCodes.CodigoObrigatorio,
                "Código da fase canônica é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (normalizado.Length > TamanhoMaximo || !FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoFase>.Failure(new DomainError(
                FaseCanonicaErrorCodes.CodigoFormatoInvalido,
                "Código da fase deve conter apenas letras maiúsculas e sublinhado "
                + $"(sem hífen e sem dígito), com no máximo {TamanhoMaximo} caracteres "
                + "(ex.: INSCRICAO, AVALIACAO, RECURSOS)."));
        }

        return Result<CodigoFase>.Success(new CodigoFase(normalizado));
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
