namespace Unifesspa.UniPlus.Regras.Entradas;

using System.Text.Json;

/// <summary>
/// Uma condição da pré-condição de um fato coletado — a tripla
/// <c>{ Fato, Operador, Valor }</c> na forma flat do wire de comando. Idêntica em forma à
/// condição de gatilho documental: o <see cref="Operador"/> é o código canônico UPPER_SNAKE
/// (<c>IGUAL</c>, <c>EM</c>, …) e o <see cref="Valor"/> é o valor JSON já tipado (string,
/// booleano, número ou array para <c>EM</c>/<c>NAO_EM</c>).
/// </summary>
public sealed record CondicaoPrecondicaoInput(string Fato, string Operador, JsonElement Valor);

/// <summary>
/// Uma restrição sobre o valor respondido no item, pelo <see cref="Tipo"/>: <c>FAIXA_NUMERICA</c>
/// e <c>TAMANHO_TEXTO</c> com <see cref="Minimo"/> e <see cref="Maximo"/> (ao menos um; inteiros no
/// tamanho), <c>OPCOES_PERMITIDAS</c> com as <see cref="Entradas"/> e <c>OPCOES_DAS_RESPOSTAS</c> com
/// os <see cref="Fatos"/> cujas respostas formam as opções.
/// </summary>
public sealed record RestricaoValorInput(
    string Tipo,
    decimal? Minimo = null,
    decimal? Maximo = null,
    IReadOnlyList<OpcoesCondicionadasInput>? Entradas = null,
    IReadOnlyList<string>? Fatos = null);

/// <summary>
/// Um grupo de opções permitidas e a condição, sobre respostas anteriores, em que ele vale; sem
/// condição, vale sempre.
/// </summary>
public sealed record OpcoesCondicionadasInput(
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Quando,
    IReadOnlyList<string> Valores);

/// <summary>
/// Um item do formulário, no processo e no modelo: o fato que o campo coleta do candidato, a
/// posição na ordem de coleta, a apresentação do campo e a exibição condicional em
/// <see cref="Precondicao"/>. O predicado está na forma normal disjuntiva — a lista externa é o
/// <b>OU</b> de cláusulas, e cada cláusula interna é o <b>E</b> de condições. A ausência de
/// condição é <see langword="null"/>, nunca uma lista vazia: o campo sem condição aparece
/// sempre, e um predicado sem cláusula avaliaria falso, o oposto.
/// </summary>
/// <remarks>
/// <see cref="TipoRenderizacao"/> é o código canônico de
/// <see cref="Enums.TipoRenderizacaoCodigo"/>. Um código ausente ou não reconhecido vira o
/// sentinela <see cref="Enums.TipoRenderizacao.Nenhuma"/>, que a forma do item recusa. A
/// <see cref="Obrigatoriedade"/> segue a forma do termo exigido: <c>SEMPRE</c> ou <c>NUNCA</c>
/// sem <see cref="PredicadoObrigatoriedade"/>, e <c>QUANDO</c> com ele.
/// </remarks>
public sealed record FatoColetadoInput(
    string FatoCodigo,
    int Ordem,
    string Rotulo,
    string TipoRenderizacao,
    string? Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Precondicao,
    string? EtapaCodigo = null,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade = null,
    string? Ajuda = null,
    bool PedirConfirmacao = false,
    IReadOnlyList<RestricaoValorInput>? Restricoes = null);

/// <summary>
/// Um grupo repetível do formulário (UNI-REQ-0146): o código, a posição na ordem dos itens, a
/// seção, o rótulo, o mínimo e o máximo de ocorrências, a exibição e a obrigatoriedade do grupo —
/// na forma das do item — e os campos de cada ocorrência, cada um na forma do item, com ordem
/// própria dentro do grupo e sem seção.
/// </summary>
public sealed record GrupoColetadoInput(
    string Codigo,
    int Ordem,
    string Rotulo,
    string? EtapaCodigo,
    int Minimo,
    int? Maximo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string? Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade,
    IReadOnlyList<FatoColetadoInput> Subitens);

/// <summary>
/// Uma etapa do formulário: seção (<c>SECAO</c>) ou bloco de sistema (<c>BLOCO</c>, com o bloco em
/// <see cref="Bloco"/>), com a ordem, o título, os textos de apoio e, na seção, a
/// <see cref="Exibicao"/> condicional — predicado sobre fatos conhecidos antes dela.
/// </summary>
public sealed record EtapaFormularioInput(
    string Codigo,
    int Ordem,
    string Tipo,
    string? Bloco,
    string Titulo,
    string? Descricao,
    string? Aviso,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao = null);

/// <summary>
/// Um termo que o formulário exige: o identificador da exigência, a ordem, o termo e a versão do
/// catálogo, a exibição (nula quando o termo sempre aparece) e a obrigatoriedade
/// (<c>SEMPRE</c>, <c>NUNCA</c> ou <c>QUANDO</c>, esta com predicado). Os predicados têm a forma
/// da pré-condição de fato coletado: OU de cláusulas, cada uma E de condições.
/// </summary>
public sealed record TermoExigidoInput(
    string Codigo,
    int Ordem,
    Guid TermoId,
    Guid VersaoId,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade);
