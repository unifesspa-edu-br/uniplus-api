namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

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
/// Um fato que o processo coleta do candidato, com a sua posição na ordem de coleta, a
/// apresentação do campo no formulário de inscrição e a pré-condição opcional que decide se o
/// campo produtor é apresentado. A <see cref="Precondicao"/> é um predicado na forma normal
/// disjuntiva — a lista externa é o <b>OU</b> de cláusulas, cada cláusula interna é o <b>E</b>
/// de condições. Ausência de pré-condição é representada por <see langword="null"/>, nunca por
/// uma lista vazia (fato sem gate é coletado sempre; um predicado sem cláusula avaliaria falso,
/// que é o oposto).
/// </summary>
/// <remarks>
/// <see cref="TipoRenderizacao"/> é o código canônico UPPER_SNAKE de
/// <see cref="Domain.Enums.TipoRenderizacaoCodigo"/> — mesmo tratamento de
/// <see cref="CondicaoPrecondicaoInput.Operador"/>: um código ausente ou não reconhecido
/// resolve para o sentinela <see cref="Domain.Enums.TipoRenderizacao.Nenhuma"/>, que
/// <see cref="Domain.Entities.FatoColetado.Criar"/> já rejeita com um erro de domínio (422)
/// claro — dispensa anotação de "campo obrigatório" no contrato, porque não existe um valor de
/// wire ausente que resolva silenciosamente para um tipo de renderização válido.
/// A <see cref="Obrigatoriedade"/> segue a forma do termo exigido: <c>SEMPRE</c> ou <c>NUNCA</c>
/// sem <see cref="PredicadoObrigatoriedade"/>, <c>QUANDO</c> com ele.
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
/// Substitui os itens do formulário de uma finalidade, os fatos que ele coleta do candidato (Story
/// #984); os itens dos outros formulários não mudam. Editável em
/// rascunho (pré-publicação) e sob sessão de retificação de um processo publicado (Story #986). Em
/// rascunho puro a precondição é ignorada (não há sessão nem ETag); sob sessão, o <c>If-Match</c>
/// é obrigatório e a revisão do rascunho avança.
/// </summary>
public sealed record DefinirFatosColetadosCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade,
    IReadOnlyList<FatoColetadoInput> Itens,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
