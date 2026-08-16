namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de documento classificatório: código (chave natural), nome,
/// descrição opcional, categoria (token canônico UPPER_SNAKE), formatos aceitos e
/// tamanho máximo opcionais e tipo equivalente opcional (rótulo classificatório).
/// O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c>, <c>Nome</c> e <c>Categoria</c> são <c>string?</c>, não
/// <c>string</c> (ADR-0125): sem validator FluentValidation garantindo não-nulo a
/// montante, o model binding automático do <c>[ApiController]</c> interceptaria um
/// campo ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarTipoDocumentoCommand(
    string? Codigo,
    string? Nome,
    string? Categoria,
    string? Descricao = null,
    string? FormatosAceitos = null,
    int? TamanhoMaximoMb = null,
    string? TipoEquivalente = null) : ICommand<Result<Guid>>;
