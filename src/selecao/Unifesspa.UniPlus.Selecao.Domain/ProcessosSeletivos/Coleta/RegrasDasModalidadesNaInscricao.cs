namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// No processo com inscrição pelo sistema, a regra de aplicabilidade de cada modalidade ofertada cita só
/// fatos conhecidos ao fim da fase que coleta inscrição, coletados pelo formulário da inscrição ou derivados
/// dele (UNI-REQ-0076, UNI-REQ-0075). O fato que só fica conhecido depois, como o grupo da convocação, é
/// recusado; a recusa aponta a modalidade e o fato pelo caminho e não os repete no texto. O processo de
/// resultado importado e o processo sem vagas por modalidade não são conferidos.
/// </summary>
public static class RegrasDasModalidadesNaInscricao
{
    public static IReadOnlyList<FieldError> Conferir(
        ProcessoDosFormularios processo, ConhecimentoDosFatos conhecimento, IReadOnlyList<RegraDaModalidadeOfertada> modalidades)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(conhecimento);
        ArgumentNullException.ThrowIfNull(modalidades);

        return processo.InscricaoPeloSistema && FimDaInscricao(conhecimento) is { } ponto
            ? [.. modalidades.SelectMany(modalidade => RecusasDaModalidade(conhecimento, modalidade, ponto))]
            : [];
    }

    /// <summary>O ponto ao fim da inscrição: a última fase que coleta inscrição, sem formulário, que alcança os obrigatórios dela.</summary>
    private static PontoDeUso? FimDaInscricao(ConhecimentoDosFatos conhecimento) =>
        conhecimento.Cronograma
            .Where(static fase => fase.ColetaInscricao)
            .OrderByDescending(fase => MomentoDaFase.Da(fase, conhecimento.Cronograma))
            .FirstOrDefault() is { } faseDaInscricao
            ? new PontoDeUso(faseDaInscricao, null)
            : null;

    private static IEnumerable<FieldError> RecusasDaModalidade(ConhecimentoDosFatos conhecimento, RegraDaModalidadeOfertada modalidade, PontoDeUso ponto) =>
        modalidade.FatosCitados
            .Distinct(StringComparer.Ordinal)
            .Where(fato => conhecimento.NoPonto(fato, ponto) != ConhecimentoDoFatoNoPonto.Conhecido || !VemDaInscricao(conhecimento, fato, []))
            .Select(fato => new FieldError(
                $"modalidades[{modalidade.CodigoDaModalidade}].fatos.{fato}",
                new DomainError(
                    ModalidadesNaInscricaoErrorCodes.RegraCitaFatoNaoColetado,
                    "A regra da modalidade cita fato que não é conhecido ao fim da fase que coleta inscrição.")));

    /// <summary>O fato é coletado por formulário obrigatório de fase que coleta inscrição, ou é derivado só de fatos assim.</summary>
    private static bool VemDaInscricao(ConhecimentoDosFatos conhecimento, string fato, HashSet<string> emAvaliacao)
    {
        if (conhecimento.Coletas.Any(coleta => ColetadoNaInscricao(conhecimento, coleta, fato)))
        {
            return true;
        }

        if (!conhecimento.Catalogo.DependenciasDosDerivados.TryGetValue(fato, out IReadOnlyCollection<string>? dependencias) || !emAvaliacao.Add(fato))
        {
            return false;
        }

        bool todasDaInscricao = dependencias.All(dependencia => VemDaInscricao(conhecimento, dependencia, emAvaliacao));
        emAvaliacao.Remove(fato);
        return todasDaInscricao;
    }

    private static bool ColetadoNaInscricao(ConhecimentoDosFatos conhecimento, ColetaDoFormulario coleta, string fato) =>
        coleta.Formulario.Obrigatorio
        && coleta.Produz(fato)
        && conhecimento.Cronograma.Any(fase => fase.Id == coleta.Formulario.FaseId && fase.ColetaInscricao);
}
