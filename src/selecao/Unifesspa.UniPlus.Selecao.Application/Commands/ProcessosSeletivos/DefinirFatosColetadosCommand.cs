namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Uma condição da pré-condição de um fato coletado — a tripla
/// <c>{ Fato, Operador, Valor }</c> na forma flat do wire de comando. Idêntica em forma à
/// condição de gatilho documental: o <see cref="Operador"/> é o código canônico UPPER_SNAKE
/// (<c>IGUAL</c>, <c>EM</c>, …) e o <see cref="Valor"/> é o valor JSON já tipado (string,
/// booleano, número ou array para <c>EM</c>/<c>NAO_EM</c>).
/// </summary>
public sealed record CondicaoPrecondicaoInput(string Fato, string Operador, JsonElement Valor);

/// <summary>
/// Um fato que o processo coleta do candidato, com a sua posição na ordem de coleta e a
/// pré-condição opcional que decide se o campo produtor é apresentado. A
/// <see cref="Precondicao"/> é um predicado na forma normal disjuntiva — a lista externa é o
/// <b>OU</b> de cláusulas, cada cláusula interna é o <b>E</b> de condições. Ausência de
/// pré-condição é representada por <see langword="null"/>, nunca por uma lista vazia (fato sem
/// gate é coletado sempre; um predicado sem cláusula avaliaria falso, que é o oposto).
/// </summary>
public sealed record FatoColetadoInput(
    string FatoCodigo,
    int Ordem,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Precondicao);

/// <summary>
/// Substitui integralmente os fatos que o processo coleta do candidato (Story #984). Editável em
/// rascunho (pré-publicação) e sob sessão de retificação de um processo publicado (Story #986). Em
/// rascunho puro a precondição é ignorada (não há sessão nem ETag); sob sessão, o <c>If-Match</c>
/// é obrigatório e a revisão do rascunho avança.
/// </summary>
public sealed record DefinirFatosColetadosCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<FatoColetadoInput> Fatos,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
