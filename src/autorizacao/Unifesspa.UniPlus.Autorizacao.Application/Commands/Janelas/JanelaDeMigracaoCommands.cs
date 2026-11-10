namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Envia o lote da gestão de acessos: uma decisão por concessão elegível, menos as do próprio gestor.</summary>
public sealed record EnviarLoteDaJanelaCommand(Guid PerfilId, EnviarLoteDaMigracaoRequest Dados) : ICommand<Result<JanelaDeMigracaoDto>>;

/// <summary>Decide a concessão que o lote reteve, por outra pessoa que não o beneficiário.</summary>
public sealed record DecidirRetidaDaJanelaCommand(Guid PerfilId, Guid ConcessaoId, DecidirRetidaRequest Dados)
    : ICommand<Result<JanelaDeMigracaoDto>>;

/// <summary>Decide a etapa da proteção de dados das concessões que a aguardam, de uma a várias.</summary>
public sealed record DecidirEtapaDaProtecaoDaJanelaCommand(Guid PerfilId, DecidirEtapaDaProtecaoRequest Dados)
    : ICommand<Result<JanelaDeMigracaoDto>>;

/// <summary>Encerra a janela antes do prazo, com motivo: quem não migrou perde o acesso.</summary>
public sealed record EncerrarJanelaDoPerfilCommand(Guid PerfilId, EncerrarJanelaRequest Dados) : ICommand<Result<JanelaDeMigracaoDto>>;

/// <summary>
/// Materializa o fim da janela de um perfil que já não está efetivamente aberta e emite os lembretes da que segue aberta, sob a
/// trava do perfil, com os avisos no outbox da mesma transação. Devolve se havia o que fazer.
/// </summary>
public sealed record MaterializarJanelaVencidaCommand(Guid PerfilId) : ICommand<Result<bool>>;
