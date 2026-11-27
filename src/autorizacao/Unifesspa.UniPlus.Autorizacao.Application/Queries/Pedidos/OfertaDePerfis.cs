namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A oferta dos perfis que se podem pedir a quem tem e-mail institucional verificado ou a permissão de manter perfis; o
/// usuário externo sem essa permissão é recusado e não vê a lista. O perfil que mistura unidade e instituição não se
/// concede e não é oferecido.
/// </summary>
public sealed class OfertaDePerfis
{
    private static readonly PermissionRequirement PermissaoManterPerfis =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisManter).Requisito;

    private readonly IPerfilRepository _perfis;
    private readonly IDominiosInstitucionais _dominios;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly CatalogoDaAplicacao _catalogoDaAplicacao;

    public OfertaDePerfis(
        IPerfilRepository perfis,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        CatalogoDaAplicacao catalogoDaAplicacao)
    {
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(catalogoDaAplicacao);

        _perfis = perfis;
        _dominios = dominios;
        _catalogo = catalogo;
        _catalogoDaAplicacao = catalogoDaAplicacao;
    }

    internal async Task<Result<ListarPerfisParaPedirResult>> ListarAsync(
        ListarPerfisParaPedirQuery query, PessoaNoToken pessoa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        DomainError? recusa = await RecusaDeQuemNaoPodePedirAsync(pessoa, verificador, cancellationToken).ConfigureAwait(false);

        return recusa is not null
            ? Result<ListarPerfisParaPedirResult>.Failure(recusa)
            : Result<ListarPerfisParaPedirResult>.Success(await OfertarAsync(query, cancellationToken).ConfigureAwait(false));
    }

    // O solicitante institucional pede sem permissão; o externo precisa da permissão de manter perfis.
    private async Task<DomainError?> RecusaDeQuemNaoPodePedirAsync(
        PessoaNoToken pessoa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (PartesDoPedido.SolicitanteInstitucional(pessoa, _dominios))
        {
            return null;
        }

        ResultadoDoAcesso acesso = await verificador.ExigirAsync(PermissaoManterPerfis, null, cancellationToken).ConfigureAwait(false);

        return acesso switch
        {
            ResultadoDoAcesso.Permitido => null,
            ResultadoDoAcesso.IdentidadeIncompleta => PedidoHandlerErros.IdentidadeIncompleta(),
            _ => PedidoHandlerErros.SolicitanteNaoInstitucional(),
        };
    }

    private async Task<ListarPerfisParaPedirResult> OfertarAsync(ListarPerfisParaPedirQuery query, CancellationToken cancellationToken)
    {
        (IReadOnlyList<Perfil> pediveis, Guid? anterior, Guid? proximo) = await _perfis
            .ListarPediveisPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        return new ListarPerfisParaPedirResult([.. pediveis.Select(Oferta).OfType<PerfilParaPedidoDto>()], anterior, proximo);
    }

    private PerfilParaPedidoDto? Oferta(Perfil perfil) =>
        perfil.Alcance(_catalogo) is { } alcance
            ? new PerfilParaPedidoDto(
                perfil.Id,
                perfil.Nome,
                perfil.Descricao,
                CodigoComRotulo<TipoDeEscopo>.De(alcance),
                PerfilMapping.Avaliar(perfil, _catalogoDaAplicacao).ExigeAprovacaoDeProtecaoDeDados,
                PerfilMapping.GovernancaDe(perfil))
            : null;
}
