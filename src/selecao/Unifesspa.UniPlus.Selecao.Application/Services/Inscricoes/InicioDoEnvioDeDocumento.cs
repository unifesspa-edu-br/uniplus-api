namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Abstractions;

using Commands.DocumentosDaInscricao;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

/// <summary>Prepara a apresentação e o arquivo pendente na versão divulgada e obtém a URL de envio.</summary>
public sealed class InicioDoEnvioDeDocumento(
    VersaoDaInscricaoResolver versao, ContextoDaVersaoFixadaResolver contextos,
    IInscricaoRepository inscricaoRepository, IDocumentoDaInscricaoRepository documentoRepository,
    IArquivoDoCandidatoStorage storage, TimeProvider timeProvider)
{
    public async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> IniciarAsync(
        Inscricao inscricao, IniciarEnvioDeDocumentoDaInscricaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(command);
        int revisaoLida = inscricao.Revisao;
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        if (adotada.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(adotada.Error!);
        }
        return await AvancarRevisaoAsync(inscricao, revisaoLida, cancellationToken).ConfigureAwait(false)
            ? await ResolverExigenciaAsync(inscricao, command, cancellationToken).ConfigureAwait(false)
            : Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(AcessoAoDocumentoDaInscricao.InscricaoAlterada);
    }

    private async Task<bool> AvancarRevisaoAsync(Inscricao inscricao, int revisaoLida, CancellationToken cancellationToken)
    {
        if (inscricao.Revisao != revisaoLida)
        {
            await inscricaoRepository.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
            return true;
        }
        return await inscricaoRepository.TentarAvancarRevisaoAsync(inscricao.Id, revisaoLida, apenasRascunho: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> ResolverExigenciaAsync(
        Inscricao inscricao, IniciarEnvioDeDocumentoDaInscricaoCommand command, CancellationToken cancellationToken)
    {
        Result<ContextoDaVersaoFixada> contexto = await contextos.ResolverAsync(inscricao.Formulario.VersaoFormularioId, command.ProcessoSeletivoId,
            inscricao.Formulario.Preenchimento.Opcoes.Primeira, inscricao.Formulario.Preenchimento.Opcoes.Segunda, cancellationToken).ConfigureAwait(false);
        if (contexto.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(contexto.Error!);
        }
        Result<ExigenciaDaInscricao.Localizada> exigencia = ConferirExigencia(inscricao, command, contexto.Value!);
        return exigencia.IsFailure
            ? Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(exigencia.Error!)
            : await IniciarArquivoAsync(inscricao, command, exigencia.Value!.Exigencia, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ExigenciaDaInscricao.Localizada> ConferirExigencia(
        Inscricao inscricao, IniciarEnvioDeDocumentoDaInscricaoCommand command, ContextoDaVersaoFixada contexto)
    {
        ExigenciaDaInscricao.Localizada? localizada = ExigenciaDaInscricao.Localizar(contexto.Configuracao.RegrasDeComprovacao, command.ExigenciaId);
        if (localizada is null)
        {
            return Result<ExigenciaDaInscricao.Localizada>.Failure(new DomainError(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada, "A exigência não existe na versão desta inscrição."));
        }
        if (!contexto.Configuracao.Fases.Any(f => f.ColetaInscricao && f.Id == localizada.Exigencia.ExigidoNaFaseId))
        {
            return Result<ExigenciaDaInscricao.Localizada>.Failure(new DomainError(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada, "A exigência não é da fase de inscrição desta versão."));
        }
        return OcorrenciaInvalida(inscricao, localizada.GrupoRepetido, command.OcorrenciaId) is { } recusa
            ? Result<ExigenciaDaInscricao.Localizada>.Failure(recusa)
            : Result<ExigenciaDaInscricao.Localizada>.Success(localizada);
    }

    private async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> IniciarArquivoAsync(
        Inscricao inscricao, IniciarEnvioDeDocumentoDaInscricaoCommand command, DocumentoExigido exigencia, CancellationToken cancellationToken)
    {
        Result<ApresentacaoDeDocumento> apresentacao = await ResolverApresentacaoAsync(command, inscricao, exigencia, cancellationToken).ConfigureAwait(false);
        if (apresentacao.IsFailure)
        {
            return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Failure(apresentacao.Error!);
        }
        Result<ArquivoDaApresentacao> arquivo = ArquivoDaApresentacao.IniciarPendente(apresentacao.Value!.Id, inscricao.Id, exigencia,
            command.TipoDeMidia, command.TamanhoBytes, timeProvider, PrazosDoArquivoEnviado.Envio);
        return arquivo.IsFailure
            ? Result<IniciarEnvioDeDocumentoDaInscricaoDto>.ValidationFailure(arquivo.Errors)
            : await PrepararGravacaoAsync(arquivo.Value!, apresentacao.Value!, command.ApresentacaoId is null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> PrepararGravacaoAsync(
        ArquivoDaApresentacao arquivo, ApresentacaoDeDocumento apresentacao, bool apresentacaoNova, CancellationToken cancellationToken)
    {
        string url = await storage.GerarUrlUploadAsync(arquivo.ObjectKey, arquivo.TipoDeMidia, PrazosDoArquivoEnviado.Envio, cancellationToken).ConfigureAwait(false);
        if (apresentacaoNova)
        {
            await documentoRepository.AdicionarApresentacaoAsync(apresentacao, cancellationToken).ConfigureAwait(false);
        }
        await documentoRepository.AdicionarArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
        return Result<IniciarEnvioDeDocumentoDaInscricaoDto>.Success(new IniciarEnvioDeDocumentoDaInscricaoDto(
            arquivo.Id, apresentacao.Id, new Uri(url, UriKind.Absolute), arquivo.TipoDeMidia, arquivo.ExpiraEm));
    }

    /// <summary>
    /// A exigência que se repete pede a ocorrência de uma das que a inscrição tem no grupo; a que não
    /// se repete não admite ocorrência.
    /// </summary>
    private static DomainError? OcorrenciaInvalida(Inscricao inscricao, string? grupoRepetido, string? ocorrenciaId)
    {
        bool valida = grupoRepetido is null
            ? string.IsNullOrWhiteSpace(ocorrenciaId)
            : inscricao.Formulario.Preenchimento.Grupos.TryGetValue(grupoRepetido, out IReadOnlyList<OcorrenciaDeGrupoInscricao>? ocorrencias)
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
    private async Task<Result<ApresentacaoDeDocumento>> ResolverApresentacaoAsync(
        IniciarEnvioDeDocumentoDaInscricaoCommand command,
        Inscricao inscricao,
        DocumentoExigido exigencia,
        CancellationToken cancellationToken)
    {
        if (command.ApresentacaoId is not { } id)
        {
            return Result<ApresentacaoDeDocumento>.Success(
                ApresentacaoDeDocumento.Criar(inscricao.Id, exigencia, string.IsNullOrWhiteSpace(command.OcorrenciaId) ? null : command.OcorrenciaId));
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
