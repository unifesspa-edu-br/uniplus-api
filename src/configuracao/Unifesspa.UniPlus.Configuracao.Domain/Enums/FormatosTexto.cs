namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Mapeamento entre <see cref="FormatoTexto"/> (PascalCase) e o token textual de contrato/banco
/// (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita (molde de
/// <see cref="CardinalidadesFato"/>). Fonte do CHECK de domínio em
/// <c>rol_de_fatos_candidato.formato</c> e do value converter de persistência.
/// </summary>
public static class FormatosTexto
{
    private static readonly Dictionary<FormatoTexto, string> ParaToken = new()
    {
        [FormatoTexto.Livre] = "LIVRE",
        [FormatoTexto.Cpf] = "CPF",
        [FormatoTexto.Email] = "EMAIL",
        [FormatoTexto.Telefone] = "TELEFONE",
        [FormatoTexto.Cep] = "CEP",
        [FormatoTexto.NomePessoa] = "NOME_PESSOA",
    };

    private static readonly Dictionary<string, FormatoTexto> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um valor válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se o valor é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(FormatoTexto valor) =>
        ParaToken.TryGetValue(valor, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Formato de texto fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE); <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out FormatoTexto valor)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out FormatoTexto resolvido))
        {
            valor = resolvido;
            return true;
        }

        valor = FormatoTexto.Nenhum;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static FormatoTexto Analisar(string? token) =>
        TryAnalisar(token, out FormatoTexto valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de formato de texto fora do domínio fechado.");

    /// <summary>
    /// Valida o texto no formato, delegando ao tipo de valor do Kernel; o texto livre só não pode
    /// ser vazio.
    /// </summary>
    public static Result Validar(FormatoTexto formato, string? texto) => formato switch
    {
        FormatoTexto.Livre => string.IsNullOrWhiteSpace(texto)
            ? Result.Failure(new DomainError(FatoCandidatoErrorCodes.TextoLivreVazio, "O texto é obrigatório."))
            : Result.Success(),
        FormatoTexto.Cpf => ComoResultado(Kernel.Domain.ValueObjects.Cpf.Criar(texto)),
        FormatoTexto.Email => ComoResultado(Kernel.Domain.ValueObjects.Email.Criar(texto)),
        FormatoTexto.Telefone => ComoResultado(Kernel.Domain.ValueObjects.Telefone.Criar(texto)),
        FormatoTexto.Cep => ComoResultado(Kernel.Domain.ValueObjects.Cep.Criar(texto)),
        FormatoTexto.NomePessoa => ComoResultado(Kernel.Domain.ValueObjects.NomePessoa.Criar(texto)),
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de texto fora do domínio fechado."),
    };

    /// <summary>
    /// A exibição parcial do texto no formato, pela máscara do tipo de valor do Kernel. O texto
    /// livre, que pode conter qualquer coisa, e o texto que não valida são inteiramente ocultados.
    /// </summary>
    public static string Mascarar(FormatoTexto formato, string? texto) => formato switch
    {
        FormatoTexto.Cpf => Kernel.Domain.ValueObjects.Cpf.Criar(texto) is { IsSuccess: true } cpf ? cpf.Value!.Mascarado : Oculto,
        FormatoTexto.Email => Kernel.Domain.ValueObjects.Email.Criar(texto) is { IsSuccess: true } email ? email.Value!.Mascarado : Oculto,
        FormatoTexto.Telefone => Kernel.Domain.ValueObjects.Telefone.Criar(texto) is { IsSuccess: true } telefone ? telefone.Value!.Mascarado : Oculto,
        FormatoTexto.Cep => Kernel.Domain.ValueObjects.Cep.Criar(texto) is { IsSuccess: true } cep ? cep.Value!.Mascarado : Oculto,
        FormatoTexto.NomePessoa => Kernel.Domain.ValueObjects.NomePessoa.Criar(texto) is { IsSuccess: true } nome ? nome.Value!.Mascarado : Oculto,
        FormatoTexto.Livre => Oculto,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de texto fora do domínio fechado."),
    };

    private const string Oculto = "***";

    private static Result ComoResultado<T>(Result<T> resultado) =>
        resultado.IsSuccess ? Result.Success() : Result.Failure(resultado.Error!);
}
