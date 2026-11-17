namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Dá à recusa da forma do documento a raiz que o resultado HTTP usa: o primeiro erro decide status e código. O grupo
/// <c>forma_invalida</c> vai na frente das causas, salvo quando a primeira já é ele ou é o limite do documento, que tem
/// o status próprio.
/// </summary>
public static class RecusaDaForma
{
    private static readonly FieldError Grupo = new(
        null,
        new DomainError(
            DocumentoDaConfiguracaoErrorCodes.FormaInvalida,
            "O documento não respeita o formato da configuração do processo seletivo."));

    public static IReadOnlyList<FieldError> ComGrupo(IReadOnlyList<FieldError> causas)
    {
        ArgumentNullException.ThrowIfNull(causas);

        return JaTemRaiz(causas[0]) ? causas : [Grupo, .. causas];
    }

    private static bool JaTemRaiz(FieldError causa) =>
        causa.Error.Code is DocumentoDaConfiguracaoErrorCodes.FormaInvalida or DocumentoDaConfiguracaoErrorCodes.AcimaDoLimite;
}
