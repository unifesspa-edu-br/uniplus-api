namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma aresta de precedência entre duas fases canônicas (UNI-REQ-0064):
/// código da antecessora, código da sucessora e se as janelas podem se sobrepor
/// (falso por omissão — a não-sobreposição é a regra padrão). O ator de auditoria
/// (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarPrecedenciaFaseCommand(
    string AntecessoraCodigo,
    string SucessoraCodigo,
    bool PermiteSobreposicao = false) : ICommand<Result<Guid>>;
