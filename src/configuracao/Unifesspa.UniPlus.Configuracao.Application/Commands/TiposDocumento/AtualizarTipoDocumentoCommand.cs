namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza um tipo de documento existente. O <c>Codigo</c> é editável (diferente
/// da Modalidade), com nova checagem de unicidade entre vivos; os demais atributos
/// (nome, descrição, categoria, formatos, tamanho máximo, tipo equivalente) também
/// podem ser editados. O <c>Id</c> é imutável. O ator (<c>updated_by</c>) é
/// carimbado server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarTipoDocumentoCommand(
    Guid Id,
    string Codigo,
    string Nome,
    string Categoria,
    string? Descricao = null,
    string? FormatosAceitos = null,
    int? TamanhoMaximoMb = null,
    string? TipoEquivalente = null) : ICommand<Result>;
