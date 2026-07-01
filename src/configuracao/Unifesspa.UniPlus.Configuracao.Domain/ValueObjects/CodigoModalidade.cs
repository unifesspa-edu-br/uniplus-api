namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de uma <see cref="Entities.Modalidade"/> de concorrência (UNI-REQ-0011,
/// módulo Configuração) — chave natural classificatória (ex.: <c>AC</c>,
/// <c>LB_PPI</c>, <c>LI_Q</c>). Value object com formato fechado:
/// <c>^[A-Z0-9_]+$</c> — apenas letras maiúsculas, dígitos e sublinhado (sem
/// hífen). Persistido como <c>varchar</c> via conversor de valor (fail-fast na
/// reidratação).
/// </summary>
/// <remarks>
/// Diferente do <c>TipoDocumento</c>, o código da Modalidade é <b>imutável</b>: o
/// comando de atualização não o aceita como campo editável, pois a cascata de
/// remanejamento e as referências de composição (<c>ComposicaoOrigem</c>,
/// <c>RemanejamentoArgs</c>) apontam para modalidades por código — renomear
/// quebraria a integridade referencial intra-banco.
/// </remarks>
public sealed partial record CodigoModalidade
{
    private const int TamanhoMaximo = 60;

    public string Valor { get; }

    private CodigoModalidade(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoModalidade"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato (ou acima
    /// do tamanho máximo) retorna <c>CodigoFormatoInvalido</c>. O valor é
    /// normalizado por <c>Trim</c> antes da validação.
    /// </summary>
    public static Result<CodigoModalidade> Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoModalidade>.Failure(new DomainError(
                ModalidadeErrorCodes.CodigoObrigatorio,
                "Código da modalidade de concorrência é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (normalizado.Length > TamanhoMaximo || !FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoModalidade>.Failure(new DomainError(
                ModalidadeErrorCodes.CodigoFormatoInvalido,
                "Código da modalidade deve conter apenas letras maiúsculas, dígitos e sublinhado "
                + $"(sem hífen), com no máximo {TamanhoMaximo} caracteres (ex.: AC, LB_PPI, LI_Q)."));
        }

        return Result<CodigoModalidade>.Success(new CodigoModalidade(normalizado));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem alocar value object.</summary>
    public static bool EhValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor)
        && valor.Trim().Length <= TamanhoMaximo
        && FormatoValido().IsMatch(valor.Trim());

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
