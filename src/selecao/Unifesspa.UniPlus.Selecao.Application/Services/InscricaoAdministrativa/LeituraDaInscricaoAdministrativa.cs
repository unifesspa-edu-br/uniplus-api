namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As leituras da consulta administrativa da inscrição: o vínculo, que se lê sem decifrar nada; o envelope da versão que
/// o primeiro envio fixou, e não a divulgada depois; e o conteúdo da inscrição, lido só depois de decidido o acesso.
/// </summary>
public sealed class LeituraDaInscricaoAdministrativa
{
    private readonly IProcessoSeletivoRepository _processos;
    private readonly IInscricaoRepository _inscricoes;
    private readonly IDocumentoDaInscricaoRepository _documentos;
    private readonly IRegistroCodecsEnvelope _registroCodecs;

    public LeituraDaInscricaoAdministrativa(
        IProcessoSeletivoRepository processos,
        IInscricaoRepository inscricoes,
        IDocumentoDaInscricaoRepository documentos,
        IRegistroCodecsEnvelope registroCodecs)
    {
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(inscricoes);
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        _processos = processos;
        _inscricoes = inscricoes;
        _documentos = documentos;
        _registroCodecs = registroCodecs;
    }

    /// <summary>Confere o vínculo da inscrição com o processo, sem decidir acesso nem decifrar nada.</summary>
    public Task<ConferenciaDoVinculo> ConferirVinculoAsync(Guid processoId, Guid inscricaoId, CancellationToken cancellationToken) =>
        AcessoAInscricao.ConferirVinculoAsync(_processos, _inscricoes, processoId, inscricaoId, cancellationToken);

    /// <summary>O envelope reidratado da versão fixada pela inscrição; falha quando a versão não existe mais ou não se reidrata.</summary>
    public async Task<Result<EnvelopeReidratado>> EnvelopeFixadoAsync(VinculoDaInscricao vinculo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vinculo);

        Result<VersaoConfiguracao> versao = await VersaoFixadaDaInscricaoResolver
            .ResolverAsync(vinculo.VersaoFormularioId, vinculo.ProcessoSeletivoId, _processos, cancellationToken)
            .ConfigureAwait(false);

        return versao.IsFailure
            ? Result<EnvelopeReidratado>.Failure(versao.Error!)
            : ContextoDaVersaoFixadaResolver.Reidratar(versao.Value!, _registroCodecs);
    }

    /// <summary>
    /// O conteúdo da inscrição; nulo quando ela deixou de existir entre a conferência do vínculo e a leitura. O primeiro envio
    /// só é lido quando a pessoa pode ver a propriedade.
    /// </summary>
    public async Task<ConteudoDaInscricao?> LerAsync(
        Guid inscricaoId, EnvelopeReidratado envelope, ProjecaoDaInscricao projecao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(projecao);

        Inscricao? inscricao = await _inscricoes.ObterPorIdAsync(inscricaoId, cancellationToken).ConfigureAwait(false);

        return inscricao is null
            ? null
            : new ConteudoDaInscricao(
                inscricao,
                envelope,
                await ArquivosAsync(inscricao.Id, envelope.Grafo, cancellationToken).ConfigureAwait(false),
                await PrimeiroEnvioAsync(inscricao.Id, projecao, cancellationToken).ConfigureAwait(false),
                await NumeroDoUltimoComprovanteAsync(inscricao.Id, cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<ArquivoAdministrativoDto>> ArquivosAsync(
        Guid inscricaoId, GrafoConfiguracao grafo, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApresentacaoDeDocumento> apresentacoes = await _documentos
            .ListarApresentacoesDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ArquivoDaApresentacao> arquivos = await _documentos
            .ListarArquivosDaInscricaoAsync(inscricaoId, cancellationToken).ConfigureAwait(false);

        return ArquivosAdministrativos.Montar(apresentacoes, arquivos, grafo);
    }

    private async Task<DateTimeOffset?> PrimeiroEnvioAsync(Guid inscricaoId, ProjecaoDaInscricao projecao, CancellationToken cancellationToken)
    {
        if (!projecao.Permite(nameof(InscricaoAdministrativaDto.SubmetidaEm)))
        {
            return null;
        }

        IReadOnlyDictionary<Guid, DateTimeOffset> envios = await _inscricoes
            .ObterPrimeirosEnviosAsync([inscricaoId], cancellationToken).ConfigureAwait(false);

        return envios.TryGetValue(inscricaoId, out DateTimeOffset primeiro) ? primeiro : null;
    }

    /// <summary>
    /// O número do último comprovante, que é a quantidade emitida, lido só por projeção, sem decifrar nenhum; nulo sem comprovante.
    /// </summary>
    private async Task<int?> NumeroDoUltimoComprovanteAsync(Guid inscricaoId, CancellationToken cancellationToken)
    {
        IReadOnlyList<EmissaoDoComprovante> emissoes = await _inscricoes
            .ObterEmissoesAsync([inscricaoId], cancellationToken).ConfigureAwait(false);

        return ComprovanteNaPosicao.NumeroDoUltimo(emissoes.Count);
    }
}

/// <summary>O que se leu da inscrição depois de decidido o acesso.</summary>
/// <param name="Inscricao">A inscrição com as respostas já decifradas.</param>
/// <param name="Envelope">O envelope da versão fixada pela inscrição, com que as respostas e os arquivos se leem.</param>
/// <param name="Arquivos">Os metadados dos arquivos confirmados, sem o conteúdo.</param>
/// <param name="PrimeiroEnvio">O instante do primeiro envio; nulo quando a pessoa não pode ver a propriedade ou não houve envio.</param>
/// <param name="NumeroDoUltimoComprovante">O número do último comprovante emitido; nulo quando não há nenhum.</param>
public sealed record ConteudoDaInscricao(
    Inscricao Inscricao,
    EnvelopeReidratado Envelope,
    IReadOnlyList<ArquivoAdministrativoDto> Arquivos,
    DateTimeOffset? PrimeiroEnvio,
    int? NumeroDoUltimoComprovante);
