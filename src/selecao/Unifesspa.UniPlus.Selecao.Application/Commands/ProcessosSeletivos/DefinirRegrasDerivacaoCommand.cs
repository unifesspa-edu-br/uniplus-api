namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Uma condição do predicado <c>quando</c> de uma regra de derivação — a tripla
/// <c>{ Fato, Operador, Valor }</c> na forma flat do wire. O <see cref="Operador"/> é o código
/// canônico UPPER_SNAKE e o <see cref="Valor"/> é o valor JSON já tipado.
/// </summary>
public sealed record CondicaoDerivacaoInput(string Fato, string Operador, JsonElement Valor);

/// <summary>
/// Uma regra de derivação: quando o predicado <see cref="Quando"/> é verdadeiro, a regra contribui
/// o código <see cref="Contribui"/> para o conjunto derivado. O <see cref="Quando"/> é um predicado
/// na forma normal disjuntiva — a lista externa é o <b>OU</b> de cláusulas, cada cláusula interna é
/// o <b>E</b> de condições. A regra <b>âncora</b> (incondicional, sempre verdadeira) é representada
/// por <see cref="Quando"/> <see langword="null"/> — nunca por uma lista vazia.
/// </summary>
public sealed record RegraDerivacaoInput(
    int Ordem,
    string Contribui,
    IReadOnlyList<IReadOnlyList<CondicaoDerivacaoInput>>? Quando);

/// <summary>
/// A configuração de derivação de um fato: o código do fato derivado e a lista de regras que o
/// resolvem.
/// </summary>
public sealed record ConfiguracaoDerivacaoInput(
    string CodigoFato,
    IReadOnlyList<RegraDerivacaoInput> Regras);

/// <summary>
/// Substitui integralmente as regras que derivam os fatos derivados do processo (Story #985).
/// Editável em rascunho (pré-publicação) e sob sessão de retificação de um processo publicado
/// (Story #986). Em rascunho puro a precondição é ignorada (não há sessão nem ETag); sob sessão, o
/// <c>If-Match</c> é obrigatório e a revisão do rascunho avança.
/// </summary>
public sealed record DefinirRegrasDerivacaoCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ConfiguracaoDerivacaoInput> Configuracoes,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
