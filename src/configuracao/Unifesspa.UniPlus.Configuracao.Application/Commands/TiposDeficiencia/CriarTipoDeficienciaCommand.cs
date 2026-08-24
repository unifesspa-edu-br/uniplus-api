namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de deficiência: código (identidade semântica de formato fechado,
/// único entre vivos — UNI-REQ-0061), nome (rótulo legível, também único entre
/// vivos), descrição (obrigatória — ADR-0116: serve também como a descrição por
/// valor do fato <c>TIPO_DEFICIENCIA</c>) e a classificação opcional de
/// permanência. O código é informado pelo operador — não há geração automática no
/// backend. O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c>, <c>Nome</c> e <c>Descricao</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125): sem validator FluentValidation garantindo não-nulo a
/// montante, o model binding automático do <c>[ApiController]</c> interceptaria um
/// campo ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarTipoDeficienciaCommand(
    string? Codigo,
    string? Nome,
    string? Descricao,
    bool? Permanente = null) : ICommand<Result<Guid>>;
