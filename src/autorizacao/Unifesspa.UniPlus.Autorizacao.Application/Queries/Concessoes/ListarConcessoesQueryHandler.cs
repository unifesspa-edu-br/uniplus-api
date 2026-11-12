namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Handler da listagem de concessões. A borda já autorizou quem administra ou quem consulta as concessões; aqui cada
/// concessão exibida entra na trilha de acesso pela permissão que autorizou, antes de a página sair (ADR-0153). A
/// mecânica de keyset vive no repositório.
/// </summary>
public static class ListarConcessoesQueryHandler
{
    private static readonly PermissionRequirement Administrar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Requisito;

    private static readonly PermissionRequirement Consultar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesConsultar).Requisito;

    public static async Task<ListarConcessoesResult> Handle(
        ListarConcessoesQuery query,
        IConcessaoRepository repository,
        IVerificadorDeAcesso verificador,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();
        FiltroDeConcessoes filtro = new(query.PessoaEmissor, query.PessoaSub, query.UnidadeId, query.PerfilId, query.Situacao, agora);
        (IReadOnlyList<ConcessaoComPerfil> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(filtro, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        await RegistrarCadaConcessaoAsync(verificador, itens, cancellationToken).ConfigureAwait(false);

        return new ListarConcessoesResult([.. itens.Select(i => ConcessaoMapping.ToDto(i, agora))], anterior, proximo);
    }

    // A permissão que autorizou na borda é a que nomeia cada concessão exibida: administrar, se a pessoa a tem, senão
    // consultar.
    private static async Task RegistrarCadaConcessaoAsync(
        IVerificadorDeAcesso verificador, IReadOnlyList<ConcessaoComPerfil> itens, CancellationToken cancellationToken)
    {
        if (itens.Count == 0)
        {
            return;
        }

        DecisaoDeAcesso leitura = await AvaliarAsync(verificador, Administrar, cancellationToken).ConfigureAwait(false);
        leitura = leitura is DecisaoPermitida ? leitura : await AvaliarAsync(verificador, Consultar, cancellationToken).ConfigureAwait(false);
        if (leitura is DecisaoPermitida permitida)
        {
            await verificador.RegistrarParaCadaAsync(
                permitida, ResourceTiposAutorizacao.Concessao, [.. itens.Select(static i => i.Id)], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, CancellationToken cancellationToken) =>
        verificador.AvaliarAsync(
            permissao, ResourceContext.From(ResourceTiposAutorizacao.Concessao, permissao.Sensibilidade).Value, cancellationToken);
}
