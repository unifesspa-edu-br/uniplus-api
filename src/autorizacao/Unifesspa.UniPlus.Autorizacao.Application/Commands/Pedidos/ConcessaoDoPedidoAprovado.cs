namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A concessão que nasce da última aprovação exigida do pedido, sem gravar: quem chama grava, junto do pedido. A concessão
/// nova se constrói e valida antes de qualquer mudança: se ela falhar, nada fica para gravar.
/// </summary>
public sealed class ConcessaoDoPedidoAprovado
{
    private readonly IConcessaoRepository _concessoes;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public ConcessaoDoPedidoAprovado(IConcessaoRepository concessoes, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(catalogo);

        _concessoes = concessoes;
        _catalogo = catalogo;
    }

    internal async Task<Result> ConcederAsync(DecisaoDaEtapa decisao, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        PedidoDeConcessao pedido = decisao.Pedido;
        Concessao? igual = await _concessoes
            .ObterNaoRevogadaIgualAsync(pedido.Beneficiario!, decisao.Perfil.Id, pedido.Escopo, cancellationToken)
            .ConfigureAwait(false);
        return igual?.EstaAtiva(agora) == true
            ? Result.Failure(PedidoHandlerErros.JaTemConcessao())
            : await RegistrarAsync(decisao, igual, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> RegistrarAsync(DecisaoDaEtapa decisao, Concessao? igual, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result<Concessao> concessao = Construir(decisao, agora);
        Result renovada = concessao.IsFailure ? Result.ValidationFailure(concessao.Errors) : RevogarVencida(igual, decisao, agora);
        if (renovada.IsSuccess)
        {
            decisao.Pedido.RegistrarConcessao(concessao.Value!.Id);
            await _concessoes.AdicionarAsync(concessao.Value, cancellationToken).ConfigureAwait(false);
        }

        return renovada;
    }

    private Result<Concessao> Construir(DecisaoDaEtapa decisao, DateTimeOffset agora)
    {
        PedidoDeConcessao pedido = decisao.Pedido;

        return Concessao.Conceder(
            pedido.Beneficiario!, decisao.Perfil, pedido.Escopo, new OrigemDaConcessao(TipoDeOrigem.Pedido, pedido.Id), decisao.Decisor, agora, _catalogo,
            validaAte: pedido.ValidaAte, beneficiarioEmail: pedido.BeneficiarioEmail);
    }

    // A vencida não se encerra sozinha e, enquanto não revogada, o índice único recusa a nova. A revogação e a concessão
    // vão na mesma gravação, que é atômica: a falha de uma não deixa a outra, nem o pedido aprovado, e o EF emite a
    // revogação antes da inserção.
    private static Result RevogarVencida(Concessao? igual, DecisaoDaEtapa decisao, DateTimeOffset agora) =>
        igual is null ? Result.Success() : igual.Revogar(decisao.Decisor, $"Renovação pelo pedido {decisao.Pedido.Id}.", agora);
}
