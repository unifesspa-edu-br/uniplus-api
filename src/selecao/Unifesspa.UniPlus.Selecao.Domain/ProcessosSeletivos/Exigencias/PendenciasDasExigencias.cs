namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

public static class PendenciasDasExigencias
{
    /// <summary>A repetição por entidade de exigência viva que deixou de valer: o grupo saiu ou mudou de formulário.</summary>
    internal static DomainError? PendenciaDaRepeticaoPorGrupo(EstadoDaConfiguracao estado) => RegrasDasExigencias.ConferirRepeticaoPorGrupo(estado, estado.NosExigencia);

    /// <summary>
    /// A finalidade de cada exigência continua coerente com a fase dela: a escrita confere, e o
    /// cronograma pode ter tirado da fase, depois, o formulário que a exigência declara.
    /// </summary>
    internal static DomainError? PendenciaDaFinalidadeDasExigencias(EstadoDaConfiguracao estado) =>
        estado.DocumentosExigidos
            .Select(exigencia => estado.CronogramaFases.FirstOrDefault(f => f.Id == exigencia.ExigidoNaFaseId) is { } fase
                ? Services.ValidadorVinculoDaExigencia.RecusaDaFinalidade(exigencia.Finalidade, fase)
                : null)
            .FirstOrDefault(static recusa => recusa is not null);

    /// <summary>
    /// Pendências dos documentos exigidos, conferidas pela publicação e pela sucessão de versão
    /// (retificação e fechamento da sessão), sempre <b>depois</b> da pendência do cronograma.
    /// </summary>
    /// <remarks>
    /// Uma exigência <c>CONDICIONAL</c> sem nenhuma condição de gatilho viva e que
    /// <see cref="DocumentoExigido.DeterminaResultado"/> é "exigência morta": nunca seria cobrada de
    /// ninguém. A decisão sobre o que é exigido para cada modalidade é do gate de conformidade legal
    /// (<see cref="Services.AvaliadorConformidadeLegal.Avaliar"/>), com base no que está configurado.
    /// </remarks>
    internal static DomainError? PendenciaDasExigenciasDocumentais(EstadoDaConfiguracao estado)
    {
        foreach (DocumentoExigido exigencia in estado.DocumentosExigidos)
        {
            bool possuiCondicaoViva = exigencia.Condicoes.Count > 0;
            if (exigencia.Aplicabilidade == Aplicabilidade.Condicional
                && !possuiCondicaoViva
                && exigencia.DeterminaResultado())
            {
                return new DomainError(
                    "DocumentoExigido.CondicionalVaziaDeterminaResultado",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' é CONDICIONAL, determina resultado, mas não tem nenhuma condição de gatilho viva — nunca seria cobrada de ninguém.");
            }
        }

        return null;
    }
}
