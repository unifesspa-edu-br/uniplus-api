namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O número do ciclo de aprovação da proteção de um item do catálogo: começa em 1 e avança a cada reinício de item
/// que já entrou numa solicitação (ADR-0163). Liga a solicitação, as manifestações e a decisão de um mesmo ciclo; as
/// manifestações de um ciclo encerrado continuam no histórico e deixam de ser vigentes (UNI-REQ-0373).
/// </summary>
/// <param name="Numero">O número do ciclo, a partir de 1.</param>
public readonly record struct CicloDaAprovacao(int Numero)
{
    /// <summary>O ciclo em que todo item nasce.</summary>
    public static CicloDaAprovacao Primeiro { get; } = new(1);

    /// <summary>O ciclo que o reinício abre.</summary>
    public CicloDaAprovacao Seguinte() => new(Numero + 1);

    /// <summary>
    /// O ciclo que quem age leu no item. Ausente ou menor que 1 é recusado no campo <c>ciclo</c>, junto das demais
    /// recusas do lote (ADR-0125), e devolve <see langword="null"/>.
    /// </summary>
    public static CicloDaAprovacao? Ler(int? numero, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);

        if (numero is >= 1)
        {
            return new CicloDaAprovacao(numero.Value);
        }

        erros.Add(new("ciclo", new DomainError(ProtecaoDoCatalogoErrorCodes.CicloInvalido, "Informe o ciclo de aprovação lido no item.")));
        return null;
    }
}
