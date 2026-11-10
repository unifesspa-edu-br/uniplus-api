namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// Os registros da trilha da contenção de um perfil, lidos do repositório: os eventos e as decisões das etapas da
/// retomada. Cada lista vem ordenada pela posição na ordem total da trilha.
/// </summary>
public sealed record TrilhaDaContencao(IReadOnlyList<EventoDaContencao> Eventos, IReadOnlyList<DecisaoDaRetomada> Decisoes);
