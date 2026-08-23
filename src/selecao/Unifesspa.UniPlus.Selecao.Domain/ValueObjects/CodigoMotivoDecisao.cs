namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Código do motivo de decisão de isenção (UNI-REQ-0120) — chave natural
/// estável, citada na decisão que o usa (ex.: <c>RENDA_ACIMA_DO_LIMITE</c>,
/// <c>NIS_NAO_LOCALIZADO</c>). Formato fechado
/// <c>^[A-Z][A-Z0-9_]{1,49}$</c>: inicia com letra maiúscula e segue com letras
/// maiúsculas, dígitos e sublinhado, de 2 a 50 caracteres. Persistido como
/// <c>varchar</c> por conversor de valor.
/// </summary>
/// <remarks>
/// O código é <b>estável</b> porque a decisão proferida preserva o código e a
/// descrição vigentes no momento do julgamento (UNI-REQ-0120): quem lê a
/// decisão antiga lê o rótulo daquela época, não o de hoje. A grafia maiúscula
/// segue a mesma convenção de <see cref="FundamentoIsencaoCodigo"/> e
/// <see cref="ResultadoPermitidoCodigo"/> — token de domínio em UPPER_SNAKE,
/// sem acento.
/// </remarks>
public sealed partial record CodigoMotivoDecisao
{
    private const int TamanhoMinimo = 2;
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoMotivoDecisao(string valor) => Valor = valor;

    /// <summary>
    /// Cria o código validando o formato fechado. Nulo ou em branco retorna
    /// <c>CodigoObrigatorio</c>; fora do formato, <c>CodigoFormatoInvalido</c>.
    /// O valor é normalizado por <c>Trim</c> antes da validação.
    /// </summary>
    public static Result<CodigoMotivoDecisao> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoMotivoDecisao>.Failure(new DomainError(
                MotivoDecisaoIsencaoErrorCodes.CodigoObrigatorio,
                "Código do motivo de decisão de isenção é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoMotivoDecisao>.Failure(new DomainError(
                MotivoDecisaoIsencaoErrorCodes.CodigoFormatoInvalido,
                "Código do motivo deve iniciar com letra maiúscula e conter apenas letras maiúsculas, "
                + $"dígitos e sublinhado, com {TamanhoMinimo} a {TamanhoMaximo} caracteres "
                + "(ex.: RENDA_ACIMA_DO_LIMITE)."));
        }

        return Result<CodigoMotivoDecisao>.Success(new CodigoMotivoDecisao(normalizado));
    }

    /// <summary>
    /// Reidrata o código vindo do banco. Falha rápido: uma linha fora do formato
    /// é defeito de persistência, e materializá-la em silêncio espalharia o
    /// valor inválido pelo domínio.
    /// </summary>
    public static CodigoMotivoDecisao Reidratar(string valor)
    {
        Result<CodigoMotivoDecisao> resultado = Criar(valor);

        return resultado.IsSuccess
            ? resultado.Value!
            : throw new InvalidOperationException(
                $"Código de motivo de decisão inválido na persistência: '{valor}'.");
    }

    public override string ToString() => Valor;

    // O quantificador repete o literal de TamanhoMinimo/TamanhoMaximo porque
    // GeneratedRegex exige padrão constante em tempo de compilação; o teste
    // do value object confere os dois limites contra as constantes.
    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
