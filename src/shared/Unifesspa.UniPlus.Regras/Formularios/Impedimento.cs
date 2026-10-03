namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A resposta do campo que impede a inscrição, e a mensagem que explica o motivo ao candidato
/// (UNI-REQ-0145). Cada cláusula da condição cita a resposta do próprio campo e pode combiná-la com
/// respostas anteriores; o campo não aparecer, ou ficar sem resposta, não impede. A avaliação diz que
/// o impedimento está ativo; recusar a inscrição é da execução.
/// </summary>
public sealed record Impedimento(PredicadoDnf Quando, string Mensagem)
{
    /// <summary>Os tipos de campo que aceitam impedimento: os que entram em regra (ADR-0136).</summary>
    public static bool CabeNoCampo(TipoRenderizacao campo) => campo is
        TipoRenderizacao.Booleano or TipoRenderizacao.Numero or TipoRenderizacao.SelecaoUnica
        or TipoRenderizacao.SelecaoMultipla or TipoRenderizacao.Municipio;

    /// <summary>
    /// A recusa de cada item com impedimento, na posição dele, quando o formulário não é o de
    /// inscrição: só ele tem impedimento, porque o impedimento impede a inscrição do candidato.
    /// </summary>
    public static List<FieldError> ConferirFinalidade(FinalidadeFormulario finalidade, IEnumerable<Impedimento?> impedimentosDosItens)
    {
        ArgumentNullException.ThrowIfNull(impedimentosDosItens);

        return finalidade == FinalidadeFormulario.Inscricao
            ? []
            : [.. impedimentosDosItens
                .Select(static (impedimento, indice) => (Impedimento: impedimento, Indice: indice))
                .Where(static p => p.Impedimento is not null)
                .Select(static p => new FieldError($"itens[{p.Indice}].impedimento", new DomainError(
                    ItemFormularioErrorCodes.ImpedimentoForaDaInscricao,
                    "Só o formulário de inscrição tem impedimento: ele impede a inscrição do candidato.")))];
    }

    /// <summary>Os fatos citados pela condição, inclusive o do próprio campo.</summary>
    public IReadOnlyCollection<string> FatosCitados => Quando.FatosCitados;

    /// <summary>Se a resposta, com as anteriores, ativa o impedimento.</summary>
    public Ternario Avaliar(IReadOnlyDictionary<string, FatoResolvido> fatos) => Quando.Avaliar(fatos);
}
