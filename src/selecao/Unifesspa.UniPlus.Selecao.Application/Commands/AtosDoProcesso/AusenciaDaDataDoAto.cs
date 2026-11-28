namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A data do ato é obrigatória em todo ato do processo. Ausente ou nula é recusada junto das demais causas da
/// preparação, no campo do pedido, antes das que a conferência trouxe; o ato só é montado quando a data existe.
/// </summary>
internal static class AusenciaDaDataDoAto
{
    public static Result<AtoAPublicar> Concluir(IDadosDoAtoPublicado dados, Result<AtoConferido> conferido)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(conferido);

        if (dados.DataDoAto is not { } data)
        {
            return Result<AtoAPublicar>.ValidationFailure([DataAusente(), .. conferido.Errors]);
        }

        return conferido.IsFailure
            ? Result<AtoAPublicar>.ValidationFailure(conferido.Errors)
            : Result<AtoAPublicar>.Success(conferido.Value!.ComAData(dados, data));
    }

    internal static FieldError DataAusente() =>
        new("dataDoAto", new DomainError(CodigosDeValidacao.Obrigatorio, "Informe a data do ato declarada pelo documento."));
}
