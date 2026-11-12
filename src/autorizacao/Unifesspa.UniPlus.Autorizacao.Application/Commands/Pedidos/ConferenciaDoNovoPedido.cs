namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O pedido recém-criado e o perfil em que ele nasceu, prontos para gravar.</summary>
public sealed record NovoPedido(PedidoDeConcessao Pedido, Perfil Perfil);

/// <summary>
/// O que a criação do pedido confere antes de gravar: o perfil que se pode pedir, as regras do agregado, a unidade do
/// escopo e, no pedido para si mesmo, a incompatibilidade dos papéis de governança. As violações de campo vencem a consulta
/// da unidade: quem erra o campo não espera a leitura.
/// </summary>
public sealed class ConferenciaDoNovoPedido
{
    private readonly PerfilParaPedir _perfis;
    private readonly ILeitorDeUnidade _unidades;
    private readonly IDominiosInstitucionais _dominios;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly IIncompatibilidadeDosPapeis _papeis;
    private readonly TimeProvider _clock;

    public ConferenciaDoNovoPedido(
        PerfilParaPedir perfis,
        ILeitorDeUnidade unidades,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        IIncompatibilidadeDosPapeis papeis,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(clock);

        _perfis = perfis;
        _unidades = unidades;
        _dominios = dominios;
        _catalogo = catalogo;
        _papeis = papeis;
        _clock = clock;
    }

    /// <summary>
    /// Cria o pedido sem gravar, ou devolve a recusa. A leitura do perfil o trava com <c>FOR SHARE</c> até o fim da
    /// transação: a troca de vigência, que o trava para escrita, espera ou conflita, e o pedido nasce sempre na versão que
    /// de fato está em vigor.
    /// </summary>
    internal async Task<Result<NovoPedido>> ConferirAsync(
        CriarPedidoDeConcessaoCommand command, PessoaNoToken solicitante, CancellationToken cancellationToken)
    {
        Result<Perfil> lido = await _perfis.ObterAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);
        if (lido.IsFailure)
        {
            return Result<NovoPedido>.Failure(lido.Error!);
        }

        DateTimeOffset agora = _clock.GetUtcNow();
        Result<PedidoDeConcessao> criado = PedidoDeConcessao.Criar(
            solicitante, command.BeneficiarioEmail, lido.Value!, Escopo(command.UnidadeId), command.Justificativa, _dominios, _catalogo, agora, command.ValidaAteSugerida);
        DomainError? recusa = criado.IsFailure ? null : await RecusaAsync(command.UnidadeId, criado.Value!, agora, cancellationToken).ConfigureAwait(false);

        return criado.IsFailure ? Result<NovoPedido>.ValidationFailure(criado.Errors)
            : recusa is not null ? Result<NovoPedido>.Failure(recusa)
            : Result<NovoPedido>.Success(new NovoPedido(criado.Value!, lido.Value!));
    }

    private static EscopoDaConcessao Escopo(Guid? unidadeId) =>
        unidadeId is { } unidade ? EscopoDaConcessao.DaUnidade(unidade) : EscopoDaConcessao.DaInstituicao();

    // Só o pedido para si mesmo já conhece o beneficiário; o pedido para outra pessoa confere no aceite. A conferência
    // definitiva é a da aprovação, sob a trava da pessoa: aqui não se trava, porque a criação não concede.
    private async Task<DomainError?> RecusaAsync(Guid? unidadeId, PedidoDeConcessao pedido, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (unidadeId is { } unidade && await _unidades.ObterPorIdAsync(unidade, cancellationToken).ConfigureAwait(false) is null)
        {
            return PedidoHandlerErros.UnidadeInexistente(unidade);
        }

        return pedido.Beneficiario is { } beneficiario
            && await _papeis.TemPapelIncompativelAsync(beneficiario, pedido.PerfilId, agora, cancellationToken).ConfigureAwait(false)
            ? PedidoHandlerErros.PapeisIncompativeis()
            : null;
    }
}
