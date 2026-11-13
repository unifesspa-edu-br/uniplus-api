namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Lê as versões publicadas do processo e o que elas congelaram de cada fato coletado, uma vez por retificação sem
/// sessão. Sem versão, não há o que ler. A versão que o sistema não sabe reidratar recusa, como a abertura da sessão.
/// </summary>
public sealed class LeituraDasVersoesPublicadas(IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope registroCodecs)
{
    internal async Task<Result<FatosDasVersoesPublicadas?>> LerAsync(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await processos
            .ObterVersoesAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versoes.Count == 0)
        {
            return Result<FatosDasVersoesPublicadas?>.Success(null);
        }

        Result<VersoesPublicadasLidas> lidas = VersoesPublicadasLidas.Ler(registroCodecs, versoes, abrindoSessao: false);
        return lidas.IsFailure
            ? Result<FatosDasVersoesPublicadas?>.Failure(lidas.Error!)
            : Result<FatosDasVersoesPublicadas?>.Success(lidas.Value!.Fatos);
    }
}
