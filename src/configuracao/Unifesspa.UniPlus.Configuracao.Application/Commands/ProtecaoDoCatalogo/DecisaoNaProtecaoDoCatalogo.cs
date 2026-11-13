namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Aplica a decisão do Representante do controlador ao tipo de documento ou ao fato e registra o ato, na mesma
/// transação. A decisão vale sobre a proteção que o Representante do controlador conferiu e enxerga a manifestação desfavorável vigente
/// pela linha do item, a mesma que o token de concorrência protege (ADR-0163).
/// </summary>
public sealed class DecisaoNaProtecaoDoCatalogo
{
    // O item foi editado depois de o Representante do controlador vê-lo: decidir sobre a proteção vigente decidiria sobre o que ele não viu.
    // Repetir a mesma decisão nunca dá certo, então não é o conflito de concorrência que se tenta de novo.
    private static readonly DomainError ProtecaoMudou = new(
        ProtecaoDoCatalogoErrorCodes.ProtecaoMudou,
        "A proteção do item mudou depois de conferida; releia o item e decida sobre a proteção atual.");

    private readonly ItensSobAprovacao _itens;
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos;
    private readonly IConfiguracaoUnitOfWork _unitOfWork;
    private readonly TimeProvider _relogio;

    public DecisaoNaProtecaoDoCatalogo(
        ItensSobAprovacao itens, IAtoDaProtecaoDoCatalogoRepository atos, IConfiguracaoUnitOfWork unitOfWork, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(relogio);

        _itens = itens;
        _atos = atos;
        _unitOfWork = unitOfWork;
        _relogio = relogio;
    }

    /// <summary>
    /// Decide e grava. Falha com o não encontrado do cadastro, <c>ProtecaoMudou</c>, <c>ManifestacaoNaoLida</c>, a
    /// justificativa exigida pela manifestação desfavorável, a recusa da transição do item ou o conflito de concorrência.
    /// </summary>
    public async Task<Result> DecidirAsync(PedidoDeDecisao pedido, AtorDoAto ator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(ator);

        Result<IItemSobAprovacao> carregado = await _itens.ObterAsync(pedido.Item, cancellationToken).ConfigureAwait(false);
        if (carregado.IsFailure)
        {
            return Result.Failure(carregado.Error!);
        }

        Result decidida = Decidir(carregado.Value!, pedido, ator);
        return decidida.IsFailure ? decidida : await GravacaoDoCircuitoDaProtecao.SalvarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // A regra da justificativa é conferida antes da transição, para nada mudar no item quando a decisão é recusada: o
    // outbox grava o contexto depois do handler, e uma entidade mudada iria junto.
    private Result Decidir(IItemSobAprovacao item, PedidoDeDecisao pedido, AtorDoAto ator)
    {
        Result conferida = Conferir(item, pedido);
        if (conferida.IsFailure)
        {
            return conferida;
        }

        DateTimeOffset agora = _relogio.GetUtcNow();
        Result decidida = item.Decidir(pedido.Decisao, ator.Sub, agora);
        if (decidida.IsSuccess)
        {
            _atos.Adicionar(AtoDaProtecaoDoCatalogo.RegistrarDecisao(item.Retrato(), pedido.Decisao, ator, agora));
        }

        return decidida;
    }

    // A decisão vale sobre a proteção e o circuito que o Representante do controlador leu: a proteção editada e a
    // manifestação registrada depois da leitura saem juntas no lote de conflitos, cada recusa no campo que divergiu, para
    // as duas chegarem ao errors[] da resposta (ADR-0125); sem conflito, a justificativa que a manifestação exige e a
    // classificação recomendada saem juntas no lote de recusas, antes de qualquer mudança no item.
    private static Result Conferir(IItemSobAprovacao item, PedidoDeDecisao pedido)
    {
        List<FieldError> conflitos = [.. ProtecaoMudada(item.Protecao, pedido.ProtecaoConferida), .. pedido.Lida.ConferirCom(item.Aprovacao).Errors];
        return conflitos.Count > 0 ? Result.ValidationFailure(conflitos) : pedido.Decisao.Conferir(item.Aprovacao, item.Protecao);
    }

    // Um conflito por eixo da proteção que difere do conferido, no campo do corpo da decisão que o levou.
    private static IEnumerable<FieldError> ProtecaoMudada(ProtecaoDeDados vigente, ProtecaoDeDados conferida)
    {
        (string Campo, bool Mudou)[] eixos =
        [
            ("classificacaoProtecao", vigente.Classificacao != conferida.Classificacao),
            ("hipoteseLegal", vigente.Hipotese != conferida.Hipotese),
            ("finalidadeTratamento", !string.Equals(vigente.Finalidade, conferida.Finalidade, StringComparison.Ordinal)),
        ];
        return eixos.Where(static e => e.Mudou).Select(static e => new FieldError(e.Campo, ProtecaoMudou));
    }
}
