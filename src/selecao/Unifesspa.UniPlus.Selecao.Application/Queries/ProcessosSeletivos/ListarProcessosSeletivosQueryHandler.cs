namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Permissoes;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;

using Wolverine.Attributes;

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
    /// <summary>
    /// <c>[NonTransactional]</c>: a porta de concessões lê o <c>AutorizacaoDbContext</c> e o repositório, o
    /// <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
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

        Guid[] unidadesDoRascunho = await UnidadesOndeConfiguraAsync(concessoes, verificador, userContext, cancellationToken).ConfigureAwait(false);

        (IReadOnlyList<ProcessoSeletivo> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await processoSeletivoRepository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, unidadesDoRascunho, cancellationToken)
            .ConfigureAwait(false);

        ProcessoSeletivoResumoDto[] items = [.. itens.Select(Project)];
        return new ListarProcessosSeletivosResult(items, anteriorAfterId, proximoAfterId);
    }

    private static ProcessoSeletivoResumoDto Project(ProcessoSeletivo processo) => new(
        processo.Id,
        processo.Nome,
        new TipoProcessoSnapshotDto(
            processo.TipoProcessoOrigemId,
            processo.TipoProcesso.Codigo,
            processo.TipoProcesso.Nome),
        processo.Status,
        processo.CreatedAt);

    /// <summary>
    /// As unidades das concessões da pessoa em que o ponto de decisão permite configurar. As concessões só indicam onde
    /// olhar; escopo, vigência e código da permissão são regras da decisão, que cada unidade candidata consulta.
    /// </summary>
    private static async Task<Guid[]> UnidadesOndeConfiguraAsync(
        IConcessoesAtivasDoSujeito concessoes, IVerificadorDeAcesso verificador, IUserContext userContext, CancellationToken cancellationToken)
    {
        if (!userContext.IsAuthenticated
            || UsuarioRef.From(userContext.Issuer ?? string.Empty, userContext.UserId ?? string.Empty) is not { IsSuccess: true } usuario)
        {
            return [];
        }

        IReadOnlyList<ConcessaoAtiva> ativas = await concessoes.ListarAsync(usuario.Value!, cancellationToken).ConfigureAwait(false);
        List<Guid> permitidas = [];
        foreach (Guid unidade in ativas.Where(static c => c.UnidadeId is not null).Select(static c => c.UnidadeId!.Value).Distinct())
        {
            ResultadoDoAcesso acesso = await AcessoAoProcesso
                .VerificarNaUnidadeAsync(
                    verificador, unidade, processoId: null, PermissaoSelecao.ProcessosConfigurar,
                    AcessoAoProcesso.RecursoProcesso, sensibilidadeDoRecurso: null, cancellationToken)
                .ConfigureAwait(false);
            if (acesso == ResultadoDoAcesso.Permitido)
            {
                permitidas.Add(unidade);
            }
        }

        return [.. permitidas];
    }
}
