namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>A versão que o certame serve e o ato da divulgação que a aponta.</summary>
public sealed record VersaoDivulgada(VersaoConfiguracao Versao, Guid AtoDaDivulgacao);

/// <summary>
/// A versão que o certame divulgado serve: a divulgação aponta o ato que confirmou a publicidade, e
/// é a versão que ele congelou — não a mais nova, que sob retificação pendente ainda não tem
/// publicidade nenhuma. Nunca o relógio.
/// </summary>
public static class VersaoDivulgadaResolver
{
    public static async Task<Result<VersaoDivulgada>> ResolverAsync(
        Guid processoSeletivoId,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (divulgado is null)
        {
            return Result<VersaoDivulgada>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {processoSeletivoId} não encontrado."));
        }

        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync([divulgado.AtoCriadorId], cancellationToken)
            .ConfigureAwait(false);

        // Divulgação sem a versão que ela aponta é corrupção, não ausência: a linha só nasce junto
        // da versão.
        return versoes.Count == 0
            ? Result<VersaoDivulgada>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A divulgação do processo {processoSeletivoId} aponta uma versão de configuração que não existe."))
            : Result<VersaoDivulgada>.Success(new VersaoDivulgada(versoes[0], divulgado.AtoCriadorId));
    }
}
