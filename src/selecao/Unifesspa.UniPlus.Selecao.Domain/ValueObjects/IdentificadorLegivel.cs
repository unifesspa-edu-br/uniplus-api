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
    /// Valida o valor informado e devolve todas as causas juntas (ADR-0125): o tamanho e o formato são independentes;
    /// a forma de identificador técnico só é conferida quando o formato passou. Quem chama decide como tratar a
    /// ausência (o cadastro a recusa com erro nomeado), por isso nulo ou vazio não é tratado aqui. As causas saem
    /// sem campo: quem chama aponta o campo do pedido.
    /// </summary>
    public static Result<IdentificadorLegivel> Criar(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);

        string normalizado = valor.Trim();
        List<FieldError> causas = [.. CausasDeTamanhoEFormato(normalizado), .. CausaDeFormatoDeGuid(normalizado)];

        return causas.Count > 0
            ? Result<IdentificadorLegivel>.ValidationFailure(causas)
            : Result<IdentificadorLegivel>.Success(new IdentificadorLegivel(normalizado));
    }

    private static IEnumerable<FieldError> CausaDeFormatoDeGuid(string normalizado)
    {
        if (FormatoKebab.TemComprimentoValido(normalizado) && FormatoKebab.TemFormatoValido(normalizado) && Guid.TryParse(normalizado, out _))
        {
            yield return Causa(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelComFormatoDeGuid,
                "O identificador legível não pode ter a forma de um identificador técnico (Guid).");
        }
    }

    private static IEnumerable<FieldError> CausasDeTamanhoEFormato(string normalizado) =>
        [.. CausaDeTamanho(normalizado), .. CausaDeFormato(normalizado)];

    private static IEnumerable<FieldError> CausaDeTamanho(string normalizado)
    {
        if (!FormatoKebab.TemComprimentoValido(normalizado))
        {
            yield return Causa(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelTamanho,
                $"O identificador legível deve ter entre {FormatoKebab.ComprimentoMinimo} e {FormatoKebab.ComprimentoMaximo} caracteres.");
        }
    }

    private static IEnumerable<FieldError> CausaDeFormato(string normalizado)
    {
        if (!FormatoKebab.TemFormatoValido(normalizado))
        {
            yield return Causa(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelFormatoInvalido,
                "O identificador legível deve estar em kebab-case: começar por letra minúscula, "
                + "conter apenas letras minúsculas sem acento, dígitos e hífens, e terminar com "
                + "letra ou dígito (ex.: medicina-2027).");
        }
    }

    private static FieldError Causa(string codigo, string mensagem) => new(null, new DomainError(codigo, mensagem));

    public override string ToString() => Valor ?? string.Empty;
}
