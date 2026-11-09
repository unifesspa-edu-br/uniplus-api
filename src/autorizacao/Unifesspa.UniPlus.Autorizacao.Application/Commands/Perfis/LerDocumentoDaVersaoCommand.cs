namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Pede a URL assinada de leitura do documento verificado da versão; o acesso entra na trilha.</summary>
public sealed record LerDocumentoDaVersaoCommand(Guid PerfilId, Guid VersaoId) : ICommand<Result<LeituraDoDocumentoDaVersaoDto>>;
