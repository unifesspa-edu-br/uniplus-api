namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código do tipo de deficiência (UNI-REQ-0012 e UNI-REQ-0061, módulo
/// Configuração) — identidade semântica classificatória (por exemplo:
/// <c>DEFICIENCIA_VISUAL</c>, <c>TEA</c>). Código no formato de:
/// <see cref="FormatoDoCodigo"/> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de no máximo 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (falha imediata na reidratação).
/// </summary>
/// <remarks>
/// Espelha <see cref="CodigoCondicao"/> em formato e disciplina de erro, mas sem
/// código reservado: nenhum tipo de deficiência é referenciado literalmente por
/// norma, então não há análogo ao <c>PCD</c> nem proteção contra renome/remoção.
/// </remarks>
public sealed record CodigoTipoDeficiencia
{
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoTipoDeficiencia(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoTipoDeficiencia"/> validando o formato fechado.
    /// Valor nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato
    /// retorna <c>CodigoFormatoInvalido</c>. O valor é conferido como recebido.
    /// </summary>
    public static Result<CodigoTipoDeficiencia> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoTipoDeficiencia>.Failure(new DomainError(
                TipoDeficienciaErrorCodes.CodigoObrigatorio,
                "Código do tipo de deficiência é obrigatório."));
        }

        return EhValido(valor)
            ? Result<CodigoTipoDeficiencia>.Success(new CodigoTipoDeficiencia(valor!))
            : Result<CodigoTipoDeficiencia>.Failure(new DomainError(
                TipoDeficienciaErrorCodes.CodigoFormatoInvalido,
                "Código do tipo de deficiência deve iniciar com letra maiúscula e conter apenas "
                + $"letras maiúsculas, dígitos e sublinhado, com no máximo {TamanhoMaximo} "
                + "caracteres (ex.: DEFICIENCIA_VISUAL, TEA)."));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem criar o objeto.</summary>
    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
