namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Remove (soft delete) um termo de consentimento sem nenhuma versão promovida.
/// Recusa remover termo com ao menos uma versão — corrigir um termo já
/// promovido é rascunho → revisão → nova versão, nunca remoção do catálogo.
/// </summary>
public sealed record RemoverTermoConsentimentoCommand(Guid Id) : ICommand<Result>;
