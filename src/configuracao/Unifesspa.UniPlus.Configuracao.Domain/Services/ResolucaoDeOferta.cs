namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Decide qual oferta uma referência natural identifica, a partir das ofertas vivas que a satisfazem.
/// </summary>
/// <remarks>
/// A regra nunca escolhe: nenhuma candidata é referência não encontrada; uma é a oferta; duas ou mais são
/// referência ambígua. Escolher entre candidatas (a primeira, a mais recente, a ativa) faria o documento apontar
/// para uma oferta que quem o escreveu não indicou.
/// </remarks>
public static class ResolucaoDeOferta
{
    public static Result<OfertaCurso> Resolver(IReadOnlyCollection<OfertaCurso> candidatas)
    {
        ArgumentNullException.ThrowIfNull(candidatas);

        return candidatas.Count switch
        {
            0 => Result<OfertaCurso>.Failure(new DomainError(
                OfertaCursoErrorCodes.ReferenciaNaoEncontrada,
                "Nenhuma oferta de curso viva corresponde à referência informada.")),
            1 => Result<OfertaCurso>.Success(candidatas.Single()),
            _ => Result<OfertaCurso>.Failure(new DomainError(
                OfertaCursoErrorCodes.ReferenciaAmbigua,
                $"A referência informada corresponde a {candidatas.Count} ofertas de curso vivas; informe o suficiente para identificar uma só.")),
        };
    }
}
