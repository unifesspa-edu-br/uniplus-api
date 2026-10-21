namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// O que os três passos do envio de documento da inscrição conferem antes de tudo: a inscrição ativa
/// é a do dono do token e as inscrições estão abertas. Quem não tem inscrição no processo recebe a
/// mesma recusa de quem pede um arquivo que não é dele: nada revela dado de outra pessoa.
/// </summary>
public static class AcessoAoDocumentoDaInscricao
{
    public static async Task<Result<Inscricao>> ResolverInscricaoAsync(
        Guid processoSeletivoId,
        IUserContext userContext,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<Inscricao>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode enviar documentos."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);
        if (cpf.IsFailure)
        {
            return Result<Inscricao>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(processoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<Inscricao>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<Inscricao>.Failure(erroDono);
        }

        DomainError? foraDoPeriodo = await PeriodoDaInscricaoResolver.ConferirAsync(
            processoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return foraDoPeriodo is not null ? Result<Inscricao>.Failure(foraDoPeriodo) : Result<Inscricao>.Success(inscricao);
    }

    /// <summary>
    /// O arquivo da inscrição, ou a recusa neutra: o arquivo inexistente e o de outra inscrição são
    /// indistinguíveis.
    /// </summary>
    public static async Task<Result<ArquivoDaApresentacao>> ResolverArquivoAsync(
        Guid arquivoId, Inscricao inscricao, IDocumentoDaInscricaoRepository repositorio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(repositorio);

        ArquivoDaApresentacao? arquivo = await repositorio.ObterArquivoAsync(arquivoId, cancellationToken).ConfigureAwait(false);
        return arquivo is null || arquivo.InscricaoId != inscricao.Id
            ? Result<ArquivoDaApresentacao>.Failure(ArquivoNaoEncontrado)
            : Result<ArquivoDaApresentacao>.Success(arquivo);
    }

    public static DomainError ArquivoNaoEncontrado { get; } = new(
        DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado, "Arquivo não encontrado nesta inscrição.");

    /// <summary>A inscrição mudou entre a leitura e a gravação (envio, cancelamento ou outro arquivo em curso).</summary>
    public static DomainError InscricaoAlterada { get; } = new(
        DocumentoDaInscricaoErrorCodes.InscricaoAlterada,
        "A inscrição mudou enquanto o arquivo era processado. Consulte a inscrição e tente de novo.");
}
