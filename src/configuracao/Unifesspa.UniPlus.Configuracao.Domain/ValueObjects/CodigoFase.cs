namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código de uma <see cref="Entities.FaseCanonica"/> (UNI-REQ-0064, módulo
/// Configuração) — chave natural do vocabulário de cronograma (por exemplo: <c>INSCRICAO</c>,
/// <c>AVALIACAO</c>, <c>CLASSIFICACAO</c>). Código no formato de:
/// <see cref="FormatoDoCodigo"/> — letra maiúscula inicial, seguida de letras
/// maiúsculas ou dígitos e sublinhados isolados. Persistido como <c>varchar</c>
/// via conversor de valor (falha imediata na reidratação).
/// </summary>
/// <remarks>
/// O código valida apenas o <b>formato</b>. A pertença ao conjunto canônico
/// fechado de fases é guarda de domínio da entidade (fábrica
/// <c>FaseCanonica.Criar</c>), não do código — o mesmo código bem-formado
/// pode ou não ser canônico. O código é <b>imutável</b>: o comando de atualização
/// não o aceita como campo editável (vocabulário canônico fixo).
/// </remarks>
public sealed record CodigoFase
{
    private const int TamanhoMaximo = 60;

    private static readonly string MensagemDoFormatoInvalido =
        "Código da fase deve iniciar com letra maiúscula e conter letras maiúsculas, dígitos e sublinhados isolados "
        + $"(sem hífen), com no máximo {TamanhoMaximo} caracteres "
        + "(ex.: INSCRICAO, AVALIACAO, CLASSIFICACAO).";

    public string Valor { get; }

    private CodigoFase(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoFase"/> validando o formato fechado. Valor
    /// nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato (ou acima
    /// do tamanho máximo) retorna <c>CodigoFormatoInvalido</c>. O valor é conferido como recebido.
    /// </summary>
    public static Result<CodigoFase> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Recusa(FaseCanonicaErrorCodes.CodigoObrigatorio, "Código da fase canônica é obrigatório.");
        }

        return !EhValido(valor)
            ? Recusa(FaseCanonicaErrorCodes.CodigoFormatoInvalido, MensagemDoFormatoInvalido)
            : Result<CodigoFase>.Success(new CodigoFase(valor!));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem criar o objeto.</summary>
    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;

    private static Result<CodigoFase> Recusa(string codigo, string mensagem) => Result<CodigoFase>.Failure(new DomainError(codigo, mensagem));

}
