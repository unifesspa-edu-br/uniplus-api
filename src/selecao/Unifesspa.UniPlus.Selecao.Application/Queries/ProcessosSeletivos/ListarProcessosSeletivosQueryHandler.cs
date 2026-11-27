namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Permissoes;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A listagem carrega apenas a raiz (sem a configuração) e projeta em
/// <see cref="ProcessoSeletivoResumoDto"/> — omitir as coleções de configuração
/// evita que um processo já configurado pareça incompleto na lista. O detalhe
/// completo vive em <c>GET /{id}</c> (<see cref="ObterProcessoSeletivoQuery"/>).
/// Os processos publicados ou encerrados são abertos a todos. Os demais só entram para quem tem concessão ativa de
/// <see cref="PermissaoSelecao.ProcessosConfigurar"/> na unidade dona, lida a cada requisição, sem cache: quem recebe
/// 404 no acesso individual não descobre o rascunho pela lista.
/// </summary>
public static class ListarProcessosSeletivosQueryHandler
{
    public static async Task<ListarProcessosSeletivosResult> Handle(
        ListarProcessosSeletivosQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IConcessoesAtivasDoSujeito concessoes,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(verificador);

        Guid[] unidadesDoRascunho = await UnidadesDoRascunhoAsync(concessoes, verificador, userContext, cancellationToken).ConfigureAwait(false);

        (IReadOnlyList<ProcessoSeletivo> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await processoSeletivoRepository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, unidadesDoRascunho, cancellationToken)
            .ConfigureAwait(false);

        ProcessoSeletivoResumoDto[] items = [.. itens.Select(Project)];
        return new ListarProcessosSeletivosResult(items, anterior, proximo);
    }

    private static ProcessoSeletivoResumoDto Project(ProcessoSeletivo processo) => new(
        processo.Id,
        processo.Nome,
        new TipoProcessoSnapshotDto(
            processo.TipoProcessoOrigemId,
            processo.TipoProcesso.Codigo,
            processo.TipoProcesso.Nome),
        CodigoComRotulo<SituacaoDoProcessoSeletivo>.De(processo.Situacao),
        processo.CreatedAt);

    /// <summary>
    /// As unidades das concessões da pessoa em que o ponto de decisão permite configurar. As concessões só indicam onde
    /// olhar; escopo, vigência e código da permissão são regras da decisão, que cada unidade candidata consulta. É
    /// pergunta de capacidade: não registra acesso.
    /// </summary>
    private static async Task<Guid[]> UnidadesDoRascunhoAsync(
        IConcessoesAtivasDoSujeito concessoes, IVerificadorDeAcesso verificador, IUserContext userContext, CancellationToken cancellationToken)
    {
        if (!userContext.IsAuthenticated
            || UsuarioRef.From(userContext.Issuer ?? string.Empty, userContext.UserId ?? string.Empty) is not { IsSuccess: true } usuario)
        {
            return [];
        }

        IReadOnlyList<ConcessaoAtiva> ativas = await concessoes.ListarAsync(usuario.Value!, cancellationToken).ConfigureAwait(false);
        return await AcessoAoProcesso.UnidadesOndeConfiguraAsync(verificador, ativas, cancellationToken).ConfigureAwait(false);
    }
}
