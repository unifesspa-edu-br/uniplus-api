namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código estável de um termo de consentimento, o que o documento de configuração usa para citá-lo. É formado uma vez, na
/// criação, a partir do nome (ex.: <c>TERMO_DE_USO_E_PRIVACIDADE</c>), e nunca muda, mesmo que o termo seja editado depois.
/// Formato fechado: letra maiúscula seguida de letras maiúsculas, dígitos e sublinhado, até <see cref="TamanhoMaximo"/>
/// caracteres.
/// </summary>
/// <remarks>
/// O código identifica o termo no momento do cadastro; a decisão de negócio usa sempre o <c>Id</c>. Nomes que só diferem em
/// acento, caixa ou pontuação geram o mesmo código e são o mesmo termo.
/// </remarks>
public sealed record CodigoTermoConsentimento
{
    public const int TamanhoMaximo = 80;

    public string Valor { get; }

    private CodigoTermoConsentimento(string valor) => Valor = valor;

    /// <summary>Forma o código do termo a partir do nome.</summary>
    public static Result<CodigoTermoConsentimento> Gerar(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        string codigo = GeradorDeCodigoEstavel.Normalizar(nome);
        DomainError? recusa = RecusaDoCodigoGerado(codigo);
        return recusa is null ? Criar(codigo) : Result<CodigoTermoConsentimento>.Failure(recusa);
    }

    private static DomainError? RecusaDoCodigoGerado(string codigo)
    {
        if (codigo.Length == 0)
        {
            return new DomainError(
                TermoConsentimentoErrorCodes.CodigoFormatoInvalido,
                "O nome precisa ter ao menos uma letra ou um dígito para formar o código do termo.");
        }

        return codigo.Length > TamanhoMaximo
            ? new DomainError(
                TermoConsentimentoErrorCodes.CodigoTamanho,
                $"O código do termo, formado pelo nome, passa de {TamanhoMaximo} caracteres: abrevie o nome.")
            : null;
    }

    /// <summary>Valida um código já formado, vindo de uma referência do documento ou do banco.</summary>
    public static Result<CodigoTermoConsentimento> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoTermoConsentimento>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.CodigoFormatoInvalido,
                "O código do termo não pode ser vazio."));
        }

        return EhValido(valor)
            ? Result<CodigoTermoConsentimento>.Success(new CodigoTermoConsentimento(valor!))
            : Result<CodigoTermoConsentimento>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.CodigoFormatoInvalido,
                $"O código do termo, formado pelo nome, deve iniciar com letra e conter apenas letras, dígitos e sublinhado, com no máximo {TamanhoMaximo} caracteres: o nome precisa começar por uma letra por uma letra."));
    }

    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
