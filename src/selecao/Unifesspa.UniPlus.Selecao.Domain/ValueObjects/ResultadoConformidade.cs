namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Resultado da avaliação de um conjunto de
/// <see cref="Entities.ObrigatoriedadeLegal"/> contra um edital. Cada item
/// em <see cref="Regras"/> é uma evaluação independente; o veredicto
/// agregado ("edital conforme") é responsabilidade do consumer (ex.:
/// `GET /api/editais/{id}/conformidade` no <c>Selecao.API</c>).
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
/// específica. <see cref="Hash"/> identifica a versão exata da regra
/// avaliada — em V1 é placeholder computado a partir dos campos textuais
/// (<see cref="RegraCodigo"/>, <see cref="BaseLegal"/>,
/// <see cref="PortariaInterna"/>). #460 substitui pelo hash canônico
/// determinístico do JSON da regra completa, momento em que esse campo
/// vira evidência forense estável.
/// </summary>
public sealed record RegraAvaliada(
    string RegraCodigo,
    bool Aprovada,
    string BaseLegal,
    string? PortariaInterna,
    string DescricaoHumana,
    string Hash);
