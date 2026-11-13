namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Registra a manifestação técnica num item com aprovação solicitada e atualiza o circuito do item na mesma transação
/// (ADR-0163). A manifestação não decide: não muda a situação da aprovação nem a proteção do item (ADR-0162).
/// </summary>
public sealed class ManifestacaoNaProtecaoDoCatalogo
{
    private readonly ItensSobAprovacao _itens;
    private readonly IManifestacaoDaProtecaoRepository _manifestacoes;
    private readonly IConfiguracaoUnitOfWork _unitOfWork;
    private readonly TimeProvider _relogio;

    public ManifestacaoNaProtecaoDoCatalogo(
        ItensSobAprovacao itens, IManifestacaoDaProtecaoRepository manifestacoes, IConfiguracaoUnitOfWork unitOfWork, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(manifestacoes);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(relogio);

        _itens = itens;
        _manifestacoes = manifestacoes;
        _unitOfWork = unitOfWork;
        _relogio = relogio;
    }

    /// <summary>
    /// Registra e grava. Falha com o não encontrado do cadastro, <c>CicloMudou</c>, <c>ItemForaDeSolicitacao</c> ou o
    /// conflito de concorrência.
    /// </summary>
    public async Task<Result> RegistrarAsync(PedidoDeManifestacao pedido, AtorDoAto autor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        // O item é lido antes das manifestações: uma manifestação confirmada depois desta leitura muda a linha do item,
        // e a gravação desta perde por conflito em vez de deixar escapar a outra.
        Result<IItemSobAprovacao> carregado = await _itens.ObterAsync(pedido.Item, cancellationToken).ConfigureAwait(false);
        if (carregado.IsFailure)
        {
            return Result.Failure(carregado.Error!);
        }

        Result<ManifestacaoDaProtecaoDoCatalogo> registrada = await RegistrarNoCicloAsync(carregado.Value!.Aprovacao, pedido, autor, cancellationToken).ConfigureAwait(false);
        return registrada.IsFailure
            ? Result.Failure(registrada.Error!)
            : await GravacaoDoCircuitoDaProtecao.SalvarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ManifestacaoDaProtecaoDoCatalogo>> RegistrarNoCicloAsync(
        AprovacaoDaProtecao circuito, PedidoDeManifestacao pedido, AtorDoAto autor, CancellationToken cancellationToken)
    {
        IReadOnlyList<ManifestacaoDaProtecaoDoCatalogo> eventos = await _manifestacoes
            .ListarDoCicloAsync(pedido.Item, circuito.Ciclo, cancellationToken).ConfigureAwait(false);
        Result<ManifestacaoDaProtecaoDoCatalogo> registrada = ManifestacoesDoCiclo.De(pedido.Item, circuito.Ciclo, eventos)
            .Registrar(circuito, pedido.Ciclo, pedido.Conteudo, autor, _relogio.GetUtcNow());
        if (registrada.IsSuccess)
        {
            _manifestacoes.Adicionar(registrada.Value!);
        }

        return registrada;
    }
}
