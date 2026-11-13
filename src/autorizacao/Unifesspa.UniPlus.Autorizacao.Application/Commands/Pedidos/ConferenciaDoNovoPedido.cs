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
/// O que a criação do pedido confere antes de gravar: o perfil que se pode pedir, as regras do agregado, o termo de
/// responsabilidade do pedido protegido, a unidade do escopo e, no pedido para si mesmo, a incompatibilidade dos papéis de
/// governança. As violações de campo vencem as consultas: quem erra o campo não espera a leitura.
/// </summary>
public sealed class ConferenciaDoNovoPedido
{
    private readonly PerfilParaPedir _perfis;
    private readonly ILeitorDeUnidade _unidades;
    private readonly IDominiosInstitucionais _dominios;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly IIncompatibilidadeDosPapeis _papeis;
    private readonly FixacaoDoTermo _termo;
    private readonly TimeProvider _clock;

    public ConferenciaDoNovoPedido(
        PerfilParaPedir perfis,
        ILeitorDeUnidade unidades,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        IIncompatibilidadeDosPapeis papeis,
        FixacaoDoTermo termo,
        TimeProvider clock)
    {
        _perfis = perfis ?? throw new ArgumentNullException(nameof(perfis));
        _unidades = unidades ?? throw new ArgumentNullException(nameof(unidades));
        _dominios = dominios ?? throw new ArgumentNullException(nameof(dominios));
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _papeis = papeis ?? throw new ArgumentNullException(nameof(papeis));
        _termo = termo ?? throw new ArgumentNullException(nameof(termo));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Cria o pedido sem gravar, ou devolve a recusa. A leitura do perfil o trava com <c>FOR SHARE</c> até o fim da
    /// transação: a troca de vigência, que o trava para escrita, espera ou conflita, e o pedido nasce sempre na versão que
    /// de fato está em vigor. O termo, quando exigido, é lido em seguida, sob a trava compartilhada da vigência.
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
            new PartesDoPedido(solicitante, command.BeneficiarioEmail), lido.Value!, Escopo(command.UnidadeId), Declaracao(command), _dominios, _catalogo, agora);

        return criado.IsFailure
            ? Result<NovoPedido>.ValidationFailure(criado.Errors)
            : await ConcluirAsync(command, new NovoPedido(criado.Value!, lido.Value!), agora, cancellationToken).ConfigureAwait(false);
    }

    private static EscopoDaConcessao Escopo(Guid? unidadeId) =>
        unidadeId is { } unidade ? EscopoDaConcessao.DaUnidade(unidade) : EscopoDaConcessao.DaInstituicao();

    private static DeclaracaoDoPedido Declaracao(CriarPedidoDeConcessaoCommand command) =>
        new(command.Justificativa, command.ValidaAteSugerida, command.AnexaraDocumento);

    private async Task<Result<NovoPedido>> ConcluirAsync(
        CriarPedidoDeConcessaoCommand command, NovoPedido novo, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result fixado = await _termo.FixarAsync(novo.Pedido, novo.Perfil, command.TermoVersaoApresentadaId, agora, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = fixado.IsFailure ? null : await RecusaAsync(command.UnidadeId, novo.Pedido, agora, cancellationToken).ConfigureAwait(false);

        return fixado.IsFailure ? Result<NovoPedido>.ValidationFailure(fixado.Errors)
            : recusa is not null ? Result<NovoPedido>.Failure(recusa)
            : Result<NovoPedido>.Success(novo);
    }

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
