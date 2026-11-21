namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Os fatos que a condição de aplicação de uma exigência cita são conhecidos no formulário que a entrega, obrigatório ou
/// facultativo (UNI-REQ-0064, UNI-REQ-0077, UNI-REQ-0144). A recusa aponta a exigência e o fato pelo
/// caminho, e o campo de grupo repetível só é citado pela exigência que repete por esse grupo; o texto não repete o fato nem o valor de nenhuma resposta. A exigência cujo formulário o processo
/// não tem na fase dela é recusada pela conferência das exigências do formulário, e não aqui.
/// </summary>
public static class ConferenciaDosGatilhosDasExigencias
{
    public static IReadOnlyList<FieldError> Conferir(ConhecimentoDosFatos conhecimento, IReadOnlyList<GatilhoDaExigencia> gatilhos)
    {
        ArgumentNullException.ThrowIfNull(conhecimento);
        ArgumentNullException.ThrowIfNull(gatilhos);

        return [.. gatilhos.SelectMany((gatilho, indice) => RecusasDoGatilho(conhecimento, gatilho, indice))];
    }

    private static IEnumerable<FieldError> RecusasDoGatilho(ConhecimentoDosFatos conhecimento, GatilhoDaExigencia gatilho, int indice) =>
        conhecimento.PontoDaExigencia(gatilho.CodigoDoFormulario, gatilho.FaseId) is { } ponto
            ? gatilho.FatosCitados
                .Distinct(StringComparer.Ordinal)
                .Select(fato => (fato, recusa: RecusaDaCitacao(conhecimento, gatilho, fato, ponto)))
                .Where(static citacao => citacao.recusa is not null)
                .Select(citacao => new FieldError($"exigencias[{indice}].gatilho.{citacao.fato}", citacao.recusa!))
            : [];

    private static DomainError? RecusaDaCitacao(ConhecimentoDosFatos conhecimento, GatilhoDaExigencia gatilho, string fato, PontoDeUso ponto)
    {
        ConhecimentoNaExigencia naExigencia = conhecimento.NaExigencia(fato, ponto);

        return naExigencia.Situacao != ConhecimentoDoFatoNoPonto.Conhecido
            ? Recusa(naExigencia.Situacao)
            : naExigencia.RecusaDaFase ?? RecusaDoGrupoRepetivel(conhecimento, gatilho, fato);
    }

    private static DomainError? RecusaDoGrupoRepetivel(ConhecimentoDosFatos conhecimento, GatilhoDaExigencia gatilho, string fato)
    {
        string? grupoDoCampo = conhecimento.GrupoDoCampo(fato);

        return grupoDoCampo is null || string.Equals(grupoDoCampo, gatilho.GrupoDeRepeticao, StringComparison.Ordinal)
            ? null
            : new DomainError(
                DocumentoExigidoErrorCodes.FatoDeMembroForaDaRepeticao,
                "O fato citado na condição de aplicação da exigência é campo de grupo repetível e só é citado pela exigência que repete por esse grupo.");
    }

    private static DomainError Recusa(ConhecimentoDoFatoNoPonto conhecimento) => conhecimento switch
    {
        ConhecimentoDoFatoNoPonto.ForaDoProcesso => new(
            DocumentoExigidoErrorCodes.FatoNaoColetadoPeloProcesso,
            "O fato citado na condição de aplicação da exigência não é coletado nem derivado por este processo."),
        _ => new(
            DocumentoExigidoErrorCodes.FatoDeFormularioNaoAlcancado,
            "O fato citado na condição de aplicação da exigência não é conhecido no formulário que a entrega."),
    };
}
