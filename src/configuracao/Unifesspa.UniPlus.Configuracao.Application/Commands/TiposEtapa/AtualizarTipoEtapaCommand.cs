namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O código não integra o comando porque é imutável. <c>Nome</c> é <c>string?</c>,
/// não <c>string</c> (ADR-0125) — ver justificativa equivalente em
/// <see cref="CriarTipoEtapaCommand"/>.
/// </summary>
public sealed record AtualizarTipoEtapaCommand(Guid Id, string? Nome, string? Descricao = null) : ICommand<Result>;
