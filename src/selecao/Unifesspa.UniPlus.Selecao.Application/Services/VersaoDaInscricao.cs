namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>A versão da configuração em que está a inscrição: a divulgada, no rascunho, e a congelada no primeiro envio, depois dele.</summary>
public sealed class VersaoDaInscricao(
    ICertameDivulgadoRepository certames,
    IProcessoSeletivoRepository processos,
    IRegistroCodecsEnvelope codecs)
{
    /// <summary>A versão em que a inscrição está, para ler dela.</summary>
    public Task<Result<VersaoConfiguracao>> ResolverAsync(Inscricao inscricao, CancellationToken cancellationToken) =>
        VersaoDaInscricaoResolver.ResolverAsync(inscricao, certames, processos, cancellationToken);

    /// <summary>Leva o rascunho à versão divulgada, na memória; a inscrição enviada fica como está.</summary>
    public Task<Result> AdotarAsync(Inscricao inscricao, CancellationToken cancellationToken) =>
        VersaoDaInscricaoResolver.AdotarAsync(inscricao, certames, processos, codecs, cancellationToken);

    /// <summary>O contexto da versão fixada da inscrição, já adotada, com as ofertas de curso escolhidas.</summary>
    public Task<Result<ContextoDaVersaoFixada>> ContextoAsync(
        Inscricao inscricao, Guid? ofertaCursoId, Guid? ofertaCursoSegundaOpcaoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        return ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, inscricao.ProcessoSeletivoId, processos, codecs, ofertaCursoId, ofertaCursoSegundaOpcaoId, cancellationToken);
    }
}
