namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código estável de uma base legal do Fator de Atenuação das Desigualdades de Acesso (FADA), o que o documento
/// de configuração usa para referenciá-la. É formado uma vez, na criação, a partir do tipo do instrumento e da
/// identificação (ex.: <c>PORTARIA_UNIFESSPA_NO_2514_2023</c>), e nunca muda, mesmo que o tipo ou a identificação
/// sejam editados depois. Formato fechado: letra maiúscula seguida de letras maiúsculas, dígitos e sublinhado,
/// até <see cref="TamanhoMaximo"/> caracteres.
/// </summary>
/// <remarks>
/// O código identifica o ato no momento do cadastro; a decisão de negócio usa sempre o <c>Id</c>. Identificações que só
/// diferem em acento, caixa ou pontuação geram o mesmo código e são o mesmo ato.
/// </remarks>
public sealed record CodigoDaBaseLegalDoFada
{
    public const int TamanhoMaximo = 80;

    public string Valor { get; }

    private CodigoDaBaseLegalDoFada(string valor) => Valor = valor;

    /// <summary>Forma o código do ato a partir do tipo e da identificação, sem repetir o tipo quando a identificação já o traz.</summary>
    public static Result<CodigoDaBaseLegalDoFada> Gerar(TipoInstrumentoNormativo tipoInstrumento, string identificacao)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        string normalizada = GeradorDeCodigoEstavel.Normalizar(identificacao);

        return normalizada.Length == 0
            ? Result<CodigoDaBaseLegalDoFada>.Failure(new DomainError(
                BaseLegalDoFadaErrorCodes.CodigoFormatoInvalido,
                "A identificação precisa ter ao menos uma letra ou um dígito para formar o código da base legal."))
            : LimitarTamanho(ComporComOTipo(tipoInstrumento.Codigo(), normalizada));
    }

    /// <summary>Valida um código já formado, vindo de uma referência do documento ou do banco.</summary>
    public static Result<CodigoDaBaseLegalDoFada> Criar(string? valor)
    {
        return EhValido(valor)
            ? Result<CodigoDaBaseLegalDoFada>.Success(new CodigoDaBaseLegalDoFada(valor!))
            : Result<CodigoDaBaseLegalDoFada>.Failure(new DomainError(
                BaseLegalDoFadaErrorCodes.CodigoFormatoInvalido,
                $"O código da base legal deve iniciar com letra maiúscula e conter apenas letras maiúsculas, dígitos e sublinhado, com no máximo {TamanhoMaximo} caracteres."));
    }

    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;

    private static string ComporComOTipo(string prefixo, string normalizada) =>
        normalizada == prefixo || normalizada.StartsWith(prefixo + "_", StringComparison.Ordinal)
            ? normalizada
            : $"{prefixo}_{normalizada}";

    private static Result<CodigoDaBaseLegalDoFada> LimitarTamanho(string codigo) =>
        codigo.Length > TamanhoMaximo
            ? Result<CodigoDaBaseLegalDoFada>.Failure(new DomainError(
                BaseLegalDoFadaErrorCodes.CodigoTamanho,
                $"O código da base legal, formado pelo tipo e pela identificação, passa de {TamanhoMaximo} caracteres: abrevie a identificação."))
            : Result<CodigoDaBaseLegalDoFada>.Success(new CodigoDaBaseLegalDoFada(codigo));

}
