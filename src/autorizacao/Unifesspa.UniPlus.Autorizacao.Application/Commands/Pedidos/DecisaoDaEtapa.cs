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
        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: false, cancellationToken)
            .ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        if (decisor is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.AutorNaoIdentificado());
        }

        if (!acesso.Aprova)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.SemPermissao());
        }

        PedidoDeConcessao? pedido = await pedidos.ObterParaAlterarAsync(pedidoId, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        if (pedido.EtapaAtual is { } etapa && !acesso.Decide(etapa))
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.EtapaInvalida());
        }

        // A leitura trava o perfil até o fim da transação: a desativação simultânea espera ou conflita.
        Perfil? perfil = await concessoes.ObterParaConcederAsync(pedido.PerfilId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        return Result<DecisaoDaEtapa>.Success(new DecisaoDaEtapa(pedido, perfil, PerfilMapping.Avaliar(perfil, catalogo), decisor));
    }
}
