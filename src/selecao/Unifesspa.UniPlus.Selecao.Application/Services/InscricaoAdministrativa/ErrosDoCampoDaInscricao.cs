namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A resposta única de quem pede um campo que não alcança: o campo que não existe na inscrição, a inscrição que a pessoa
/// não alcança e a falta de permissão respondem o mesmo erro, para que nenhum revele o outro.
/// </summary>
public static class ErrosDoCampoDaInscricao
{
    public static DomainError NaoEncontrado { get; } = new(
        InscricaoErrorCodes.CampoNaoEncontrado,
        "Campo não encontrado nesta inscrição.");
}
