namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da listagem. A fila de uma etapa exige a permissão dela e a fila de todos, a de uma das duas; a lista de quem pergunta exige só a identidade. A
/// mecânica de keyset vive no repositório.
/// </summary>
public static class ListarPedidosDeConcessaoQueryHandler
{
    public static async Task<Result<ListarPedidosDeConcessaoResult>> Handle(
        ListarPedidosDeConcessaoQuery query,
        IPedidoDeConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.PessoaDe(userContext) is not { } pessoa)
        {
            return Result<ListarPedidosDeConcessaoResult>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
        }

        if (query.Fila != FilaDePedidos.Meus)
        {
            AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: false, cancellationToken)
                .ConfigureAwait(false);
            if (acesso is null)
            {
                return Result<ListarPedidosDeConcessaoResult>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
            }

            bool pode = query.Fila switch
            {
                FilaDePedidos.Gestor => acesso.Funcional,
                FilaDePedidos.ProtecaoDeDados => acesso.ProtecaoDeDados,
                _ => acesso.Aprova,
            };
            if (!pode)
            {
                return Result<ListarPedidosDeConcessaoResult>.Failure(PedidoHandlerErros.SemPermissao());
            }
        }

        FiltroDePedidos filtro = new(
            query.Fila,
            query.Situacao,
            pessoa.Identidade,
            pessoa.EmailVerificado ? PedidoDeConcessao.EmailNormalizado(pessoa.Email) : null);

        (IReadOnlyList<PedidoComPerfil> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(filtro, query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        PedidoResumoDto[] dtos = [.. itens.Select(PedidoMapping.ToResumo)];

        return Result<ListarPedidosDeConcessaoResult>.Success(new ListarPedidosDeConcessaoResult(dtos, anterior, proximo));
    }
}
