namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Código de um <see cref="Entities.FatoCandidato"/> (UNI-REQ-0077) — chave natural
/// classificatória do vocabulário de fatos (por exemplo: <c>COR_RACA</c>, <c>PCD</c>,
/// <c>MODALIDADE</c>). Código no formato de <see cref="FormatoDoCodigo"/>
/// — começa por letra maiúscula, seguido de maiúsculas e dígitos, com sublinhados isolados, até 50 caracteres. Persistido como texto via conversor de valor (falha imediata na
/// reidratação).
/// </summary>
/// <remarks>
/// O código é <b>imutável</b>: é ele que entra, por valor, dentro de um predicado
/// congelado num documento de publicação (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0061) — renomear reinterpretaria
/// o passado. A entidade não expõe método que o altere.
/// </remarks>
public sealed record CodigoFatoCandidato
{
    public const int TamanhoMaximo = FatoDoCatalogo.CodigoMaxLength;

    public string Valor { get; }

    private CodigoFatoCandidato(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoFatoCandidato"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato retorna
    /// <c>CodigoFormatoInvalido</c>. O valor é conferido como recebido.
    /// </summary>
    public static Result<CodigoFatoCandidato> Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoFatoCandidato>.Failure(new DomainError(
                FatoCandidatoErrorCodes.CodigoObrigatorio,
                "Código do fato do candidato é obrigatório."));
        }

        return EhValido(valor)
            ? Result<CodigoFatoCandidato>.Success(new CodigoFatoCandidato(valor!))
            : Result<CodigoFatoCandidato>.Failure(new DomainError(
                FatoCandidatoErrorCodes.CodigoFormatoInvalido,
                "Código do fato deve começar por letra maiúscula e conter apenas letras maiúsculas, "
                + $"dígitos e sublinhado, com no máximo {TamanhoMaximo} caracteres (ex.: COR_RACA, PCD, MODALIDADE)."));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem criar o objeto.</summary>
    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
