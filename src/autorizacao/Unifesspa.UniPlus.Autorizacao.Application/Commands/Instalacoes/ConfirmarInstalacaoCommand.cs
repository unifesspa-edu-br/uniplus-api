namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passo 4: quem iniciou informa o código em até 2 horas, e só então os dois gestores passam a valer. A instalação
/// confirmada é sempre a pendente de quem chama, nunca uma escolhida pelo cliente.
/// </summary>
public sealed record ConfirmarInstalacaoCommand(string? Codigo) : ICommand<Result>;
