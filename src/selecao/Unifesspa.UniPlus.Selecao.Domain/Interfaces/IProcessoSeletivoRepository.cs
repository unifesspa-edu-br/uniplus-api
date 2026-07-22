namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório único do agregado <see cref="ProcessoSeletivo"/>: carrega e
/// persiste a raiz com as entidades de configuração já modeladas (etapas e
/// oferta de atendimento especializado; as demais dimensões entram nas fatias
/// seguintes). Nenhuma entidade filha tem repositório próprio.
/// </summary>
public interface IProcessoSeletivoRepository : IRepository<ProcessoSeletivo>
{
    /// <summary>
    /// Obtém o processo com toda a configuração carregada, para <b>leitura</b> — a
    /// consulta de conformidade e o DTO do recurso. Não trava a linha e <b>não</b> carrega
    /// a sessão editorial.
    /// </summary>
    /// <remarks>
    /// <b>Não use em handler de mutação.</b> Sem o <see cref="ProcessoSeletivo.Rascunho"/>
    /// carregado, a allowlist da ADR-0110 D4 leria <see langword="null"/> como "não há
    /// sessão" quando na verdade ela só não foi carregada — e recusaria uma edição
    /// legítima. Para mutar, é <see cref="ObterParaMutacaoAsync"/>, e um fitness test o
    /// prova.
    /// </remarks>
    Task<ProcessoSeletivo?> ObterComConfiguracaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carregamento <b>de mutação</b> (ADR-0110 D4): a configuração completa, a
    /// <b>sessão editorial</b> e o <b>lock pessimista</b> da linha raiz — tudo o que um
    /// comando que altera o agregado precisa para decidir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que é um método separado.</b> <see cref="ProcessoSeletivo.Rascunho"/> nulo é
    /// ambíguo — significa "não existe" <b>e</b> "não foi carregado". Um comando que
    /// usasse um carregamento sem essa navegação veria "sem sessão" num processo publicado
    /// que tem uma, e recusaria a edição: <b>fail-closed indevido</b>. Tornar o
    /// carregamento de mutação explícito, e provar por fitness test que todo handler passa
    /// por ele, é o que fecha essa porta.
    /// </para>
    /// <para>
    /// <b>O lock.</b> <c>SELECT ... FOR UPDATE</c> na raiz serializa os handlers
    /// concorrentes que tocam o MESMO processo (os seis <c>Definir*</c>, a abertura e o
    /// fechamento da sessão, publicar e retificar). Sem ele, um <c>Definir*</c> que leu
    /// <c>Status = Rascunho</c> antes de uma publicação concorrente persistiria a mutação
    /// <b>depois</b> de a versão já ter sido congelada — furando a RN08 sem que o guard em
    /// memória tivesse visibilidade da publicação alheia. Roda na transação ambiente do
    /// Wolverine: a segunda transação bloqueia aqui até a primeira committar.
    /// </para>
    /// </remarks>
    Task<ProcessoSeletivo?> ObterParaMutacaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista processos paginados por cursor keyset bidirecional (ADR-0026 +
    /// ADR-0089): ordena por <c>Id</c> e retorna até <paramref name="limit"/>
    /// itens na direção <paramref name="direction"/> a partir de
    /// <paramref name="afterId"/> (ou a primeira janela quando <c>null</c>),
    /// sempre em ordem ascendente, junto das âncoras de <c>prev</c>/<c>next</c>
    /// (nulas quando não há aquele lado). Implementações aplicam <c>AsNoTracking</c>.
    /// </summary>
    Task<(IReadOnlyList<ProcessoSeletivo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona a <see cref="VersaoConfiguracao"/> congelada por
    /// <see cref="ProcessoSeletivo.Publicar"/>/<see cref="ProcessoSeletivo.Retificar"/>.
    /// A versão é agregado próprio (ADR-0104) sem repositório dedicado: é o
    /// repositório do certame que a persiste, na mesma transação da publicação.
    /// </summary>
    Task AdicionarVersaoConfiguracaoAsync(VersaoConfiguracao versao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Versão de configuração corrente do processo — a de maior
    /// <see cref="VersaoConfiguracao.NumeroVersao"/>. <see langword="null"/>
    /// quando o processo nunca foi publicado. É o insumo de
    /// <see cref="ProcessoSeletivo.Retificar"/>, que sucede a cadeia a partir
    /// dela. Leitura <c>AsNoTracking</c>.
    /// </summary>
    Task<VersaoConfiguracao?> ObterVersaoAtualAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atos que criaram as versões da configuração do processo — a linhagem de atos do
    /// certame (ADR-0107). Vazio antes da primeira publicação.
    /// </summary>
    /// <remarks>
    /// É contra ela que a conferência da vaga do objeto se faz: uma retificação não disputa a
    /// vaga que a sua própria linhagem já ocupa. Sem esta lista, a conferência não distingue
    /// "a vaga está ocupada por mim" de "a vaga está ocupada por outro".
    /// </remarks>
    Task<IReadOnlyList<Guid>> ObterAtosCriadoresAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a <see cref="VersaoConfiguracao"/> vigente num instante (RN08,
    /// ADR-0075/0076/0104): a de maior
    /// <see cref="VersaoConfiguracao.VigenteAPartirDe"/> ≤
    /// <paramref name="instante"/>, desempatada por
    /// <see cref="VersaoConfiguracao.NumeroVersao"/> decrescente.
    /// <see langword="null"/> antes da primeira publicação — a ausência aflora,
    /// sem recorrer a nada (ADR-0076) — e também quando o processo não existe
    /// ou foi excluído logicamente. Leitura <c>AsNoTracking</c>.
    /// <para>
    /// Quem ordena é a VERSÃO, pelo relógio do sistema: este seletor não lê
    /// atributo algum do ato (tipo, número, data documental) e por isso é
    /// imune a tipos de ato e à data que o documento declara — que a
    /// retificação republica inalterada, e que um acervo migrado pode trazer
    /// regredida. O instante entra explicitamente: nunca há relógio lido por
    /// dentro (ADR-0068).
    /// </para>
    /// </summary>
    Task<VersaoConfiguracao?> ObterVersaoVigenteAsync(
        Guid processoSeletivoId,
        DateTimeOffset instante,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see langword="true"/> se existe um Processo Seletivo com este id
    /// (checagem barata via <c>AnyAsync</c>, sem materializar o agregado). Usada
    /// pelo seletor de snapshot vigente para distinguir 404 (processo
    /// inexistente) de 422 (sem publicação vigente ≤ o instante).
    /// </summary>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default);
}
