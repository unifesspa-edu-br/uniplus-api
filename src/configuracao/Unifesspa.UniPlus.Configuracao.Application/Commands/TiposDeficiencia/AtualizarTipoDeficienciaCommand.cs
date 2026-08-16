namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza um tipo de deficiência existente. O <c>Nome</c> é editável, com nova
/// checagem de unicidade entre vivos; a descrição (obrigatória, ADR-0116) e a
/// classificação de permanência também podem ser editadas. O <c>Id</c> é
/// imutável. O ator (<c>updated_by</c>) é carimbado server-side via
/// <c>IUserContext</c>.
/// </summary>
/// <remarks>
/// <c>Nome</c> e <c>Descricao</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record AtualizarTipoDeficienciaCommand(
    Guid Id,
    string? Nome,
    string? Descricao,
    bool? Permanente = null) : ICommand<Result>;
