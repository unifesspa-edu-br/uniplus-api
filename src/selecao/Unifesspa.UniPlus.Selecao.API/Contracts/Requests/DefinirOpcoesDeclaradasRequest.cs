namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirOpcoesDeclaradas"/> — as opções na ordem
/// de exibição; o processo e o fato vêm da rota.
/// </summary>
public sealed record DefinirOpcoesDeclaradasRequest(IReadOnlyList<OpcaoDeclaradaRequest> Opcoes);

/// <summary>Uma opção declarada: o código que a resposta cita e o rótulo exibido ao candidato.</summary>
public sealed record OpcaoDeclaradaRequest(string Codigo, string Rotulo);
