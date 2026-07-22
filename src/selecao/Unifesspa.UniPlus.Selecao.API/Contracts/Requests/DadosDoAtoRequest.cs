namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

/// <summary>
/// Dados que o DOCUMENTO declara sobre si — órgão publicador, série, ano, data de
/// publicação, quem assina e o tipo do ato no catálogo de Publicações. Nenhum deles é
/// derivado pelo sistema: a data documental não é o relógio, o assinante não é o usuário
/// autenticado, e o tipo não se infere (ADR-0103 — um aviso pode retificar um edital).
/// </summary>
public sealed record DadosDoAtoRequest(
    string Orgao,
    string Serie,
    int Ano,
    DateOnly DataPublicacao,
    string Assinante,
    string TipoAtoCodigo);
