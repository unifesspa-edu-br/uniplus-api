namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código da categoria de documento (UNI-REQ-0013, módulo Configuração) — chave
/// natural classificatória (ex.: <c>RENDA</c>, <c>DOCUMENTO_PROCESSUAL</c>,
/// <c>TITULACAO_EXPERIENCIA</c>). Value object com formato fechado
/// <c>^[A-Z][A-Z0-9_]{1,49}$</c> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de 2 a 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// Mesma convenção de <see cref="CodigoCondicao"/> e dos demais códigos de
/// cadastro do módulo: o número é permitido em qualquer posição exceto a
/// primeira, o que impede numeração sequencial de voltar como identidade sem
/// proibir código semântico que carregue número (exercício fiscal, número de
/// lei). Nenhuma categoria é reservada — todas são editáveis e removíveis.
/// </remarks>
public sealed partial record CodigoCategoriaDocumento
{
    private const int TamanhoMinimo = 2;
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoCategoriaDocumento(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoCategoriaDocumento"/> validando o formato fechado.
    /// Valor nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato
    /// retorna <c>CodigoFormatoInvalido</c>. O valor é normalizado por <c>Trim</c>
    /// antes da validação.
    /// </summary>
    public static Result<CodigoCategoriaDocumento> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoCategoriaDocumento>.Failure(new DomainError(
                CategoriaDocumentoErrorCodes.CodigoObrigatorio,
                "Código da categoria de documento é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoCategoriaDocumento>.Failure(new DomainError(
                CategoriaDocumentoErrorCodes.CodigoFormatoInvalido,
                "Código da categoria de documento deve iniciar com letra maiúscula e conter apenas "
                + $"letras maiúsculas, dígitos e sublinhado, com {TamanhoMinimo} a {TamanhoMaximo} "
                + "caracteres (ex.: RENDA, DOCUMENTO_PROCESSUAL, RACA_ETNIA)."));
        }

        return Result<CodigoCategoriaDocumento>.Success(new CodigoCategoriaDocumento(normalizado));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar value object.</summary>
    public static bool EhValido(string valor) =>
        !string.IsNullOrWhiteSpace(valor) && FormatoValido().IsMatch(valor.Trim());

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
