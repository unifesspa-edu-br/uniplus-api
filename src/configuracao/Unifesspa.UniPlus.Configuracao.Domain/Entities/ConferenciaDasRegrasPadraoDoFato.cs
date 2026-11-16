namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que o fato precisa ser para ter regras padrão: do administrador e derivado por regra, com as regras
/// conferidas contra o catálogo (<see cref="ValidadorRegrasPadrao"/>).
/// </summary>
internal static class ConferenciaDasRegrasPadraoDoFato
{
    internal static Result Conferir(FatoCandidato fato, IReadOnlyList<RegraDerivacao> regras, CatalogoDeFatos catalogo)
    {
        if (fato.SemeadoPeloSistema)
        {
            return Result.Failure(RedefinicaoDoFatoDeSistema.RecusaDaDefinicao);
        }

        return fato.DerivadoPorRegra
            ? ValidadorRegrasPadrao.Validar(fato, regras, catalogo)
            : Result.Failure(new DomainError(
                FatoCandidatoErrorCodes.RegrasPadraoSoEmDerivadoPorRegra,
                "Só o fato derivado por regra tem regras padrão."));
    }
}
