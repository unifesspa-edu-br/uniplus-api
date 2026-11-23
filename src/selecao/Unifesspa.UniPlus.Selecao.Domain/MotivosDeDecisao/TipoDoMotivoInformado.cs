namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Converte o código de tipo recebido no wire para o vocabulário de decisões, separando o tipo que
/// não veio do tipo que veio e não pertence ao vocabulário. As mensagens listam só os rótulos do
/// vocabulário, nunca o texto recebido.
/// </summary>
public static class TipoDoMotivoInformado
{
    public const string Campo = "tipo";

    /// <summary>Os rótulos do vocabulário, na ordem de declaração, em texto corrido para compor mensagens.</summary>
    public static string RotulosAceitosEmTexto { get; } = string.Join("; ", CatalogoDe<TipoMotivoDeDecisao>.Todos().Select(static declarado => declarado.Rotulo));

    /// <summary>A recusa do tipo que não pertence ao vocabulário, que lista os rótulos aceitos e não cita o texto recebido.</summary>
    public static DomainError TipoForaDoVocabulario() => new(
        MotivoDeDecisaoErrorCodes.TipoInvalido,
        $"O tipo informado não pertence ao vocabulário de decisões. Tipos aceitos: {RotulosAceitosEmTexto}.");

    public static Result<TipoMotivoDeDecisao> Converter(string? tipoDeDecisaoCodigo)
    {
        if (string.IsNullOrWhiteSpace(tipoDeDecisaoCodigo))
        {
            return Falha(
                MotivoDeDecisaoErrorCodes.TipoObrigatorio,
                $"Informe o tipo do motivo. Tipos aceitos: {RotulosAceitosEmTexto}.");
        }

        return CatalogoDe<TipoMotivoDeDecisao>.TentarObter(tipoDeDecisaoCodigo, out TipoMotivoDeDecisao tipo)
            ? Result<TipoMotivoDeDecisao>.Success(tipo)
            : Result<TipoMotivoDeDecisao>.ValidationFailure([new FieldError(Campo, TipoForaDoVocabulario())]);
    }

    private static Result<TipoMotivoDeDecisao> Falha(string codigo, string mensagem) =>
        Result<TipoMotivoDeDecisao>.ValidationFailure([new FieldError(Campo, new DomainError(codigo, mensagem))]);
}
