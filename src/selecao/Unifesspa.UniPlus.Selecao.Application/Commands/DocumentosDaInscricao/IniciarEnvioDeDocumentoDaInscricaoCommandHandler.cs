namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Handler de <see cref="IniciarEnvioDeDocumentoDaInscricaoCommand"/>: confere o dono, o período e a
/// exigência na árvore congelada da versão da inscrição, cria o arquivo pendente e devolve a URL
/// pré-assinada de PUT, de validade curta (<see cref="PrazosDoArquivoEnviado.Envio"/>).
/// </summary>
public static class IniciarEnvioDeDocumentoDaInscricaoCommandHandler
{
    public static async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> Handle(
        IniciarEnvioDeDocumentoDaInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IDocumentoDaInscricaoRepository documentoRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> acesso = await AcessoAoDocumentoDaInscricao
            .ResolverInscricaoAsync(
                command.ProcessoSeletivoId, userContext, inscricaoRepository, processoSeletivoRepository,
                certameDivulgadoRepository, registroCodecs, resolvedorFuso, timeProvider, cancellationToken)
            .ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(acesso.Error!);
        }

        Inscricao inscricao = acesso.Value!;

        // O rascunho acompanha a versão divulgada: a exigência que o candidato vê é a dela.
        Result adotada = await VersaoDaInscricaoResolver
            .AdotarAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(adotada.Error!);
        }

        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            inscricao.VersaoFormularioId, command.ProcessoSeletivoId, processoSeletivoRepository, registroCodecs,
            inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(contexto.Error!);
        }

        ExigenciaDaInscricao.Localizada? localizada = ExigenciaDaInscricao.Localizar(contexto.Value!.Configuracao.Arvore, command.ExigenciaId);
        if (localizada is null)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(new DomainError(
                DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada, "A exigência não existe na versão desta inscrição."));
        }

        // Só a exigência da fase que coleta a inscrição recebe arquivo; a de outra fase não é do candidato aqui.
        if (!contexto.Value!.Configuracao.Fases.Any(f => f.ColetaInscricao && f.Id == localizada.Exigencia.ExigidoNaFaseId))
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(new DomainError(
                DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada, "A exigência não é da fase de inscrição desta versão."));
        }

        if (OcorrenciaInvalida(inscricao, localizada.GrupoRepetido, command.OcorrenciaId) is { } ocorrenciaInvalida)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(ocorrenciaInvalida);
        }

        Result<ApresentacaoDeDocumento> apresentacao = await ResolverApresentacaoAsync(command, inscricao, documentoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (apresentacao.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(apresentacao.Error!);
        }

        Result<ArquivoDaApresentacao> pendente = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Value!.Id, inscricao.Id, localizada.Exigencia, command.TipoDeMidia, command.TamanhoBytes, timeProvider, PrazosDoArquivoEnviado.Envio);
        if (pendente.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.ValidationFailure(pendente.Errors);
        }

        ArquivoDaApresentacao arquivo = pendente.Value!;

        // A URL sai antes de persistir: se o armazenamento falhar, nada é gravado.
        string urlUpload = await storage
            .GerarUrlUploadAsync(arquivo.ObjectKey, arquivo.TipoDeMidia, PrazosDoArquivoEnviado.Envio, cancellationToken)
            .ConfigureAwait(false);

        if (command.ApresentacaoId is null)
        {
            await documentoRepository.AdicionarApresentacaoAsync(apresentacao.Value!, cancellationToken).ConfigureAwait(false);
        }

        await documentoRepository.AdicionarArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Success(new IniciarEnvioDeDocumentoDaInscricaoDto(
            arquivo.Id, apresentacao.Value!.Id, new Uri(urlUpload, UriKind.Absolute), arquivo.TipoDeMidia, arquivo.ExpiraEm));
    }

    /// <summary>
    /// A exigência que se repete pede a ocorrência de uma das que a inscrição tem no grupo; a que não
    /// se repete não admite ocorrência.
    /// </summary>
    private static DomainError? OcorrenciaInvalida(Inscricao inscricao, string? grupoRepetido, string? ocorrenciaId)
    {
        bool valida = grupoRepetido is null
            ? string.IsNullOrWhiteSpace(ocorrenciaId)
            : inscricao.Grupos.TryGetValue(grupoRepetido, out IReadOnlyList<OcorrenciaDeGrupoInscricao>? ocorrencias)
                && ocorrencias.Any(o => string.Equals(o.Id, ocorrenciaId, StringComparison.Ordinal));
        return valida
            ? null
            : new DomainError(
                DocumentoDaInscricaoErrorCodes.OcorrenciaInvalida,
                grupoRepetido is null
                    ? "Esta exigência não se repete: não informe a ocorrência."
                    : "Esta exigência vale por ocorrência: informe uma ocorrência que a inscrição tenha.");
    }

    /// <summary>
    /// Uma apresentação só é reaproveitada quando a requisição traz o id dela e ela é desta inscrição,
    /// desta exigência e desta ocorrência; sem o id, abre-se uma nova — a frente e o verso que o cliente
    /// não junta não escolhem uma apresentação por conta própria.
    /// </summary>
    private static async Task<Result<ApresentacaoDeDocumento>> ResolverApresentacaoAsync(
        IniciarEnvioDeDocumentoDaInscricaoCommand command,
        Inscricao inscricao,
        IDocumentoDaInscricaoRepository documentoRepository,
        CancellationToken cancellationToken)
    {
        if (command.ApresentacaoId is not { } id)
        {
            return Result<ApresentacaoDeDocumento>.Success(
                ApresentacaoDeDocumento.Criar(inscricao.Id, command.ExigenciaId, string.IsNullOrWhiteSpace(command.OcorrenciaId) ? null : command.OcorrenciaId));
        }

        ApresentacaoDeDocumento? existente = await documentoRepository.ObterApresentacaoAsync(id, cancellationToken).ConfigureAwait(false);
        return existente is not null
            && existente.InscricaoId == inscricao.Id
            && existente.ExigenciaId == command.ExigenciaId
            && string.Equals(existente.OcorrenciaId, string.IsNullOrWhiteSpace(command.OcorrenciaId) ? null : command.OcorrenciaId, StringComparison.Ordinal)
                ? Result<ApresentacaoDeDocumento>.Success(existente)
                : Result<ApresentacaoDeDocumento>.Failure(new DomainError(
                    DocumentoDaInscricaoErrorCodes.ApresentacaoNaoEncontrada, "Apresentação não encontrada para esta exigência."));
    }
}
