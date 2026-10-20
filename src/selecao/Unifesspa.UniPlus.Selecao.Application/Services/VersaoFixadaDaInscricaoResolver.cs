namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve a <see cref="VersaoConfiguracao"/> pelo id exato que a inscrição guarda em
/// <c>Inscricao.VersaoFormularioId</c> (UNI-REQ-0084), nunca pela divulgação atual: a inscrição enviada
/// não muda de versão com uma retificação divulgada depois. Quem decide qual versão vale para a inscrição
/// é <see cref="VersaoDaInscricaoResolver"/>. Não há "obter versão por id" dedicado no repositório — a
/// lista inteira de versões do processo é pequena (uma por publicação/retificação) e já vem pronta via
/// <see cref="IProcessoSeletivoRepository.ObterVersoesAsync"/>.
/// </summary>
public static class VersaoFixadaDaInscricaoResolver
{
    public static async Task<Result<VersaoConfiguracao>> ResolverAsync(
        Guid versaoFormularioId,
        Guid processoSeletivoId,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        VersaoConfiguracao? versao = versoes.FirstOrDefault(v => v.Id == versaoFormularioId);
        if (versao is null)
        {
            return Result<VersaoConfiguracao>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A inscrição está na versão {versaoFormularioId}, que não existe mais entre as versões do processo {processoSeletivoId}."));
        }

        return Result<VersaoConfiguracao>.Success(versao);
    }
}
