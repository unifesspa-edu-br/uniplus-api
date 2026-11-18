namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O documento enviado que passou pela forma: preparado para ser guardado e com a unidade que ele nomeia.</summary>
/// <param name="Documento">O documento normalizado e o nome que a listagem mostra.</param>
/// <param name="CodigoDaUnidade">O código em <c>/identificacao/unidadeAdministradoraCodigo</c>, sempre preenchido.</param>
public sealed record DocumentoPreparado(DocumentoDoRascunho Documento, string CodigoDaUnidade);
