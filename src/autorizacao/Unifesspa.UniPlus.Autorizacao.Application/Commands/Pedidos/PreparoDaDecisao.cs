namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os passos de <see cref="DecisaoDaEtapa.PrepararAsync"/> depois da conferência de acesso: ou a recusa por falta de
/// permissão, ou a carga do perfil e do pedido na ordem única de travas, o perfil antes do pedido.
/// </summary>
internal sealed record PreparoDaDecisao(
    IPedidoDeConcessaoRepository Pedidos,
    IConcessaoRepository Concessoes,
    IVerificadorDeAcesso Verificador,
    CatalogoDaAplicacao Catalogo,
    AcessoDoAprovador Acesso,
    Identidade Decisor)
{
    public async Task<Result<DecisaoDaEtapa>> NegarAsync(Guid pedidoId, CancellationToken cancellationToken)
    {
        // O id da rota só entra na trilha se o pedido existe; o inexistente registra a negativa sem o id.
        if (await Pedidos.ObterParaLeituraAsync(pedidoId, cancellationToken).ConfigureAwait(false) is not null)
        {
            await Acesso.RegistrarDoDesfechoAsync(Verificador, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(Verificador, incluirAdministrar: false, cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.SemPermissao());
    }

    public async Task<Result<DecisaoDaEtapa>> CarregarAsync(Guid pedidoId, CancellationToken cancellationToken)
    {
        // O perfil se trava antes de o pedido ser carregado: a troca de vigência trava o perfil e depois os pedidos, e a
        // ordem inversa aqui fecharia um ciclo. A leitura também trava a desativação simultânea até o fim da transação.
        Guid? perfilId = await Pedidos.ObterPerfilIdAsync(pedidoId, cancellationToken).ConfigureAwait(false);
        Perfil? perfil = perfilId is { } id ? await Concessoes.ObterParaConcederAsync(id, cancellationToken).ConfigureAwait(false) : null;
        PedidoDeConcessao? pedido = perfil is null ? null : await Pedidos.ObterParaAlterarAsync(pedidoId, cancellationToken).ConfigureAwait(false);

        // Pedido que não existe não foi acessado: nada se registra.
        if (perfil is null || pedido is null)
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.NaoEncontrado());
        }

        return await ConferirEtapaAsync(pedido, perfil, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<DecisaoDaEtapa>> ConferirEtapaAsync(
        PedidoDeConcessao pedido, Perfil perfil, CancellationToken cancellationToken)
    {
        // A permissão que governa é a da etapa que o pedido aguarda, permitida ou negada.
        if (pedido.EtapaAtual is { } etapa)
        {
            await AcessoDoAprovador.RegistrarAsync(Verificador, Acesso.DecisaoDaEtapa(etapa), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await Acesso.RegistrarDoDesfechoAsync(Verificador, cancellationToken).ConfigureAwait(false);
        }

        if (pedido.EtapaAtual is { } etapaAtual && !Acesso.Decide(etapaAtual))
        {
            return Result<DecisaoDaEtapa>.Failure(PedidoHandlerErros.EtapaInvalida());
        }

        pedido.ExigirVersaoEmVigorDe(perfil);

        return Result<DecisaoDaEtapa>.Success(new DecisaoDaEtapa(pedido, perfil, PerfilMapping.Avaliar(perfil, Catalogo), Decisor));
    }
}
