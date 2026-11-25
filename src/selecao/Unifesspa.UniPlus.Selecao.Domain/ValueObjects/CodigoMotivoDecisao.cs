namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Código do motivo de decisão (UNI-REQ-0120, UNI-REQ-0283) — chave natural
/// estável dentro do tipo do motivo, citada na decisão que o usa (ex.:
/// <c>RENDA_ACIMA_DO_LIMITE</c>, <c>SEM_RESULTADO_INEP</c>). Formato fechado
/// começa com letra maiúscula e segue com letras maiúsculas ou dígitos, separados
/// por sublinhados isolados, até 50 caracteres. Persistido como
/// <c>varchar</c> por conversor de valor.
/// </summary>
/// <remarks>
/// O código é <b>estável</b> porque a decisão proferida preserva o código e a
/// descrição vigentes no momento do julgamento (UNI-REQ-0120): quem lê a
/// decisão antiga lê o rótulo daquela época, não o de hoje. A grafia maiúscula
/// segue a mesma convenção de <see cref="FundamentoIsencao"/> e
/// <see cref="SentidoDoMotivo"/> — token de domínio em UPPER_SNAKE,
/// sem acento.
/// </remarks>
public sealed record CodigoMotivoDecisao
{
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoMotivoDecisao(string valor) => Valor = valor;

    /// <summary>
    /// Cria o código validando o formato fechado. Nulo ou em branco retorna
    /// <c>CodigoObrigatorio</c>; fora do formato, <c>CodigoFormatoInvalido</c>.
    /// </summary>
    public static Result<CodigoMotivoDecisao> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoMotivoDecisao>.Failure(new DomainError(
                MotivoDeDecisaoErrorCodes.CodigoObrigatorio,
                "Código do motivo de decisão é obrigatório."));
        }

        return valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor)
            ? Result<CodigoMotivoDecisao>.Success(new CodigoMotivoDecisao(valor))
            : RecusarFormato();
    }

    private static Result<CodigoMotivoDecisao> RecusarFormato() =>
        Result<CodigoMotivoDecisao>.Failure(new DomainError(
            MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido,
            "Código do motivo deve iniciar com letra maiúscula e conter apenas letras maiúsculas, "
            + $"dígitos e sublinhados isolados, com até {TamanhoMaximo} caracteres."));

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
                "Código de motivo de decisão inválido na persistência.");
    }

    public override string ToString() => Valor;

}
