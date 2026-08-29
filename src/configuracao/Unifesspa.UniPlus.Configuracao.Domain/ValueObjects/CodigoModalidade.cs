namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Collections.Frozen;
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
/// <para>Diferente do <c>TipoDocumento</c>, o código da Modalidade é <b>imutável</b>: o
/// comando de atualização não o aceita como campo editável, pois a cascata de
/// remanejamento e as referências de composição (<c>ComposicaoOrigem</c>,
/// <c>RemanejamentoArgs</c>) apontam para modalidades por código — renomear
/// quebraria a integridade referencial intra-banco.</para>
/// <para>Treze códigos são <b>reservados</b> ao catálogo legal fixo (ver
/// <see cref="CodigosLegaisFixos"/>): as oito modalidades da Lei 12.711/2012 (red. Lei
/// 14.723/2023), a ampla concorrência, as duas modalidades de pessoa com deficiência fora da
/// reserva federal e as duas vagas por acréscimo do PSIQ. A proteção é exposta por
/// <see cref="EhLegalFixa"/> e aplicada pelo agregado (atualização) e pelos handlers (criação
/// e remoção), não pelo banco.</para>
/// </remarks>
public sealed partial record CodigoModalidade
{
    private const int TamanhoMaximo = 60;

    /// <summary>Ampla concorrência.</summary>
    public const string Ac = "AC";

    /// <summary>Pessoa com deficiência na ampla concorrência (rótulo <c>V</c> nos editais).</summary>
    public const string AcPcd = "AC_PCD";

    /// <summary>Cota de baixa renda para pessoa preta, parda ou indígena.</summary>
    public const string LbPpi = "LB_PPI";

    /// <summary>Cota de baixa renda para pessoa quilombola.</summary>
    public const string LbQ = "LB_Q";

    /// <summary>Cota de baixa renda para pessoa com deficiência.</summary>
    public const string LbPcd = "LB_PCD";

    /// <summary>Cota de baixa renda para egresso de escola pública.</summary>
    public const string LbEp = "LB_EP";

    /// <summary>Cota independente de renda para pessoa preta, parda ou indígena.</summary>
    public const string LiPpi = "LI_PPI";

    /// <summary>Cota independente de renda para pessoa quilombola.</summary>
    public const string LiQ = "LI_Q";

    /// <summary>Cota independente de renda para pessoa com deficiência.</summary>
    public const string LiPcd = "LI_PCD";

    /// <summary>Cota independente de renda para egresso de escola pública.</summary>
    public const string LiEp = "LI_EP";

    /// <summary>
    /// Pessoa com deficiência sem nenhuma condição de origem escolar — a reserva de PcD
    /// para o processo que não oferta as cotas da Lei 12.711/2012 (UNI-REQ-0085).
    /// </summary>
    public const string PcdPuro = "PCD_PURO";

    /// <summary>
    /// Vaga por acréscimo para candidato indígena — modalidade institucional do PSIQ,
    /// suplementar ao total do curso (UNI-REQ-0096). Par cruzado de <see cref="AcQ"/>.
    /// </summary>
    public const string AcI = "AC_I";

    /// <summary>
    /// Vaga por acréscimo para candidato quilombola — modalidade institucional do PSIQ,
    /// suplementar ao total do curso (UNI-REQ-0096). Par cruzado de <see cref="AcI"/>.
    /// </summary>
    public const string AcQ = "AC_Q";

    /// <summary>
    /// Os treze códigos do catálogo legal fixo — piso de ações afirmativas da Lei
    /// 12.711/2012 (red. Lei 14.723/2023) mais a ampla concorrência, as duas modalidades de
    /// pessoa com deficiência fora da reserva federal (<see cref="AcPcd"/>, condicionada a
    /// não ser egresso de escola pública, e <see cref="PcdPuro"/>, sem essa condição) e as
    /// duas vagas por acréscimo do PSIQ (<see cref="AcI"/> e <see cref="AcQ"/>, par cruzado
    /// uma da outra). Não são cadastro: nascem do seed, e a estrutura de vagas de cada um
    /// (natureza, composição, remanejamento) é ditada por norma, não pela universidade.
    /// Alterá-la exige mudança no seed e migração — o que, no par cruzado, é também o que
    /// mantém a reciprocidade: um lado editado sozinho quebraria o cruzamento em silêncio.
    /// </summary>
    public static FrozenSet<string> CodigosLegaisFixos { get; } = FrozenSet.ToFrozenSet(
        [Ac, AcPcd, LbPpi, LbQ, LbPcd, LbEp, LiPpi, LiQ, LiPcd, LiEp, PcdPuro, AcI, AcQ],
        StringComparer.Ordinal);

    public string Valor { get; }

    private CodigoModalidade(string valor) => Valor = valor;

    /// <summary>
    /// Indica se este código pertence ao catálogo legal fixo — comparação case-sensitive
    /// (<see cref="StringComparison.Ordinal"/>), alinhada ao formato canônico do value
    /// object (maiúsculas).
    /// </summary>
    public bool EhLegalFixa => CodigosLegaisFixos.Contains(Valor);

    /// <summary>
    /// Indica se <paramref name="valor"/> é um código do catálogo legal fixo, sem alocar
    /// value object. Apara o valor antes de comparar, como faz <see cref="Criar"/> — um
    /// código com espaços à volta é o mesmo código.
    /// </summary>
    public static bool EhCodigoLegalFixo(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && CodigosLegaisFixos.Contains(valor.Trim());

    /// <summary>
    /// Cria um <see cref="CodigoModalidade"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato (ou acima
    /// do tamanho máximo) retorna <c>CodigoFormatoInvalido</c>. O valor é
    /// normalizado por <c>Trim</c> antes da validação.
    /// </summary>
    public static Result<CodigoModalidade> Criar(string? valor)
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
