namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Repositório da inscrição do candidato (issue #1858, ADR-0121).</summary>
public interface IInscricaoRepository
{
    Task<Inscricao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Localiza a inscrição ATIVA (rascunho ou enviada, nunca a cancelada) do dono (CPF) num processo —
    /// a mesma chave do índice único parcial <c>(processo_seletivo_id, cpf_indice)</c>. Assíncrono: a implementação computa o índice
    /// cego via <c>IUniPlusBlindIndexService</c>, que não tem versão síncrona.
    /// </summary>
    Task<Inscricao?> ObterAtivaPorProcessoECpfAsync(Guid processoSeletivoId, Cpf cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas as inscrições do dono (CPF), em qualquer processo seletivo (issue #1860, "Minhas
    /// inscrições") — mesmo índice cego de <see cref="ObterAtivaPorProcessoECpfAsync"/>, sem o filtro
    /// de processo e com as canceladas: é o histórico do candidato.
    /// </summary>
    Task<IReadOnlyList<Inscricao>> ObterTodasPorCpfAsync(Cpf cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// O vínculo da inscrição com o processo e a versão fixada, sem decifrar CPF nem respostas. <see langword="null"/>
    /// quando a inscrição não existe.
    /// </summary>
    Task<VinculoDaInscricao?> ObterVinculoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Localiza a resposta cifrada de UM campo identificador, sem decifrar nada: fora de grupo, a da ocorrência raiz; em
    /// grupo, a da ocorrência do grupo cujo identificador estável é <paramref name="ocorrenciaId"/>.
    /// <see langword="null"/> quando a resposta não existe.
    /// </summary>
    Task<RespostaIdentificadora?> LocalizarIdentificadorAsync(
        Guid inscricaoId, string fatoCodigo, string? grupoCodigo, string? ocorrenciaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O valor decifrado de UMA resposta identificadora, sempre da coluna do valor cifrado e com a chave dos
    /// identificadores. É a única leitura que devolve um identificador em claro fora do próprio agregado. Devolve nulo
    /// quando o valor decifrado é nulo ou vazio.
    /// </summary>
    Task<string?> LerIdentificadorAsync(Guid respostaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// As inscrições enviadas do processo, as submetidas e as canceladas, paginadas por cursor bidirecional
    /// (ADR-0026, ADR-0089) em ordem cronológica. O rascunho fica de fora: enquanto o candidato não envia, não há
    /// inscrição para quem opera o processo. Lê só o necessário — os cursos escolhidos, o nome, o CPF para mascarar e o
    /// instante do primeiro envio —, sem decifrar as demais respostas. O nome e o CPF só são lidos (e o CPF decifrado) quando
    /// <paramref name="lerNome"/> e <paramref name="lerCpf"/> pedem.
    /// </summary>
    Task<(IReadOnlyList<ResumoDaInscricaoDoProcesso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarEnviadasDoProcessoAsync(
        Guid processoSeletivoId,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        bool lerNome,
        bool lerCpf,
        CancellationToken cancellationToken = default);

    Task AdicionarAsync(Inscricao entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assíncrono porque o CPF e os documentos são recifrados a cada atualização (ADR-0121) — a
    /// implementação chama <c>IUniPlusEncryptionService</c>, que não tem versão síncrona.
    /// </summary>
    Task AtualizarAsync(Inscricao entity, CancellationToken cancellationToken = default);

    /// <summary>Remove fisicamente a inscrição e as linhas filhas (respostas, grupos, aceites e endereços).</summary>
    Task RemoverAsync(Inscricao entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// O comprovante que ocupa a posição <paramref name="numero"/> na ordem de emissão da inscrição (o instante, e o id para
    /// desempatar), com o total emitido. Só ele é lido e decifrado; <see langword="null"/> quando a posição não existe.
    /// </summary>
    Task<ComprovanteNaPosicao?> ObterComprovanteAsync(Guid inscricaoId, int numero, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quando cada inscrição emitiu cada comprovante, em ordem de emissão, lido só por projeção, sem carregar nem decifrar o
    /// conteúdo. A posição de cada emissão na sua inscrição é o número do comprovante.
    /// </summary>
    Task<IReadOnlyList<EmissaoDoComprovante>> ObterEmissoesAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default);

    /// <summary>
    /// O instante do primeiro envio de cada inscrição, o menor instante entre os comprovantes, lido só por projeção, sem
    /// carregar nem decifrar o conteúdo deles. A inscrição sem comprovante não consta.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ObterPrimeirosEnviosAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default);

    /// <summary>Grava o comprovante emitido por <see cref="Inscricao.Submeter"/> — nunca pela navegação do agregado.</summary>
    Task AdicionarComprovanteAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken = default);

    /// <summary>
    /// Avança a revisão da inscrição só se ela ainda está na <paramref name="revisaoLida"/> e não foi
    /// cancelada — <c>UPDATE ... WHERE id = @id AND revisao = @lida AND situacao &lt;&gt; Cancelada</c>, sem
    /// passar pelo change tracker, na transação do handler. É como a mutação de um arquivo de documento
    /// disputa a inscrição com o envio e o cancelamento: quem chega depois encontra a revisão já avançada
    /// e não afeta nada. <paramref name="apenasRascunho"/> restringe ao rascunho.
    /// </summary>
    /// <returns><see langword="true"/> quando a revisão avançou.</returns>
    Task<bool> TentarAvancarRevisaoAsync(
        Guid id, int revisaoLida, bool apenasRascunho, CancellationToken cancellationToken = default);
}
