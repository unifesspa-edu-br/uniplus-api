namespace Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Repositório da entidade <see cref="AtoNormativo"/> (schema <c>publicacoes</c>,
/// ADR-0097). O ato é append-only (ADR-0063): há inserção e leitura, nunca
/// atualização nem remoção — daí a ausência de <c>Remover</c>/<c>Atualizar</c>.
/// </summary>
public interface IAtoNormativoRepository
{
    /// <summary>Insere um novo ato. A persistência efetiva ocorre no <c>SalvarAlteracoesAsync</c> da unidade de trabalho.</summary>
    Task AdicionarAsync(AtoNormativo ato, CancellationToken cancellationToken);

    /// <summary>Carrega o ato para leitura (<c>AsNoTracking</c>) — projeção em DTO. Nulo se não existir.</summary>
    Task<AtoNormativo?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Devolve o ato que já retifica <paramref name="atoRetificadoId"/>, ou nulo se
    /// nenhum o retificou ainda. Sustenta a linearidade da cadeia (ADR-0103): dá a
    /// mensagem que nomeia o retificador no caso comum. A garantia dura contra a
    /// corrida check-then-act é do índice único parcial em <c>ato_retificado_id</c>,
    /// não desta consulta.
    /// </summary>
    Task<AtoNormativo?> ObterRetificadorAsync(Guid atoRetificadoId, CancellationToken cancellationToken);

    /// <summary>
    /// Devolve os identificadores de todos os atos da cadeia de retificação linear à
    /// qual <paramref name="atoId"/> pertence — a raiz, o próprio ato e as demais
    /// retificações empilhadas. Sobe de <paramref name="atoId"/> até a raiz e desce
    /// dela até a cabeça. Base para excluir a linhagem inteira do aviso de duplicata:
    /// uma republicação com o mesmo número dentro da cadeia não é colisão, é a mesma
    /// linhagem (ADR-0103). Lista com o próprio <paramref name="atoId"/> quando ele
    /// não participa de retificação alguma.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListarIdsDaCadeiaAsync(Guid atoId, CancellationToken cancellationToken);

    /// <summary>
    /// Devolve os identificadores dos atos que compartilham a mesma numeração
    /// <c>(orgao, serie, ano, numero)</c>, excluindo <paramref name="excluirId"/>
    /// quando informado (o próprio ato, na recomputação do aviso durante a leitura).
    /// Base do aviso de número duplicado (AC4) — diagnóstico do estado atual, não
    /// prova imutável: entre esta consulta e um insert concorrente cabe uma corrida,
    /// tolerada porque o aviso não bloqueia o registro.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListarIdsComMesmaNumeracaoAsync(
        string orgao,
        string serie,
        int ano,
        string numero,
        Guid? excluirId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista atos paginados por cursor keyset bidirecional (ADR-0026 + ADR-0089):
    /// ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve as âncoras de
    /// <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    Task<(IReadOnlyList<AtoNormativo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    /// <summary>
    /// Devolve a raiz da cadeia de retificação a que <paramref name="atoId"/> pertence —
    /// o próprio <paramref name="atoId"/> quando ele não retifica ninguém. Identifica a
    /// linhagem, que é a chave da vaga de <see cref="LinhagemUnicaPorObjeto"/>. Sobe pela
    /// cadeia, que é linear e acíclica (ADR-0103).
    /// </summary>
    Task<Guid> ObterRaizDaCadeiaAsync(Guid atoId, CancellationToken cancellationToken);

    /// <summary>
    /// Devolve a linhagem que ocupa a vaga do objeto
    /// <paramref name="entidadeTipo"/>/<paramref name="entidadeId"/> para o tipo de ato
    /// <paramref name="tipoCodigo"/>, ou <see langword="null"/> se a vaga está livre.
    /// Dá a mensagem legível no caso comum; a garantia dura contra a corrida
    /// check-then-act é o índice único da vaga, não esta consulta.
    /// </summary>
    Task<LinhagemUnicaPorObjeto?> ObterLinhagemDoObjetoAsync(
        string entidadeTipo,
        Guid entidadeId,
        string tipoCodigo,
        CancellationToken cancellationToken);

    /// <summary>Reserva a vaga do objeto para uma linhagem. Persiste no <c>SalvarAlteracoesAsync</c> da unidade de trabalho.</summary>
    Task AdicionarLinhagemAsync(LinhagemUnicaPorObjeto linhagem, CancellationToken cancellationToken);

    /// <summary>
    /// Devolve os pares <c>(tipo, id)</c> das entidades a que <paramref name="atoId"/>
    /// está vinculado. Base da herança de vínculos pela retificação: quem emenda um ato
    /// trata do mesmo objeto que ele — se a retificação não os herdasse, sumiria da
    /// consulta do certame, e a consulta exibiria a versão superada escondendo a que
    /// vale.
    /// </summary>
    Task<IReadOnlyList<(string EntidadeTipo, Guid EntidadeId)>> ListarVinculosDoAtoAsync(
        Guid atoId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Devolve um ato do tipo <paramref name="tipoCodigo"/> já vinculado ao objeto
    /// <paramref name="entidadeTipo"/>/<paramref name="entidadeId"/> e que <b>não</b>
    /// pertence à cadeia <paramref name="idsDaPropriaCadeia"/>, ou <see langword="null"/>
    /// se não há nenhum.
    /// </summary>
    /// <remarks>
    /// Olha o histórico de atos, não a tabela de vagas — e é essa a razão de existir.
    /// O atributo <c>unico_por_objeto</c> é editável no catálogo: um ato publicado quando
    /// o tipo ainda <i>não</i> era único por objeto não reservou vaga alguma, e sem esta
    /// consulta um ato posterior, publicado depois de o atributo virar, encontraria a vaga
    /// livre e o objeto acabaria com duas linhagens vivas. A vaga
    /// (<see cref="LinhagemUnicaPorObjeto"/>) continua sendo a garantia dura contra a
    /// corrida entre transações concorrentes; esta consulta é a que enxerga o passado.
    /// </remarks>
    Task<AtoNormativo?> ObterAtoConflitanteNoObjetoAsync(
        string entidadeTipo,
        Guid entidadeId,
        string tipoCodigo,
        IReadOnlyCollection<Guid> idsDaPropriaCadeia,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista os atos vinculados a uma entidade, ordenados por data de publicação
    /// ascendente com o <c>Id</c> (Guid v7) de desempate — a retificação republica a
    /// mesma data, então a data sozinha não ordena de forma estável. Keyset ordenado
    /// (ADR-0094) sob cursor opaco; as âncoras são o par <c>(data, id)</c>.
    /// </summary>
    Task<(IReadOnlyList<AtoNormativo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarPorEntidadeAsync(
            string entidadeTipo,
            Guid entidadeId,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken);
}
