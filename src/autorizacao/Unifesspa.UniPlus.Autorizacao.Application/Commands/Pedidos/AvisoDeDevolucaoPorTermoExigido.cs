namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O aviso do pedido devolvido na aprovação do gestor porque o perfil passou a exigir o termo de responsabilidade depois
/// da criação: vai ao solicitante, que precisa refazer o pedido. O fato é o próprio pedido, devolvido uma vez só; o
/// registro dos avisos descarta a repetição, e o envio sai pelo outbox da mesma transação da devolução.
/// </summary>
public sealed class AvisoDeDevolucaoPorTermoExigido
{
    private readonly EmissorDeAvisos _emissor;
    private readonly ILinkDoPedido _link;

    public AvisoDeDevolucaoPorTermoExigido(EmissorDeAvisos emissor, ILinkDoPedido link)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(link);

        _emissor = emissor;
        _link = link;
    }

    public Task<IReadOnlyList<EnviarEmail>> AvisarAsync(PedidoDeConcessao pedido, string perfilNome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        ModeloDeAviso modelo = new(TipoDeAviso.PedidoDevolvidoPorTermoExigido, perfilNome, null, [], _link.Para(pedido.Id));
        DestinatarioDoAviso[] solicitante = [new(pedido.Solicitante, pedido.SolicitanteEmail ?? string.Empty)];

        return _emissor.EmitirAsync(
            new PedidoDeAviso(modelo, pedido.Id, ResultadoDaDecisaoCodigo.TermoExigido, solicitante, []), cancellationToken);
    }
}
