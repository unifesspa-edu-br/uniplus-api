namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O acesso de quem opera o processo a uma inscrição enviada como resource (ADR-0153), decidido antes de qualquer leitura
/// de conteúdo: a inscrição que não existe, a de outro processo, a em rascunho e a sem a permissão de consultar respondem
/// o mesmo 404, e a recusa por permissão é registrada, com o id do resource quando ele existe.
/// </summary>
public static class AcessoDoOperadorAInscricao
{
    /// <summary>
    /// Decide o acesso à inscrição conferida. A recusa devolve o erro com que a rota responde: o 404 do que não alcança,
    /// ou a identidade incompleta e o impedimento pelo que são. <paramref name="naoEncontrada"/> é o 404 da rota que pede o acesso.
    /// </summary>
    public static async Task<Result<InscricaoAlcancada>> AlcancarAsync(
        IVerificadorDeAcesso verificador,
        ConferenciaDoVinculo conferencia,
        Guid processoId,
        Guid inscricaoId,
        DomainError naoEncontrada,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(naoEncontrada);

        return conferencia is Vinculo conferido
            ? await DecidirAcessoAsync(verificador, conferido, processoId, inscricaoId, naoEncontrada, cancellationToken).ConfigureAwait(false)
            : await RecusarInexistenteAsync(verificador, conferencia, processoId, naoEncontrada, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<InscricaoAlcancada>> DecidirAcessoAsync(
        IVerificadorDeAcesso verificador,
        Vinculo conferido,
        Guid processoId,
        Guid inscricaoId,
        DomainError naoEncontrada,
        CancellationToken cancellationToken)
    {
        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(conferido.UnidadeId, processoId, inscricaoId);
        AcessoNaInscricao acesso = await AcessoAInscricao
            .DecidirAsync(verificador, conferido, PermissaoSelecao.InscricoesConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false);

        return acesso is Recusada recusada
            ? Result<InscricaoAlcancada>.Failure(AcessoAInscricao.Erro(recusada, naoEncontrada))
            : Result<InscricaoAlcancada>.Success(new InscricaoAlcancada(new ProcessoDaInscricaoDto(processoId, conferido.ProcessoNome), conferido.Inscricao, resource));
    }

    private static async Task<Result<InscricaoAlcancada>> RecusarInexistenteAsync(
        IVerificadorDeAcesso verificador,
        ConferenciaDoVinculo conferencia,
        Guid processoId,
        DomainError naoEncontrada,
        CancellationToken cancellationToken)
    {
        await AcessoAInscricao
            .RegistrarNegativaDoInexistenteAsync(verificador, conferencia, processoId, [PermissaoSelecao.InscricoesConsultar.Requisito], cancellationToken)
            .ConfigureAwait(false);

        return Result<InscricaoAlcancada>.Failure(naoEncontrada);
    }
}

/// <summary>A inscrição que a pessoa pode alcançar, com o resource sobre o qual o acesso foi decidido.</summary>
/// <param name="Processo">O processo da rota, com o nome lido na conferência do vínculo.</param>
/// <param name="Vinculo">O vínculo da inscrição com o processo e a versão fixada.</param>
/// <param name="Resource">O resource da inscrição na unidade dona do processo.</param>
public sealed record InscricaoAlcancada(ProcessoDaInscricaoDto Processo, VinculoDaInscricao Vinculo, ResourceContext Resource);
