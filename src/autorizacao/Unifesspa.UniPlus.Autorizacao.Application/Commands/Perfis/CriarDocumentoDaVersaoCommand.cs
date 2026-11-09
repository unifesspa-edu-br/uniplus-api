namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Pede a URL de PUT de outro documento para a versão pendente, depois que a verificação recusou o anterior.</summary>
public sealed record CriarDocumentoDaVersaoCommand(Guid PerfilId, Guid VersaoId, long TamanhoDoDocumento)
    : ICommand<Result<EnvioDoDocumentoDto>>;
