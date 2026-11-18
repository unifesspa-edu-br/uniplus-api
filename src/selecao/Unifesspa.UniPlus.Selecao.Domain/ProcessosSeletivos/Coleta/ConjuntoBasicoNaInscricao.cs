namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// No processo com inscrição pelo sistema, o conjunto básico de dados do candidato (UNI-REQ-0151) está em
/// formulário obrigatório da fase que coleta inscrição, em campo de resposta única, e não em campo de grupo
/// repetível; o conjunto só em formulário facultativo, ou fora dessa fase, é recusado. O processo de resultado importado não é conferido, e a falta de qualquer
/// formulário obrigatório na inscrição é recusa da obrigatoriedade, e não desta.
/// </summary>
public static class ConjuntoBasicoNaInscricao
{
    public static IReadOnlyList<FieldError> Conferir(
        ProcessoDosFormularios processo, IReadOnlyList<ColetaDoFormulario> coletas, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(coletas);
        ArgumentNullException.ThrowIfNull(cronograma);

        ColetaDoFormulario[] daInscricao = ObrigatoriasDaInscricao(coletas, cronograma);
        return processo.InscricaoPeloSistema && daInscricao.Length > 0 && !TemTodoOConjuntoBasico(daInscricao)
            ? [new FieldError("formularios", new DomainError(
                FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico,
                "O conjunto básico de dados do candidato não está todo em formulário obrigatório da fase que coleta inscrição."))]
            : [];
    }

    private static bool TemTodoOConjuntoBasico(ColetaDoFormulario[] coletas)
    {
        HashSet<string> coletados = [.. coletas.SelectMany(static coleta => coleta.FatosDosItens)];
        return ConjuntoBasicoDaInscricao.Fatos.All(coletados.Contains);
    }

    private static ColetaDoFormulario[] ObrigatoriasDaInscricao(IReadOnlyList<ColetaDoFormulario> coletas, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        HashSet<Guid> fasesDeInscricao = [.. cronograma.Where(static fase => fase.ColetaInscricao).Select(static fase => fase.Id)];
        return [.. coletas.Where(coleta => coleta.Formulario.Obrigatorio && coleta.Formulario.FaseId is { } faseId && fasesDeInscricao.Contains(faseId))];
    }
}
