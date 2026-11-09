namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Pede outra URL de PUT para o documento da versão que ainda aguarda o envio: a anterior pode ter se perdido ou expirado.</summary>
public sealed record ReemitirEnvioDoDocumentoDaVersaoCommand(Guid PerfilId, Guid VersaoId) : ICommand<Result<EnvioDoDocumentoDto>>;
