namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê os perfis que se podem pedir a quem tem e-mail institucional verificado ou a permissão de manter perfis;
/// o usuário externo sem essa permissão é recusado e não vê a lista.
/// </summary>
public static class ListarPerfisParaPedirQueryHandler
{
    private static readonly PermissionRequirement PermissaoManterPerfis =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisManter).Requisito;

    public static async Task<Result<ListarPerfisParaPedirResult>> Handle(
        ListarPerfisParaPedirQuery query,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        CatalogoDaAplicacao catalogoDaAplicacao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(catalogoDaAplicacao);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<ListarPerfisParaPedirResult>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        if (!PedidoDeConcessao.SolicitanteInstitucional(pessoa, dominios))
        {
            ResultadoDoAcesso acesso = await verificador
                .VerificarAsync(PermissaoManterPerfis, null, cancellationToken)
                .ConfigureAwait(false);
            if (acesso != ResultadoDoAcesso.Permitido)
            {
                return Result<ListarPerfisParaPedirResult>.Failure(
                    acesso == ResultadoDoAcesso.IdentidadeIncompleta
                        ? PedidoHandlerErros.IdentidadeIncompleta()
                        : PedidoHandlerErros.SolicitanteNaoInstitucional());
            }
        }

        (IReadOnlyList<Perfil> ativos, Guid? anterior, Guid? proximo) = await perfis
            .ListarAtivosPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        List<PerfilParaPedidoDto> lista = [];
        foreach (Perfil perfil in ativos)
        {
            // O perfil que mistura unidade e instituição não se concede: não é oferecido.
            if (perfil.Alcance(catalogo) is not { } alcance)
            {
                continue;
            }

            lista.Add(new PerfilParaPedidoDto(
                perfil.Id,
                perfil.Nome,
                perfil.Descricao,
                alcance.ToCodigo(),
                PerfilMapping.Avaliar(perfil, catalogoDaAplicacao).ExigeEncarregada));
        }

        return Result<ListarPerfisParaPedirResult>.Success(new ListarPerfisParaPedirResult(lista, anterior, proximo));
    }
}
