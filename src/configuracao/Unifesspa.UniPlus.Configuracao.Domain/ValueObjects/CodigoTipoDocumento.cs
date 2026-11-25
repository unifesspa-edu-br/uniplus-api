namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código do tipo de documento (UNI-REQ-0013, módulo Configuração) — chave
/// natural classificatória (ex.: <c>RG</c>, <c>LAUDO_MEDICO</c>,
/// <c>DECLARACAO_IRPF_2025</c>). Value object com formato fechado:
/// <c>^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$</c> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de no máximo 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// <para>O dígito vale em qualquer posição <b>menos a primeira</b>. É essa
/// restrição que separa o código semântico da numeração sequencial: preserva
/// <c>DECLARACAO_IRPF_2025</c> e <c>LEI_12711</c>, onde o número é exercício
/// fiscal ou número de lei, e recusa <c>01</c>, que não identifica nada por si.</para>
/// <para>Fechar o formato aqui não é preferência de grafia. Este código é a chave
/// de igualdade ordinal com que o motor de conformidade legal decide se um edital
/// pode ser publicado, e entra no hash do envelope de publicação — congelado por
/// valor no consumidor (ADR-0061), de modo que corrigir o cadastro depois não
/// corrige o edital já publicado.</para>
/// <para>O código continua <b>editável</b> no cadastro, ao contrário do da
/// Modalidade: é justamente o congelamento por snapshot-copy que torna a edição
/// inofensiva para quem já consumiu.</para>
/// </remarks>
public sealed record CodigoTipoDocumento
{
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoTipoDocumento(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoTipoDocumento"/> validando o formato fechado.
    /// Valor nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato
    /// retorna <c>CodigoFormatoInvalido</c>. O valor é conferido como recebido.
    /// </summary>
    public static Result<CodigoTipoDocumento> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoTipoDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.CodigoObrigatorio,
                "Código do tipo de documento é obrigatório."));
        }

        return EhValido(valor)
            ? Result<CodigoTipoDocumento>.Success(new CodigoTipoDocumento(valor!))
            : Result<CodigoTipoDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.CodigoFormatoInvalido,
                "Código do tipo de documento deve iniciar com letra maiúscula e conter apenas "
                + $"letras maiúsculas, dígitos e sublinhado, com no máximo {TamanhoMaximo} "
                + "caracteres (ex.: RG, LAUDO_MEDICO, DECLARACAO_IRPF_2025)."));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar o value object.</summary>
    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
