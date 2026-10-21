namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Remove fisicamente o rascunho do dono do token. O rascunho não tem número nem comprovante, e guardar
/// dado pessoal sem finalidade contraria a LGPD, e por isso os arquivos de documento saem também do
/// armazenamento; a inscrição enviada nunca passa por aqui.
/// </summary>
public static class DescartarRascunhoDaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRascunhoDaInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        TimeProvider timeProvider,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        IDocumentoDaInscricaoRepository documentoRepository,
        IArquivoArmazenadoStorage storage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(storage);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode descartar a inscrição."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);
        if (cpf.IsFailure)
        {
            return Result.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(command.ProcessoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result.Failure(erroDono);
        }

        // Com as inscrições encerradas o rascunho não tem ação (AcoesPermitidas): a mesma regra da lista.
        DomainError? foraDoPeriodo = await Services.PeriodoDaInscricaoResolver.ConferirAsync(
            command.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, resolvedorFuso,
            timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (foraDoPeriodo is not null)
        {
            return Result.Failure(foraDoPeriodo);
        }

        // O If-Match que o candidato tem é o do rascunho já levado à versão divulgada, que é o que a
        // leitura devolve; conferir contra o gravado recusaria para sempre o ETag que ele recebeu.
        Result adotada = await Services.VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result.Failure(adotada.Error!);
        }

        if (inscricao.ConferirDescarte(command.Precondicao) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        // Os registros dos arquivos caem em cascata com a inscrição; os objetos do armazenamento, não.
        IReadOnlyList<ArquivoDaApresentacao> arquivos = await documentoRepository
            .ListarArquivosDaInscricaoAsync(inscricao.Id, cancellationToken).ConfigureAwait(false);

        foreach (ArquivoDaApresentacao arquivo in arquivos)
        {
            // A URL de envio ainda vale até ExpiraEm: um PUT nela recria o conteúdo em claro.
            await documentoRepository
                .AgendarLimpezaAsync(arquivo.ObjectKey, arquivo.ExpiraEm + Services.RemocaoDeArquivosPendentesVencidos.Tolerancia, cancellationToken)
                .ConfigureAwait(false);
            await documentoRepository.AgendarLimpezaAsync(arquivo.ChaveSelada, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        await inscricaoRepository.RemoverAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        foreach (ArquivoDaApresentacao arquivo in arquivos)
        {
            await storage.RemoverAsync(arquivo.ObjectKey, cancellationToken).ConfigureAwait(false);
            await storage.RemoverAsync(arquivo.ChaveSelada, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
