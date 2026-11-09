namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma versão nova do perfil, pendente, com o documento que a institui esperando o envio. Versão não se edita:
/// mudar o perfil é criar outra.
/// </summary>
public sealed record CriarVersaoDoPerfilCommand(Guid PerfilId, DadosDaVersaoRequest Dados) : ICommand<Result<EnvioDoDocumentoDto>>;
