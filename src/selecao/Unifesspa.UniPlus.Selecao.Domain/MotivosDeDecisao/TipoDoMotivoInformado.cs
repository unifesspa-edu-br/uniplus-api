namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Converte o código de tipo recebido no wire para o vocabulário de decisões, separando o tipo que
/// não veio do tipo que veio e não pertence ao vocabulário. As mensagens listam só os códigos do
/// vocabulário, nunca o texto recebido.
/// </summary>
public static class TipoDoMotivoInformado
{
    public const string Campo = "tipo";

    public static Result<TipoMotivoDeDecisao> Converter(string? tipoCodigo)
    {
        if (string.IsNullOrWhiteSpace(tipoCodigo))
        {
            return Falha(
                MotivoDeDecisaoErrorCodes.TipoObrigatorio,
                $"Informe o tipo do motivo. Tipos aceitos: {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}.");
        }

        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisaoCodigo.FromCodigo(tipoCodigo);
        return tipo == TipoMotivoDeDecisao.Nenhum
            ? Falha(
                MotivoDeDecisaoErrorCodes.TipoInvalido,
                $"O tipo informado não pertence ao vocabulário de decisões. Tipos aceitos: {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}.")
            : Result<TipoMotivoDeDecisao>.Success(tipo);
    }

    private static Result<TipoMotivoDeDecisao> Falha(string codigo, string mensagem) =>
        Result<TipoMotivoDeDecisao>.ValidationFailure([new FieldError(Campo, new DomainError(codigo, mensagem))]);
}
