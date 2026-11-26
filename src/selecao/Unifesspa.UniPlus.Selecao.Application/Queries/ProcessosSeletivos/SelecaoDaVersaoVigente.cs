namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

internal static class SelecaoDaVersaoVigente
{
    public static async Task<Result<VersaoConfiguracao>> ObterAsync(Guid processoId, DateTimeOffset instante, IProcessoSeletivoRepository repositorio, CancellationToken cancellationToken)
    {
        VersaoConfiguracao? versao = await repositorio.ObterVersaoVigenteAsync(processoId, instante, cancellationToken).ConfigureAwait(false);
        if (versao is not null)
        {
            return Result<VersaoConfiguracao>.Success(versao);
        }
        bool existe = await repositorio.ExisteAsync(processoId, cancellationToken).ConfigureAwait(false);
        return Result<VersaoConfiguracao>.Failure(existe
            ? new DomainError("Snapshot.VigenteAusente", $"Nenhuma publicação vigente para o instante {instante:O}.")
            : AcessoAoProcesso.NaoEncontrado());
    }
}
