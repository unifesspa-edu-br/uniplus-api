namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Inicia a inscrição do candidato autenticado num processo seletivo (issue #1858), ou devolve a
/// que já existe — "iniciar" é idempotente por construção, nunca uma segunda inscrição para o
/// mesmo CPF e processo.
/// </summary>
public sealed record IniciarInscricaoCommand(Guid ProcessoSeletivoId) : ICommand<Result<InscricaoDto>>;
