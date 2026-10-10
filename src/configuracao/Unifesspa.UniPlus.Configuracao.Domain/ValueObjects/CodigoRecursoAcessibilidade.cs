namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código do recurso de acessibilidade (UNI-REQ-0012 e UNI-REQ-0061, módulo
/// Configuração) — identidade semântica do apoio ofertado (ex.: <c>LEDOR</c>,
/// <c>SALA_AMAMENTACAO</c>). Value object com formato fechado:
/// <c>^[A-Z][A-Z0-9_]{1,49}$</c> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de 2 a 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (fail-fast na reidratação).
/// </summary>
/// <remarks>
/// Espelha <see cref="CodigoTipoDeficiencia"/> em formato e disciplina de erro, sem
/// código reservado: é o valor que o candidato escolhe no fato
/// <c>RECURSO_ACESSIBILIDADE</c>, e nenhum recurso é citado literalmente por norma.
/// </remarks>
public sealed partial record CodigoRecursoAcessibilidade
{
    private const int TamanhoMinimo = 2;
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoRecursoAcessibilidade(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoRecursoAcessibilidade"/> validando o formato fechado.
    /// Valor nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato
    /// retorna <c>CodigoFormatoInvalido</c>. O valor é normalizado por <c>Trim</c>
    /// antes da validação.
    /// </summary>
    public static Result<CodigoRecursoAcessibilidade> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoRecursoAcessibilidade>.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.CodigoObrigatorio,
                "Código do recurso de acessibilidade é obrigatório."));
        }

        string normalizado = valor.Trim();

        if (!FormatoValido().IsMatch(normalizado))
        {
            return Result<CodigoRecursoAcessibilidade>.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.CodigoFormatoInvalido,
                "Código do recurso de acessibilidade deve iniciar com letra maiúscula e conter apenas "
                + $"letras maiúsculas, dígitos e sublinhado, com {TamanhoMinimo} a {TamanhoMaximo} "
                + "caracteres (ex.: LEDOR, SALA_AMAMENTACAO)."));
        }

        return Result<CodigoRecursoAcessibilidade>.Success(new CodigoRecursoAcessibilidade(normalizado));
    }

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
