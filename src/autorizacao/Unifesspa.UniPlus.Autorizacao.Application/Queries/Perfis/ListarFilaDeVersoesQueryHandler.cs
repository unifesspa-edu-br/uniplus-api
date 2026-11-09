namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da fila de aprovação de uma etapa. A permissão da etapa governa o acesso; a etapa de cada versão se calcula
/// aqui, pelo catálogo atual, e nunca se grava.
/// </summary>
public static class ListarFilaDeVersoesQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>> Handle(
        ListarFilaDeVersoesQuery query,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        return await ListarAsync(query, perfis, verificador, catalogo, clock, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>> ListarAsync(
        ListarFilaDeVersoesQuery query,
        IPerfilRepository perfis,
        IVerificadorDeAcesso verificador,
        CatalogoDaAplicacao catalogo,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (EtapaDaVersaoCodigo.TentarLer(query.Fila) is not { } fila)
        {
            return Recusa(PerfilErros.EtapaInvalida());
        }

        DomainError? negado = await NegadoAsync(verificador, fila, cancellationToken).ConfigureAwait(false);
        if (negado is not null)
        {
            return Recusa(negado);
        }

        IReadOnlyList<Perfil> comPendente = await perfis.ListarComPendenteNoPrazoAsync(clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>.Success(Fila(comPendente, fila, catalogo));
    }

    private static async Task<DomainError?> NegadoAsync(
        IVerificadorDeAcesso verificador, EtapaDaVersao fila, CancellationToken cancellationToken)
    {
        bool? pode = await AcessoDaVersao.PodeListarAsync(verificador, fila, cancellationToken).ConfigureAwait(false);

        return pode switch
        {
            null => PerfilHandlerErros.AutorNaoIdentificado(),
            false => PerfilErros.SemPermissaoNaEtapa(),
            _ => null,
        };
    }

    private static IReadOnlyList<ItemDaFilaDeVersoesDto> Fila(
        IReadOnlyList<Perfil> comPendente, EtapaDaVersao fila, CatalogoDaAplicacao catalogo) =>
        [.. comPendente
            .Select(p => (Perfil: p, Versao: VersaoMapping.PendenteDe(p, catalogo)))
            .Where(x => x.Versao?.EtapaAtual == fila.ToCodigo())
            .Select(static x => new ItemDaFilaDeVersoesDto(x.Perfil.Id, x.Perfil.Nome, x.Versao!))
            .OrderBy(static i => i.Versao.CriadaEm)];

    private static Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> Recusa(DomainError erro) =>
        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>.Failure(erro);
}
