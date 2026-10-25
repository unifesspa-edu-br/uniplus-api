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
/// O que os passos do documento da inscrição conferem antes de tudo. No envio: a inscrição ativa é a do dono
/// do token e as inscrições estão abertas. Na leitura do que já foi enviado: o arquivo é do dono do token. Quem não tem inscrição no processo recebe a
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
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> doDono = await ResolverInscricaoDoDonoAsync(processoSeletivoId, userContext, inscricaoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (doDono.IsFailure)
        {
            return doDono;
        }

        DomainError? foraDoPeriodo = await PeriodoDaInscricaoResolver.ConferirAsync(
            processoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return foraDoPeriodo is not null ? Result<Inscricao>.Failure(foraDoPeriodo) : doDono;
    }

    private static async Task<Result<Inscricao>> ResolverInscricaoDoDonoAsync(
        Guid processoSeletivoId,
        IUserContext userContext,
        IInscricaoRepository inscricaoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);

        Result<Cpf> cpf = CpfDoToken(userContext);
        if (cpf.IsFailure)
        {
            return Result<Inscricao>.Failure(cpf.Error!);
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(processoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<Inscricao>.Failure(Inscricao.NaoEncontrada);
        }

        return inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono
            ? Result<Inscricao>.Failure(erroDono)
            : Result<Inscricao>.Success(inscricao);
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

    /// <summary>
    /// O arquivo do dono do token, em qualquer situação da inscrição: ler o que já foi enviado não depende do
    /// período nem de a inscrição estar ativa — o candidato que cancelou continua podendo baixar o que enviou.
    /// O dono é o <c>sub</c> e o CPF do token juntos, como nas demais leituras. O arquivo inexistente, o de outro
    /// processo e o de outra pessoa recebem a mesma recusa.
    /// </summary>
    public static async Task<Result<ArquivoDaApresentacao>> ResolverArquivoDoDonoAsync(
        Guid processoSeletivoId,
        Guid arquivoId,
        IUserContext userContext,
        IInscricaoRepository inscricaoRepository,
        IDocumentoDaInscricaoRepository documentoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);

        Result<Cpf> cpf = CpfDoToken(userContext);
        if (cpf.IsFailure)
        {
            return Result<ArquivoDaApresentacao>.Failure(cpf.Error!);
        }

        ArquivoDaApresentacao? arquivo = await documentoRepository.ObterArquivoAsync(arquivoId, cancellationToken).ConfigureAwait(false);
        Inscricao? inscricao = arquivo is null
            ? null
            : await inscricaoRepository.ObterPorIdAsync(arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);

        return arquivo is not null
            && inscricao is not null
            && inscricao.ProcessoSeletivoId == processoSeletivoId
            && inscricao.Cpf == cpf.Value
            && inscricao.ConferirDono(userContext.UserId) is null
                ? Result<ArquivoDaApresentacao>.Success(arquivo)
                : Result<ArquivoDaApresentacao>.Failure(ArquivoNaoEncontrado);
    }

    private static Result<Cpf> CpfDoToken(IUserContext userContext)
    {
        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<Cpf>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF acessa os documentos da inscrição."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);
        return cpf.IsFailure
            ? Result<Cpf>.Failure(new DomainError("Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."))
            : cpf;
    }

    public static DomainError ArquivoNaoEncontrado { get; } = new(
        DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado, "Arquivo não encontrado nesta inscrição.");

    /// <summary>A inscrição mudou entre a leitura e a gravação (envio, cancelamento ou outro arquivo em curso).</summary>
    public static DomainError InscricaoAlterada { get; } = new(
        DocumentoDaInscricaoErrorCodes.InscricaoAlterada,
        "A inscrição mudou enquanto o arquivo era processado. Consulte a inscrição e tente de novo.");
}
