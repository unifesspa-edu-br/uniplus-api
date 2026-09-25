namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Errors;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Identificador legível do processo seletivo, escolhido no cadastro: é dele que derivam o
/// endereço da página pública do certame e a chave do documento no acervo público.
/// </summary>
/// <remarks>
/// <para>
/// É <b>validador</b>, não gerador — o valor vem de quem cadastra, porque derivá-lo do nome
/// produziria colisões e endereços que mudam quando o nome é corrigido.
/// </para>
/// <para>
/// Formato e comprimento são os de <see cref="FormatoKebab"/>. Além deles, um valor com a forma
/// de um identificador técnico (Guid) é recusado: a leitura pública localiza o certame tanto
/// pelo identificador técnico quanto por este, e os dois precisam ser distinguíveis só pela forma.
/// </para>
/// </remarks>
public readonly record struct IdentificadorLegivel
{
    public string Valor { get; }

    private IdentificadorLegivel(string valor) => Valor = valor;

    /// <summary>
    /// Valida o valor informado. O chamador decide o que significa a ausência — no cadastro ela
    /// é permitida, na publicação não —, por isso nulo ou vazio não é tratado aqui.
    /// </summary>
    public static Result<IdentificadorLegivel> Criar(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);

        string normalizado = valor.Trim();

        if (!FormatoKebab.TemComprimentoValido(normalizado))
        {
            return Result<IdentificadorLegivel>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho,
                $"O identificador legível deve ter entre {FormatoKebab.ComprimentoMinimo} e {FormatoKebab.ComprimentoMaximo} caracteres."));
        }

        if (!FormatoKebab.TemFormatoValido(normalizado))
        {
            return Result<IdentificadorLegivel>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido,
                "O identificador legível deve estar em kebab-case: começar por letra minúscula, "
                + "conter apenas letras minúsculas sem acento, dígitos e hífens, e terminar com "
                + "letra ou dígito (ex.: medicina-2027)."));
        }

        if (Guid.TryParse(normalizado, out _))
        {
            return Result<IdentificadorLegivel>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelComFormatoDeGuid,
                "O identificador legível não pode ter a forma de um identificador técnico (Guid)."));
        }

        return Result<IdentificadorLegivel>.Success(new IdentificadorLegivel(normalizado));
    }

    public override string ToString() => Valor ?? string.Empty;
}
