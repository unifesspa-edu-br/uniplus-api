namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A exigência que repete por entidade nomeia um grupo repetível do processo, coletado no formulário que a
/// entrega ou num formulário que ele alcança, e os campos do grupo são conhecidos no ponto
/// (UNI-REQ-0069, UNI-REQ-0146). Não há repetição dentro de repetição: o grupo não tem grupo como campo.
/// </summary>
public static class ConferenciaDaRepeticaoPorGrupo
{
    public static IReadOnlyList<FieldError> Conferir(ConhecimentoDosFatos conhecimento, IReadOnlyList<RepeticaoDaExigencia> repeticoes)
    {
        ArgumentNullException.ThrowIfNull(conhecimento);
        ArgumentNullException.ThrowIfNull(repeticoes);

        return [.. repeticoes.SelectMany((repeticao, indice) => RecusaDaRepeticao(conhecimento, repeticao)
            .Select(recusa => new FieldError($"exigencias[{indice}].repeticaoPorGrupo", recusa)))];
    }

    private static IEnumerable<DomainError> RecusaDaRepeticao(ConhecimentoDosFatos conhecimento, RepeticaoDaExigencia repeticao) =>
        conhecimento.PontoDaExigencia(repeticao.CodigoDoFormulario, repeticao.FaseId) is { } ponto
            ? RecusaDoGrupo(conhecimento, repeticao.CodigoDoGrupo, ponto)
            : [];

    private static IEnumerable<DomainError> RecusaDoGrupo(ConhecimentoDosFatos conhecimento, string codigoDoGrupo, PontoDeUso ponto)
    {
        ColetaDoFormulario? coletaDoGrupo = conhecimento.Coletas.FirstOrDefault(coleta => coleta.ColetaGrupo(codigoDoGrupo));
        if (coletaDoGrupo is null)
        {
            yield return Recusa(
                "NoExigencia.RepeticaoPorGrupoInexistente", "O grupo repetível nomeado pela exigência não é coletado por nenhum formulário do processo.");
        }
        else if (!CamposDoGrupoConhecidos(conhecimento, coletaDoGrupo, codigoDoGrupo, ponto))
        {
            yield return Recusa(
                "NoExigencia.RepeticaoPorGrupoNaoAlcancado", "O grupo repetível nomeado pela exigência não é conhecido no formulário que a entrega.");
        }
    }

    private static bool CamposDoGrupoConhecidos(ConhecimentoDosFatos conhecimento, ColetaDoFormulario coletaDoGrupo, string codigoDoGrupo, PontoDeUso ponto) =>
        coletaDoGrupo.Grupos
            .First(grupo => string.Equals(grupo.Codigo, codigoDoGrupo, StringComparison.Ordinal))
            .Subitens
            .All(campo => conhecimento.NoPonto(campo.FatoCodigo, ponto) == ConhecimentoDoFatoNoPonto.Conhecido);

    private static DomainError Recusa(string codigo, string mensagem) => new(codigo, mensagem);
}
