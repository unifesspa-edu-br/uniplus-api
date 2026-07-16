namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Resultado da avaliação de um conjunto de
/// <see cref="Entities.ObrigatoriedadeLegal"/> contra um processo seletivo. Cada item
/// em <see cref="Regras"/> é uma evaluação independente; o veredicto
/// agregado ("processo conforme") é responsabilidade do consumer.
/// </summary>
/// <param name="Regras">Veredicto por regra avaliada.</param>
/// <param name="Avisos">
/// Mensagens diagnósticas geradas durante a avaliação (ex.:
/// <see cref="PredicadoObrigatoriedade.Customizado"/> em uso). Existe para
/// permitir que callers — Application/API — propaguem o sinal para logs
/// estruturados sem o domain service depender de uma stack de logging
/// concreta. O domain service também publica o sinal via
/// <c>ILogger</c> opcional (CA-05 da Story #459), mas a propagação
/// estruturada via <see cref="Avisos"/> é a forma autoritativa.
/// </param>
public sealed record ResultadoConformidade(
    IReadOnlyList<RegraAvaliada> Regras,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Veredicto sobre uma <see cref="Entities.ObrigatoriedadeLegal"/>
/// específica, com evidência forense suficiente para congelar no envelope
/// (Story #853 §3.4): identidade, o predicado inteiro avaliado, o tipo de
/// processo contra o qual a vigência foi resolvida, e a janela de vigência
/// no instante da avaliação. <see cref="Hash"/> é o hash canônico
/// determinístico da regra (<see cref="Entities.ObrigatoriedadeLegal.Hash"/>,
/// #460) no instante em que foi avaliada — evidência estável mesmo que a
/// regra seja editada depois (RN08: o congelado não muda).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "Espelha ObrigatoriedadeLegal.AtoNormativoUrl — payload textual de auditoria, "
        + "pode incluir DOI/URN/IRI que System.Uri só suporta com workarounds.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Pareado com a justificativa de CA1056 acima.")]
/// <param name="Motivo">
/// Razão nomeada da reprovação (CA-02/CA-03/CA-09: nomeia o código de etapa ausente, a oferta
/// que falhou, a modalidade/tipo de documento não implementado, etc.) — <see langword="null"/>
/// quando <see cref="Aprovada"/> é <see langword="true"/>. Diagnóstico transiente, não evidência
/// forense: só regras aprovadas são congeladas no envelope (§3.4), logo todo congelado tem este
/// campo sempre nulo.
/// </param>
public sealed record RegraAvaliada(
    Guid RegraId,
    string RegraCodigo,
    Enums.CategoriaObrigatoriedade Categoria,
    string TipoProcessoCodigoAvaliado,
    PredicadoObrigatoriedade Predicado,
    bool Aprovada,
    string? Motivo,
    string BaseLegal,
    string? AtoNormativoUrl,
    string? PortariaInterna,
    string DescricaoHumana,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string Hash);
