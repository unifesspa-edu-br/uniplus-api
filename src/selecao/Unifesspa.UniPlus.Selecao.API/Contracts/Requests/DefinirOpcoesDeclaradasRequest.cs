namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoEtapasEVagasController.DefinirOpcoesDeclaradas"/> — as opções na ordem
/// de exibição; o processo e o fato vêm da rota. A lista ausente ou nula é recusada (422).
/// </summary>
public sealed record DefinirOpcoesDeclaradasRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    IReadOnlyList<OpcaoDeclaradaRequest>? Opcoes);

/// <summary>Uma opção declarada: o código que a resposta cita e o rótulo exibido ao candidato.</summary>
public sealed record OpcaoDeclaradaRequest(string Codigo, string Rotulo);
