namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Envia as decisões da gestão de acessos: uma decisão por concessão elegível, menos as do próprio gestor.</summary>
public sealed record EnviarDecisoesDaRevisaoCommand(Guid PerfilId, EnviarDecisoesDaRevisaoRequest Dados) : ICommand<Result<RevisaoDasConcessoesDto>>;

/// <summary>Decide a concessão que o envio de decisões reteve, por outra pessoa que não o beneficiário.</summary>
public sealed record DecidirRetidaDaRevisaoCommand(Guid PerfilId, Guid ConcessaoId, DecidirRetidaRequest Dados)
    : ICommand<Result<RevisaoDasConcessoesDto>>;

/// <summary>Decide a etapa da proteção de dados das concessões que a aguardam, de uma a várias.</summary>
public sealed record DecidirEtapaDaProtecaoDaRevisaoCommand(Guid PerfilId, DecidirEtapaDaProtecaoRequest Dados)
    : ICommand<Result<RevisaoDasConcessoesDto>>;

/// <summary>Encerra a revisão antes do prazo, com motivo: quem não migrou perde o acesso.</summary>
public sealed record EncerrarRevisaoDoPerfilCommand(Guid PerfilId, EncerrarRevisaoRequest Dados) : ICommand<Result<RevisaoDasConcessoesDto>>;

/// <summary>
/// Materializa o fim da revisão de um perfil que já não está efetivamente aberta e emite os lembretes da que segue aberta, sob a
/// bloqueio do perfil, com os avisos no outbox da mesma transação. Devolve se havia o que fazer.
/// </summary>
public sealed record MaterializarRevisaoVencidaCommand(Guid PerfilId) : ICommand<Result<bool>>;
