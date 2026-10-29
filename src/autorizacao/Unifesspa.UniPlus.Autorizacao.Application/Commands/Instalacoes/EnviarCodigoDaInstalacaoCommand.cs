namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passo 3: com o documento verificado, quem iniciou pede o código, que sai para o e-mail institucional verificado
/// do próprio token.
/// </summary>
public sealed record EnviarCodigoDaInstalacaoCommand(Guid Id) : ICommand<Result<InstalacaoDto>>;
