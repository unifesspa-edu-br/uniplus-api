namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que aprovar e recusar têm em comum antes de decidir: quem decide, se tem a permissão da etapa, o pedido e o
/// perfil que o aprovador avalia agora. A permissão da etapa se confere pela situação do pedido: sem a da etapa, mas
/// com a da outra, a recusa é de etapa; sem nenhuma das duas, é 403. A identidade incompleta responde antes de tudo.
/// </summary>
internal sealed record DecisaoDaEtapa(PedidoDeConcessao Pedido, Perfil Perfil, PerfilAvaliado Avaliado, Identidade Decisor)
{
    public static async Task<Result<DecisaoDaEtapa>> PrepararAsync(
        Guid pedidoId,
        IPedidoDeConcessaoRepository pedidos,
        IConcessaoRepository concessoes,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
        Identidade? decisor,
        CancellationToken cancellationToken)
    {
        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Decisao, pedidoId, cancellationToken).ConfigureAwait(false);

        // A recusa por autor não identificado não é de autorização: nenhum acesso ocorreu, e nada se registra.
        if (acesso is null || decisor is null)
        {
            return Result<DecisaoDaEtapa>.Failure(
                acesso is null ? PedidoHandlerErros.IdentidadeIncompleta() : PedidoHandlerErros.AutorNaoIdentificado());
        }

        PreparoDaDecisao preparo = new(pedidos, concessoes, verificador, catalogo, acesso, decisor);
        return acesso.Aprova
            ? await preparo.CarregarAsync(pedidoId, cancellationToken).ConfigureAwait(false)
            : await preparo.NegarAsync(pedidoId, cancellationToken).ConfigureAwait(false);
    }
}
