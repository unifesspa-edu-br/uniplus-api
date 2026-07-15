namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only de <c>PrecedenciaFase</c> para consumo cross-módulo via
/// <see cref="IPrecedenciaFaseReader"/> (ADR-0056). Uma aresta do grafo de
/// precedências entre fases canônicas: o Módulo Seleção lê o grafo vigente para
/// validar o cronograma de um processo — o gate lê o dado, nunca um literal de
/// código de fase.
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="AntecessoraCodigo">Código canônico da fase antecessora.</param>
/// <param name="SucessoraCodigo">Código canônico da fase sucessora.</param>
/// <param name="PermiteSobreposicao">Se as janelas das duas fases podem se sobrepor quando ambas estão no cronograma.</param>
public sealed record PrecedenciaFaseView(
    Guid Id,
    string AntecessoraCodigo,
    string SucessoraCodigo,
    bool PermiteSobreposicao);
