namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O aviso do pedido que a entrada em vigor de uma versão com acréscimo devolveu: vai ao solicitante e ao beneficiário.
/// O fato é o par pedido e versão que o devolveu, único porque o pedido é devolvido uma vez por versão. O beneficiário
/// que ainda não aceitou não tem identidade: entra pelo papel que ele tem no fato.
/// </summary>
public sealed class AvisoDePedidoDevolvido
{
    private const string PapelDoBeneficiario = "beneficiario";
    private const string EmissorDoPapel = "papel";

    private readonly EmissorDeAvisos _emissor;
    private readonly ILinkDoPedido _link;

    public AvisoDePedidoDevolvido(EmissorDeAvisos emissor, ILinkDoPedido link)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(link);

        _emissor = emissor;
        _link = link;
    }

    public Task<IReadOnlyList<EnviarEmail>> AvisarAsync(
        PedidoDeConcessao pedido, string perfilNome, VersaoDoPerfil versao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(versao);

        ModeloDeAviso modelo = new(TipoDeAviso.PedidoDevolvidoPelaJanela, perfilNome, versao.Numero, [], _link.Para(pedido.Id));
        DestinatarioDoAviso[] partes =
        [
            new(pedido.Solicitante, pedido.SolicitanteEmail ?? string.Empty),
            new(pedido.Beneficiario ?? new Identidade(EmissorDoPapel, PapelDoBeneficiario), pedido.BeneficiarioEmail),
        ];

        return _emissor.EmitirAsync(new PedidoDeAviso(modelo, pedido.Id, versao.Id.ToString("N"), partes, []), cancellationToken);
    }
}
