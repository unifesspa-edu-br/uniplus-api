namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Item de entrada de uma etapa pontuada, usado por
/// <see cref="DefinirEtapasCommand"/>. <see cref="Id"/> é opcional: quando
/// informado e corresponder a uma etapa já existente no processo, o handler
/// atualiza a MESMA etapa em vez de recriá-la — preservando a identidade que
/// critérios de desempate ou regras de eliminação da classificação possam
/// referenciar (<c>etapa_ref</c>). Omitido (ou sem correspondência), o
/// handler cria uma etapa nova.
/// </summary>
/// <param name="TipoEtapaOrigemId">
/// Id do tipo de etapa ativo em Configuração (issue #1071). Obrigatório em toda
/// definição — o handler resolve o item via <c>ITipoEtapaReader</c> e congela um
/// snapshot-copy independente do rótulo editorial (<paramref name="Nome"/>).
/// </param>
public sealed record EtapaProcessoInput(
    string Nome,
    CaraterEtapa Carater,
    Guid TipoEtapaOrigemId,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem,
    Guid? Id = null,
    string? FaseCodigo = null,
    IReadOnlyList<ProdutoDaEtapaInput>? Produtos = null,
    DateTimeOffset? Inicio = null,
    DateTimeOffset? Fim = null,
    bool EmiteParecerIndividual = false,
    IReadOnlyList<BancaDaEtapaInput>? Bancas = null,
    IReadOnlyList<RecursoDaEtapaInput>? Recursos = null);

/// <summary>Uma banca requerida pela etapa — o id do tipo no cadastro de Configuração.</summary>
public sealed record BancaDaEtapaInput(Guid TipoBancaId);

/// <summary>
/// Uma janela recursal da etapa. <paramref name="AtoAncoraCodigo"/> só é lido na âncora de
/// ato publicado: é por ele que Application resolve, dentro dos produtos da própria etapa,
/// qual publicação abre a janela.
/// </summary>
public sealed record RecursoDaEtapaInput(
    AncoraDoRecurso Ancora,
    string RegraCodigo,
    string RegraVersao,
    decimal PrazoValor,
    UnidadePrazo PrazoUnidade,
    string? AtoAncoraCodigo,
    decimal? SuspensividadePrimeiraInstanciaValor,
    UnidadePrazo? SuspensividadePrimeiraInstanciaUnidade,
    decimal? SuspensividadeSegundaInstanciaValor,
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade);

/// <summary>O que uma etapa publica: o código do tipo de ato e o papel no ciclo recursal.</summary>
public sealed record ProdutoDaEtapaInput(string AtoCodigo, PapelProdutoFase? Papel);

/// <summary>
/// Substitui integralmente as etapas pontuadas do processo (CA-02 da Story
/// #758). Etapas de caráter classificatória/ambas com peso compõem o divisor
/// da média.
/// </summary>
public sealed record DefinirEtapasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<EtapaProcessoInput> Etapas,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
