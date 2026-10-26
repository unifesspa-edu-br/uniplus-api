namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

/// <summary>
/// Repositório dos lotes de importação das notas do ENEM. Só acrescenta: um lote é evidência
/// append-only, então não há caminho para atualizar ou apagar.
/// </summary>
public interface ILoteDeNotasDoEnemRepository
{
    Task AdicionarAsync(LoteDeNotasDoEnem lote, CancellationToken cancellationToken = default);

    /// <summary>O lote vigente do escopo, sem os filhos, ou nulo se nada foi importado.</summary>
    Task<LoteDeNotasDoEnem?> ObterVigenteAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default);

    /// <summary>O lote do escopo com as ocorrências, sem rastreamento, ou nulo.</summary>
    Task<LoteDeNotasDoEnem?> ObterComOcorrenciasAsync(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        Guid loteId,
        CancellationToken cancellationToken = default);

    /// <summary>Os lotes do escopo, sem os filhos, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<LoteDeNotasDoEnem>> ListarAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quem são as inscrições pedidas, lidas de uma vez: número de inscrição, nome de exibição (nome
    /// social quando houver), CPF mascarado, versão do formulário e cursos escolhidos. Inscrição sem
    /// nome respondido fica com nome nulo.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, InscritoDoLote>> ObterInscritosAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Por inscrição do lote, os códigos das áreas em que o candidato faltou, na ordem canônica das áreas.
    /// Só aparecem inscrições com ao menos uma área ausente.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ObterAusentesDoLoteAsync(
        Guid loteId, CancellationToken cancellationToken = default);

    /// <summary>Quantos inscritos têm ao menos uma área ausente, por lote.</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarAusentesPorLoteAsync(
        IReadOnlyCollection<Guid> loteIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// O inscrito como o operador o reconhece, com a versão do formulário em que os nomes dos cursos
/// escolhidos (1ª e 2ª opção) estão congelados.
/// </summary>
public sealed record InscritoDoLote(
    string? NumeroDeInscricao,
    string? Nome,
    string CpfMascarado,
    Guid VersaoFormularioId,
    Guid? PrimeiraOpcao,
    Guid? SegundaOpcao);
