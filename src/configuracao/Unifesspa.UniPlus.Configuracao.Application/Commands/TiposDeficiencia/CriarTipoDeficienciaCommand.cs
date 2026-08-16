namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de deficiência: nome (chave natural, único entre vivos), descrição
/// (obrigatória — ADR-0116: serve também como a descrição por valor do fato
/// <c>TIPO_DEFICIENCIA</c>) e a classificação opcional de permanência. O ator de
/// auditoria (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>,
/// não no payload.
/// </summary>
/// <remarks>
/// <c>Nome</c> e <c>Descricao</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarTipoDeficienciaCommand(
    string? Nome,
    string? Descricao,
    bool? Permanente = null) : ICommand<Result<Guid>>;
