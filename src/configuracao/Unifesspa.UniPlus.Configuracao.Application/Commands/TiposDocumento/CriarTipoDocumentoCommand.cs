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
public sealed record CriarTipoDocumentoCommand(
    string Codigo,
    string Nome,
    string Categoria,
    string? Descricao = null,
    string? FormatosAceitos = null,
    int? TamanhoMaximoMb = null,
    string? TipoEquivalente = null) : ICommand<Result<Guid>>;
