namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Reúne o que o comprovante precisa fora do próprio comprovante, a versão fixada da inscrição, o nome do processo
/// e o da unidade administradora (cópias congeladas na criação), o prazo da correção e o cancelamento, e o monta. A versão fixada é a que o primeiro envio congelou: o texto do documento não acompanha a
/// retificação do edital.
/// </summary>
public sealed class MontadorDoComprovante
{
    private readonly IProcessoSeletivoRepository _processoSeletivoRepository;
    private readonly IRegistroCodecsEnvelope _registroCodecs;
    private readonly PrazoDaCorrecaoDoComprovante _prazoDaCorrecao;
    private readonly CancelamentoDoComprovante _cancelamento;

    public MontadorDoComprovante(
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        PrazoDaCorrecaoDoComprovante prazoDaCorrecao,
        CancelamentoDoComprovante cancelamento)
    {
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(prazoDaCorrecao);
        ArgumentNullException.ThrowIfNull(cancelamento);

        _processoSeletivoRepository = processoSeletivoRepository;
        _registroCodecs = registroCodecs;
        _prazoDaCorrecao = prazoDaCorrecao;
        _cancelamento = cancelamento;
    }

    public async Task<Result<ComprovanteDaInscricaoDto>> MontarAsync(
        Inscricao inscricao, ComprovanteNaPosicao posicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(posicao);

        Result<EnvelopeReidratado> envelope = await EnvelopeFixadoAsync(inscricao, cancellationToken).ConfigureAwait(false);
        Result<CancelamentoNoComprovanteDto?> cancelamento = _cancelamento.Ler(inscricao);
        if (envelope.IsFailure || cancelamento.IsFailure)
        {
            return Result<ComprovanteDaInscricaoDto>.Failure((envelope.Error ?? cancelamento.Error)!);
        }

        return Result<ComprovanteDaInscricaoDto>.Success(ComprovanteDoTitular.Montar(
            await InsumosAsync(inscricao, posicao, envelope.Value!, cancelamento.Value, cancellationToken).ConfigureAwait(false)));
    }

    private async Task<InsumosDoComprovante> InsumosAsync(
        Inscricao inscricao, ComprovanteNaPosicao posicao, EnvelopeReidratado envelope, CancelamentoNoComprovanteDto? cancelamento, CancellationToken cancellationToken)
    {
        string nome = await _processoSeletivoRepository.ObterNomeAsync(inscricao.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        string unidade = await _processoSeletivoRepository.ObterNomeDaUnidadeAdministradoraAsync(inscricao.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        PrazoDaCorrecaoDto? prazo = await _prazoDaCorrecao.LerAsync(inscricao, posicao.Vale, cancellationToken).ConfigureAwait(false);

        return new InsumosDoComprovante(inscricao, posicao, envelope, nome, unidade, prazo, cancelamento);
    }

    private async Task<Result<EnvelopeReidratado>> EnvelopeFixadoAsync(Inscricao inscricao, CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> versao = await VersaoFixadaDaInscricaoResolver
            .ResolverAsync(inscricao.VersaoFormularioId, inscricao.ProcessoSeletivoId, _processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);

        return versao.IsFailure
            ? Result<EnvelopeReidratado>.Failure(versao.Error!)
            : ContextoDaVersaoFixadaResolver.Reidratar(versao.Value!, _registroCodecs);
    }
}
