namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve a <see cref="VersaoConfiguracao"/> que uma inscrição fixou ao iniciar (UNI-REQ-0084) —
/// pelo id exato, gravado em <c>Inscricao.VersaoFormularioId</c>, nunca pela divulgação atual: uma
/// retificação publicada depois não pode mudar a validação nem a apresentação de uma inscrição já
/// em curso. Não há "obter versão por id" dedicado no repositório — a lista inteira de versões do
/// processo é pequena (uma por publicação/retificação) e já vem pronta via
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
                $"A inscrição fixou a versão {versaoFormularioId}, que não existe mais entre as versões do processo {processoSeletivoId}."));
        }

        return Result<VersaoConfiguracao>.Success(versao);
    }
}
