namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Dados que o DOCUMENTO declara sobre si — órgão publicador, série, ano, data do ato,
/// quem assina e o tipo do ato no catálogo de Publicações. Nenhum deles é derivado pelo
/// sistema: a data do ato não é o relógio (o tipo vigora no dia em que o sistema publica, não nela),
/// o assinante não é o usuário autenticado, e o tipo não se infere (ADR-0103 — um aviso pode
/// retificar um edital).
/// </summary>
public sealed record DadosDoAtoRequest(
    string Orgao,
    string Serie,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    int? Ano,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    DateOnly? DataDoAto,
    string Assinante,
    string TipoAtoCodigo);
