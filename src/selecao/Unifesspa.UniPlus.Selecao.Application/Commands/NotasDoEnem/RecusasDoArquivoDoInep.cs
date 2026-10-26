namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using Domain.Errors;

using Kernel.Results;

/// <summary>
/// As recusas do arquivo que a borda HTTP decide antes de haver comando: tamanho, tipo e ausência.
/// Ficam aqui, e não no controller, para o código e a mensagem terem um só dono.
/// </summary>
public static class RecusasDoArquivoDoInep
{
    public static Result GrandeDemais { get; } = Result.Failure(new DomainError(
        NotasDoEnemErrorCodes.ArquivoGrandeDemais, "O arquivo passa do tamanho aceito."));

    public static Result Vazio { get; } = Result.Failure(new DomainError(
        NotasDoEnemErrorCodes.ArquivoVazio, "O arquivo não foi enviado ou está vazio."));

    public static Result NaoEhFormulario { get; } = Result.Failure(new DomainError(
        NotasDoEnemErrorCodes.TipoDeArquivoNaoAceito, "A requisição deve ser multipart/form-data."));

    public static Result NaoEhTexto { get; } = Result.Failure(new DomainError(
        NotasDoEnemErrorCodes.TipoDeArquivoNaoAceito, "O arquivo deve ser de texto, como o INEP o devolve."));
}
