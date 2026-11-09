namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Uma decisão pedida sobre uma concessão; a decisão nula ou que não é migrar nem recusar é inválida.</summary>
public sealed record ItemDoLote(Guid ConcessaoId, ResultadoDaMigracao? Decisao);

/// <summary>O lote da gestão de acessos: uma decisão por concessão e a confirmação de que recusar revoga.</summary>
public sealed record PedidoDoLote(IReadOnlyList<ItemDoLote> Itens, bool ConfirmouExclusao);

/// <summary>A decisão sobre a concessão retida, que outra pessoa toma depois do lote.</summary>
public sealed record PedidoDaRetida(Guid ConcessaoId, ResultadoDaMigracao? Decisao, bool ConfirmouExclusao);

/// <summary>A remessa da proteção de dados: uma ou várias concessões que aguardam a etapa dela.</summary>
public sealed record PedidoDaEtapaDaProtecao(IReadOnlyList<ItemDoLote> Itens, bool ConfirmouExclusao);

/// <summary>O que uma pessoa decidiu sobre uma concessão e em que etapa, para a trilha.</summary>
/// <param name="Envio">O envio que produziu a decisão; nulo na decisão da retida.</param>
/// <param name="Dispensa">Por que a proteção de dados não decide a concessão, quando a gestão migrou sem ela.</param>
public sealed record DecisaoHumana(
    EtapaDaMigracao Etapa, ResultadoDaMigracao Resultado, AutoriaDaVersao Autoria, LoteDaMigracao? Envio, string? Dispensa);

/// <summary>Quem encerra a janela antes do prazo e por quê.</summary>
public sealed record EncerramentoManual(AutoriaDaVersao Autoria, string? Motivo);
