namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Uma condição da pré-condição de um <see cref="FatoColetado"/> (Story #926) — a forma
/// <b>relacional</b> (linha com ordinal de cláusula) da tripla <c>{ Fato, Operador, Valor }</c>.
/// Agrupadas por <see cref="Clausula"/> (OU entre cláusulas, E dentro), formam o predicado que
/// decide se o campo produtor do fato é apresentado ao candidato.
/// </summary>
/// <remarks>
/// Deliberadamente separada de <see cref="CondicaoGatilho"/>, apesar da forma idêntica: aquela é
/// filha de <see cref="DocumentoExigido"/> e responde "esta exigência se aplica?"; esta é filha de
/// <see cref="FatoColetado"/> e responde "este campo é apresentado?". Unificá-las sob um
/// discriminador acoplaria dois ciclos de vida distintos — a exigência é substituída com os
/// documentos, o fato coletado com o formulário — e faria a chave estrangeira de uma valer para a
/// outra. O reúso está onde importa: ambas produzem <see cref="CondicaoDnf"/> e são avaliadas pelo
/// mesmo <see cref="Regras.ValueObjects.PredicadoDnf"/>.
/// </remarks>
public sealed class CondicaoPrecondicaoFato : EntityBase
{
    public Guid FatoColetadoId { get; private set; }

    /// <summary>Ordinal da cláusula — OU entre cláusulas, E dentro da mesma cláusula.</summary>
    public int Clausula { get; private set; }

    /// <summary>Código do fato citado — sempre um fato <b>anterior</b> na ordem de coleta.</summary>
    public string Fato { get; private set; } = string.Empty;

    public Operador Operador { get; private set; }

    public JsonElement Valor { get; private set; }

    private CondicaoPrecondicaoFato() { }

    /// <summary>
    /// As condições que representam a exibição de um fato coletado, uma linha por condição, com o
    /// ordinal da cláusula na ordem do predicado. A exibição já tem a forma das condições provada.
    /// </summary>
    internal static IEnumerable<CondicaoPrecondicaoFato> DaExibicao(PredicadoDnf? exibicao, Guid fatoColetadoId) =>
        (exibicao?.Clausulas ?? []).SelectMany(
            (clausula, indice) => clausula.Condicoes.Select(condicao => DaClausula(indice, condicao, fatoColetadoId)));

    private static CondicaoPrecondicaoFato DaClausula(int clausula, CondicaoDnf condicao, Guid fatoColetadoId) => new()
    {
        FatoColetadoId = fatoColetadoId,
        Clausula = clausula,
        Fato = condicao.Fato,
        Operador = condicao.Operador,
        Valor = condicao.Valor,
    };

    /// <summary>Reconstrói o VO já validado: a forma veio provada pela exibição de que a linha saiu.</summary>
    internal CondicaoDnf ParaCondicaoDnf() => CondicaoDnf.Criar(Fato, Operador, Valor).Value!;
}
