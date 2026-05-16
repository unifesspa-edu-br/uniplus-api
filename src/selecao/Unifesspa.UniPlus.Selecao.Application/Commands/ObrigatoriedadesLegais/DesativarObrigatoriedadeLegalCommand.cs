namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Command que desativa uma <c>ObrigatoriedadeLegal</c> via soft-delete.
/// O ciclo append-only do <c>obrigatoriedade_legal_historico</c> preserva
/// o estado pré-desativação para evidência forense (ADR-0058 + ADR-0063).
/// O <c>UNIQUE</c> parcial sobre <c>Hash</c> libera o slot para reciclar a
/// regra após o soft-delete (ADR-0060 + entrega #520).
/// </summary>
public sealed record DesativarObrigatoriedadeLegalCommand(Guid Id) : ICommand<Result>;
