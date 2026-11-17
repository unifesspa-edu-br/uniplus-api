namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Errors;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O documento que o rascunho do processo seletivo guarda: o texto JSON como a aplicação o leu e o nome do processo
/// que a listagem mostra, projetado dele. O domínio não interpreta o conteúdo; a forma do documento é conferida antes,
/// na aplicação.
/// </summary>
public sealed record DocumentoDoRascunho
{
    /// <summary>O tamanho máximo do nome na listagem.</summary>
    public const int NomeMaxLength = 300;

    private DocumentoDoRascunho(string conteudo, string? nome)
    {
        Conteudo = conteudo;
        Nome = nome;
    }

    public string Conteudo { get; }

    /// <summary>O nome sem os espaços das pontas; nulo quando o documento não o traz.</summary>
    public string? Nome { get; }

    /// <summary>Confere só a presença do conteúdo e o tamanho do nome, e acumula as duas recusas.</summary>
    public static Result<DocumentoDoRascunho> Criar(string? conteudo, string? nome)
    {
        string? nomeAparado = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
        FieldError[] errors = [.. new[] { ConferirConteudo(conteudo), ConferirNome(nomeAparado) }.OfType<FieldError>()];

        return errors.Length > 0
            ? Result<DocumentoDoRascunho>.ValidationFailure(errors)
            : Result<DocumentoDoRascunho>.Success(new DocumentoDoRascunho(conteudo!, nomeAparado));
    }

    private static FieldError? ConferirConteudo(string? conteudo) =>
        string.IsNullOrWhiteSpace(conteudo)
            ? new FieldError(nameof(Conteudo), new DomainError(
                RascunhoDeProcessoSeletivoErrorCodes.ConteudoDoDocumentoAusente,
                "O documento do rascunho não pode ser vazio."))
            : null;

    private static FieldError? ConferirNome(string? nomeAparado) =>
        nomeAparado is { Length: > NomeMaxLength }
            ? new FieldError(nameof(Nome), new DomainError(
                RascunhoDeProcessoSeletivoErrorCodes.NomeAcimaDoLimite,
                $"O nome do processo seletivo não pode passar de {NomeMaxLength} caracteres."))
            : null;
}
