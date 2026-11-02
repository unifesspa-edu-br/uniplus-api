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
        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: false, pedidoId, cancellationToken)
            .ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        // A recusa por autor não identificado não é de autorização: nenhum acesso ocorreu, e nada se registra.
        if (decisor is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.AutorNaoIdentificado());
        }

        if (!acesso.Aprova)
        {
            // O id da rota só entra na trilha se o pedido existe; o inexistente registra a negativa sem o id.
            if (await pedidos.ObterParaLeituraAsync(pedidoId, cancellationToken).ConfigureAwait(false) is not null)
            {
                await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(verificador, incluirAdministrar: false, cancellationToken)
                    .ConfigureAwait(false);
            }

            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.SemPermissao());
        }

        PedidoDeConcessao? pedido = await pedidos.ObterParaAlterarAsync(pedidoId, cancellationToken).ConfigureAwait(false);

        // Pedido que não existe não foi acessado: nada se registra.
        if (pedido is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        // A permissão que governa é a da etapa que o pedido aguarda, permitida ou negada.
        if (pedido.EtapaAtual is { } etapa)
        {
            await AcessoDoAprovador.RegistrarAsync(verificador, acesso.DecisaoDaEtapa(etapa), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
        }

        if (pedido.EtapaAtual is { } etapaAtual && !acesso.Decide(etapaAtual))
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
