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
    /// Obtém o processo com toda a configuração carregada (todas as coleções
    /// filhas, inclusive as filhas da oferta de atendimento). É a forma
    /// canônica de materializar o agregado para os comandos <c>Definir*</c> e
    /// para a consulta de conformidade.
    /// </summary>
    Task<ProcessoSeletivo?> ObterComConfiguracaoAsync(Guid id, CancellationToken cancellationToken = default);

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
    /// atributo algum do ato (natureza, número, data documental) e por isso é
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
    /// Dados documentais do ato que criou uma versão — os campos que o contrato
    /// de leitura publica sobre o documento, e nada além deles.
    /// <see langword="null"/> quando o ato não existe ou não pertence a
    /// <paramref name="processoSeletivoId"/>: <c>ato_criador_id</c> é referência
    /// por VALOR, sem chave estrangeira (ADR-0061), então a pertença é
    /// verificada aqui, não pelo banco.
    /// <para>
    /// Projeção estreita — não devolve o <see cref="Edital"/> — porque esta é a
    /// única superfície que a #804 troca: quando o ato migrar para o módulo
    /// <c>Publicacoes</c>, muda a origem destes dois campos, não o seletor de
    /// vigência acima, que jamais tocou no ato.
    /// </para>
    /// </summary>
    Task<DadosDocumentaisAto?> ObterDadosDocumentaisDoAtoAsync(
        Guid processoSeletivoId,
        Guid atoCriadorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see langword="true"/> se existe um Processo Seletivo com este id
    /// (checagem barata via <c>AnyAsync</c>, sem materializar o agregado). Usada
    /// pelo seletor de snapshot vigente para distinguir 404 (processo
    /// inexistente) de 422 (sem publicação vigente ≤ o instante).
    /// </summary>
    Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dados que o snapshot vigente publica sobre o DOCUMENTO do ato — a data que
/// o documento declara e a natureza do ato. Deliberadamente não é o
/// <see cref="Edital"/>: nenhum deles ordena coisa alguma (ADR-0104), e o
/// contrato de leitura não deve depender da entidade que a #804 substitui.
/// </summary>
public sealed record DadosDocumentaisAto(DateTimeOffset DataPublicacao, string Natureza);
