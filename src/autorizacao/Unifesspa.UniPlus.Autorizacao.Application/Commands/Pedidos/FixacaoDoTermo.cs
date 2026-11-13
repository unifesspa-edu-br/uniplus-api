namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A fixação do termo de responsabilidade no pedido recém-criado. Só o pedido de perfil protegido lê a vigência, e a lê
/// com <c>FOR SHARE</c> até o fim da transação, depois da trava do perfil e antes da inserção do pedido (ADR-0162): a
/// publicação concorrente espera esta criação, e a criação que chega durante a publicação espera e relê a versão nova.
/// </summary>
public sealed class FixacaoDoTermo
{
    private readonly ITermoDeResponsabilidadeRepository _termos;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public FixacaoDoTermo(ITermoDeResponsabilidadeRepository termos, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(catalogo);

        _termos = termos;
        _catalogo = catalogo;
    }

    internal async Task<Result> FixarAsync(
        PedidoDeConcessao pedido, Perfil perfil, Guid? apresentada, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        bool exigido = PedidoDeConcessao.ExigeTermo(perfil, _catalogo);
        VersaoDoTermo? vigente = exigido
            ? await _termos.ObterVigenteSobTravaCompartilhadaAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return pedido.FixarTermo(new TermoNaCriacao(vigente, apresentada, exigido), agora);
    }
}
