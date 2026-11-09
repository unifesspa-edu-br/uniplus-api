namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria o perfil com a versão 1 pendente e o documento que a institui esperando o envio. O perfil só entra em vigor
/// quando esse documento é verificado.
/// </summary>
public sealed record CriarPerfilCommand(DadosDaVersaoRequest Dados) : ICommand<Result<EnvioDoDocumentoDto>>;
