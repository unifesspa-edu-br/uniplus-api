namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Serialization;

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
/// <remarks>
/// <para>
/// <c>Produtos</c>, <c>Bancas</c> e <c>Recursos</c> são <c>[JsonRequired]</c>: a gravação
/// substitui a coleção inteira, e uma chave ausente era indistinguível de "a etapa não tem
/// nenhum". Um cliente que não conhecesse os campos — uma tela anterior, um script de
/// importação, uma chamada montada a partir de exemplo antigo — apagava em silêncio os
/// produtos, as bancas e as janelas recursais de TODAS as etapas do processo, e recebia 204.
/// </para>
/// <para>
/// Exigir a chave torna a omissão um 400 e mantém a lista vazia como o que ela deve ser: uma
/// declaração explícita de que não há nenhum. É a mesma razão pela qual <c>BaseadoEmEnem</c> e
/// <c>Cobra</c> são obrigatórios nos seus comandos — o silêncio não pode significar uma
/// escolha que o operador não fez.
/// </para>
/// <para>
/// O <c>= null!</c> existe só para o compilador: <c>Id</c>, <c>FaseCodigo</c>, <c>Inicio</c>,
/// <c>Fim</c> e <c>EmiteParecerIndividual</c> já têm valor padrão, e parâmetro sem default não
/// pode vir depois de um que tem — nem aqui, nem se as três coleções fossem para o fim da
/// lista. Esse default nunca é usado pela desserialização, porque <c>[JsonRequired]</c> recusa
/// a carga sem a chave; o <c>null</c> explícito é recusado pelo validador.
/// </para>
/// <para>
/// O que ele NÃO cobre é a construção em C#: o default deixa o compilador aceitar, em
/// silêncio, uma instância sem as coleções, apesar de o tipo delas ser não-anulável. Quem
/// construir o record em código — teste, fixture, qualquer chamador futuro — precisa declarar
/// as três explicitamente; nenhum diagnóstico avisa se esquecer, e o handler desreferencia.
/// </para>
/// </remarks>
public sealed record EtapaProcessoInput(
    string Nome,
    CaraterEtapa Carater,
    Guid TipoEtapaOrigemId,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem,
    Guid? Id = null,
    string? FaseCodigo = null,
    [property: JsonRequired] IReadOnlyList<ProdutoDaEtapaInput> Produtos = null!,
    DateTimeOffset? Inicio = null,
    DateTimeOffset? Fim = null,
    bool EmiteParecerIndividual = false,
    [property: JsonRequired] IReadOnlyList<BancaDaEtapaInput> Bancas = null!,
    [property: JsonRequired] IReadOnlyList<RecursoDaEtapaInput> Recursos = null!);

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
/// <param name="Papel">Token UPPER_SNAKE, como o produto da fase o recebe.</param>
public sealed record ProdutoDaEtapaInput(string AtoCodigo, string? Papel);

/// <summary>
/// Substitui integralmente as etapas pontuadas do processo (CA-02 da Story
/// #758). Etapas de caráter classificatória/ambas com peso compõem o divisor
/// da média.
/// </summary>
public sealed record DefinirEtapasCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<EtapaProcessoInput> Etapas,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
