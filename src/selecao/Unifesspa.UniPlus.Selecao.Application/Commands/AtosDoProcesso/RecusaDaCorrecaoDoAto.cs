namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A recusa da correção sem ato corrigido ou sem motivo, que se confere sem ler o processo. A data, os documentos
/// ausentes e a quantidade de anexos não dependem dela, e saem na mesma resposta (ADR-0125).
/// </summary>
internal static class RecusaDaCorrecaoDoAto
{
    public static Result<AtoPublicadoDto>? De(IDadosDoAtoPublicado dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        Result correcao = AtoDoProcesso.ValidarCorrecao(dados.AtoCorrigidoId, dados.MotivoDaCorrecao);
        if (correcao.IsSuccess)
        {
            return null;
        }

        return Result<AtoPublicadoDto>.ValidationFailure([.. correcao.Errors, .. RecusasIndependentesDaCorrecao(dados)]);
    }

    private static IEnumerable<FieldError> RecusasIndependentesDaCorrecao(IDadosDoAtoPublicado dados)
    {
        FieldError[] dataAusente = dados.DataDoAto is null ? [AusenciaDaDataDoAto.DataAusente()] : [];
        IReadOnlyList<AnexoSolicitadoDoAto> anexos = dados.Anexos ?? [];
        return
        [
            .. dataAusente,
            .. ConferenciaDosDocumentosDoAto.DocumentosAusentes(dados.DocumentoPrincipalId, anexos),
            .. ConferenciaDosDocumentosDoAto.QuantidadeExcedida(anexos),
        ];
    }
}
