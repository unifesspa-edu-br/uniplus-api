namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Aplica o veredicto ao anexo do pedido. O anexo verificado libera o pedido que o esperava, e o aviso à próxima pessoa
/// sai pelo outbox na mesma gravação.
/// </summary>
public sealed partial class ReceptorDoAnexoDoPedido : IReceptorDoResultadoDaVerificacao
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IPerfilRepository _perfis;
    private readonly AvisosDoPedido _avisos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReceptorDoAnexoDoPedido> _logger;

    public ReceptorDoAnexoDoPedido(
        IPedidoDeConcessaoRepository pedidos,
        IPerfilRepository perfis,
        AvisosDoPedido avisos,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger<ReceptorDoAnexoDoPedido> logger)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _pedidos = pedidos;
        _perfis = perfis;
        _avisos = avisos;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DesfechoDoRecebimento> ReceberAsync(ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        AnexoDoPedido? anexo = await _pedidos.ObterAnexoAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);

        return anexo is null
            ? DesfechoDoRecebimento.NaoReconhecido
            : await TratarAsync(anexo, resultado, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DesfechoDoRecebimento> TratarAsync(
        AnexoDoPedido anexo, ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken)
    {
        SituacaoDoAnexo situacao = anexo.Situacao;
        if (!Registrar(anexo, resultado))
        {
            LogAnexoIgnorado(_logger, resultado.ArquivoId, situacao);
            return DesfechoDoRecebimento.Tratado();
        }

        IReadOnlyList<EnviarEmail> mensagens = resultado.Veredicto == VeredictoDaVerificacao.Verificado
            ? await LiberarPedidoAsync(anexo.PedidoId, cancellationToken).ConfigureAwait(false)
            : [];
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return DesfechoDoRecebimento.Tratado(mensagens);
    }

    private bool Registrar(AnexoDoPedido anexo, ResultadoDaVerificacaoDeArquivo resultado) =>
        resultado.Veredicto == VeredictoDaVerificacao.Verificado
            ? anexo.RegistrarVerificado(resultado.HashSha256!, _clock.GetUtcNow())
            : anexo.RegistrarRecusado(_clock.GetUtcNow());

    private async Task<IReadOnlyList<EnviarEmail>> LiberarPedidoAsync(Guid pedidoId, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await _pedidos.ObterParaAlterarAsync(pedidoId, cancellationToken).ConfigureAwait(false);
        if (pedido is null || !pedido.LiberarAoVerificarDocumento())
        {
            return [];
        }

        _pedidos.Atualizar(pedido);
        Perfil? perfil = await _perfis.ObterPorIdAsync(pedido.PerfilId, cancellationToken).ConfigureAwait(false);

        return await _avisos
            .AvisarAsync(pedido, perfil?.Nome ?? string.Empty, SituacaoDoPedido.AguardandoDocumento, cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do anexo do pedido {AnexoId} ignorado: o anexo está {Situacao}")]
    private static partial void LogAnexoIgnorado(ILogger logger, Guid anexoId, SituacaoDoAnexo situacao);
}
