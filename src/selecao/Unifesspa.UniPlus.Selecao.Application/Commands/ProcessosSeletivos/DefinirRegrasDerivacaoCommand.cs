namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.ComponentModel;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// Uma regra de derivação: quando o predicado <see cref="Quando"/> é verdadeiro, a regra contribui
/// o código <see cref="Contribui"/> para o conjunto derivado; no derivado booleano a regra não
/// contribui código (<see cref="Contribui"/> <see langword="null"/>), e a ativa torna o derivado
/// verdadeiro. O <see cref="Quando"/> é um predicado
/// na forma normal disjuntiva — a lista externa é o <b>OU</b> de cláusulas, cada cláusula interna é
/// o <b>E</b> de condições. A regra <b>âncora</b> (incondicional, sempre verdadeira) é representada
/// por <see cref="Quando"/> <see langword="null"/> — nunca por uma lista vazia.
/// </summary>
public sealed record RegraDerivacaoInput(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Ordem,
    string? Contribui,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Quando);

/// <summary>
/// A configuração de derivação de um fato: o código do fato derivado e a lista de regras que o
/// resolvem. A ordem da regra e a lista de regras são obrigatórias: ausentes ou nulas são recusadas (422) na conferência
/// da forma da derivação.
/// </summary>
public sealed record ConfiguracaoDerivacaoInput(
    string CodigoFato,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    IReadOnlyList<RegraDerivacaoInput>? Regras);

/// <summary>
/// Substitui integralmente as regras que derivam os fatos derivados do processo (Story #985).
/// Editável só em rascunho (pré-publicação).
/// </summary>
public sealed record DefinirRegrasDerivacaoCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<ConfiguracaoDerivacaoInput> Configuracoes) : ICommand<Result>;
