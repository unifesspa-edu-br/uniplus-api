namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Propõe uma versão nova do perfil: a pendente, com o documento que a institui esperando o envio, e a remoção das
/// permissões que ela retira, que vale na hora. Versão não se edita: mudar o perfil é criar outra.
/// </summary>
public sealed record CriarVersaoDoPerfilCommand(Guid PerfilId, ProporVersaoRequest Dados) : ICommand<Result<ResultadoDaPropostaDto>>;
