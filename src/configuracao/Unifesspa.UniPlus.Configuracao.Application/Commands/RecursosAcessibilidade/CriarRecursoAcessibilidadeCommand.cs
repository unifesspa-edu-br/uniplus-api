namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um recurso de acessibilidade: código (identidade semântica de formato fechado,
/// único entre vivos — UNI-REQ-0061), nome (rótulo legível, também único entre vivos)
/// e descrição opcional. O código é informado pelo operador — não há geração
/// automática no backend. O ator de auditoria (<c>created_by</c>) é carimbado
/// server-side via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125): sem
/// valor default, para o schema OpenAPI continuar listando-os como obrigatórios;
/// nulos, para o campo ausente escapar do <c>[ApiController]</c> e chegar à validação
/// de domínio.
/// </remarks>
public sealed record CriarRecursoAcessibilidadeCommand(
    string? Codigo,
    string? Nome,
    string? Descricao = null) : ICommand<Result<Guid>>;
