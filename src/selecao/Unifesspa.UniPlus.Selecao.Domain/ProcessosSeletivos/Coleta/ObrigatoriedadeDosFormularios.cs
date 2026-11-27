namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A obrigatoriedade dos formulários por fase (UNI-REQ-0144). O processo com inscrição pelo sistema tem ao
/// menos um formulário obrigatório na fase que coleta inscrição, e o de resultado importado não tem
/// formulário nela. Toda fase organizada em chamadas tem ao menos um formulário obrigatório: a recusa sai
/// uma por fase, na ordem do cronograma. O obrigatório numa fase da janela de isenção só existe quando ela também coleta
/// inscrição, e nessa fase as duas recusas não se aplicam uma à outra: o obrigatório é o da inscrição e os
/// facultativos são os do pedido. O processo que cobra taxa tem ao menos um facultativo na janela, e o que
/// não cobra não tem nenhum. A primeira fase depois da importação sempre tem o obrigatório dos dados básicos
/// que a importação não traz; quais fatos ele coleta vem da importação declarada, e não de um fato fixo nesta regra; a habilitação sem exigência nem termo, nas demais, não precisa de formulário. As recusas
/// independentes saem juntas (ADR-0125).
/// </summary>
public static class ObrigatoriedadeDosFormularios
{
    private const string CampoDosFormularios = "formularios";

    public static IReadOnlyList<FieldError> Conferir(
        ProcessoDosFormularios processo, IReadOnlyList<ColetaDoFormulario> coletas, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(coletas);
        ArgumentNullException.ThrowIfNull(cronograma);

        FormularioDeclarado[] formularios = [.. coletas.Select(static coleta => coleta.Formulario)];
        HashSet<Guid> fasesDeInscricao = FasesQue(cronograma, static fase => fase.ColetaInscricao);
        HashSet<Guid> fasesDaJanela = FasesQue(cronograma, static fase => fase.ColetaSolicitacaoIsencao);
        return
        [
            .. RecusasDaInscricao(processo, formularios, fasesDeInscricao),
            .. RecusasDaJanelaDeIsencao(processo, formularios, fasesDeInscricao, fasesDaJanela),
            .. RecusasDasFasesOrganizadasEmChamadas(formularios, cronograma),
            .. RecusaDaPrimeiraFaseDepoisDaImportacao(processo, coletas),
        ];
    }

    private static HashSet<Guid> FasesQue(IEnumerable<FaseCronograma> cronograma, Func<FaseCronograma, bool> criterio) =>
        [.. cronograma.Where(criterio).Select(static fase => fase.Id)];

    private static bool EstaEm(FormularioDeclarado formulario, HashSet<Guid> fases) => formulario.FaseId is { } faseId && fases.Contains(faseId);

    private static IEnumerable<FieldError> RecusasDaInscricao(
        ProcessoDosFormularios processo, IReadOnlyList<FormularioDeclarado> formularios, HashSet<Guid> fasesDeInscricao)
    {
        if (!processo.InscricaoPeloSistema)
        {
            return formularios.Where(formulario => EstaEm(formulario, fasesDeInscricao)).Select(static formulario => Recusa(
                formulario,
                FormularioProcessoErrorCodes.InscricaoEmResultadoImportado,
                "O processo de resultado importado não tem formulário na fase que coleta inscrição."));
        }

        return formularios.Any(formulario => formulario.Obrigatorio && EstaEm(formulario, fasesDeInscricao))
            ? []
            : [new FieldError(CampoDosFormularios, new DomainError(
                FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio,
                "O processo com inscrição pelo sistema precisa de ao menos um formulário obrigatório na fase que coleta inscrição."))];
    }

    private static IEnumerable<FieldError> RecusasDaJanelaDeIsencao(
        ProcessoDosFormularios processo, IReadOnlyList<FormularioDeclarado> formularios, HashSet<Guid> fasesDeInscricao, HashSet<Guid> fasesDaJanela) =>
        [
            .. RecusasDoObrigatorioNaJanela(formularios, fasesDeInscricao, fasesDaJanela),
            .. RecusasDoFacultativoNaJanela(processo, [.. formularios.Where(formulario => !formulario.Obrigatorio && EstaEm(formulario, fasesDaJanela))]),
        ];

    private static IEnumerable<FieldError> RecusasDoObrigatorioNaJanela(
        IReadOnlyList<FormularioDeclarado> formularios, HashSet<Guid> fasesDeInscricao, HashSet<Guid> fasesDaJanela) =>
        formularios
            .Where(formulario => formulario.Obrigatorio && EstaEm(formulario, fasesDaJanela) && !EstaEm(formulario, fasesDeInscricao))
            .Select(static formulario => Recusa(
                formulario,
                FormularioProcessoErrorCodes.FormularioDaIsencaoObrigatorio,
                "O formulário da fase de solicitação de isenção é facultativo: só a fase que também coleta inscrição tem formulário obrigatório."));

    private static IEnumerable<FieldError> RecusasDoFacultativoNaJanela(ProcessoDosFormularios processo, FormularioDeclarado[] facultativos)
    {
        if (!processo.CobraTaxa)
        {
            return facultativos.Select(static formulario => Recusa(
                formulario, FormularioProcessoErrorCodes.IsencaoSemTaxa, "O processo que não cobra taxa não tem formulário na fase de solicitação de isenção."));
        }

        return facultativos.Length > 0
            ? []
            : [new FieldError(CampoDosFormularios, new DomainError(
                FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao,
                "O processo que cobra taxa precisa de ao menos um formulário facultativo na fase de solicitação de isenção."))];
    }

    private static IEnumerable<FieldError> RecusasDasFasesOrganizadasEmChamadas(
        IReadOnlyList<FormularioDeclarado> formularios, IReadOnlyCollection<FaseCronograma> cronograma) =>
        cronograma
            .Select(static (fase, posicao) => (fase, posicao))
            .Where(par => par.fase.OrganizadaEmChamadas && !formularios.Any(formulario => formulario.Obrigatorio && formulario.FaseId == par.fase.Id))
            .OrderBy(static par => par.fase.Ordem)
            .Select(static par => new FieldError($"fases[{par.posicao}]", new DomainError(
                FormularioProcessoErrorCodes.ChamadasSemFormularioObrigatorio,
                $"{CitacaoDaFase.ComArtigo(par.fase.Citacao)} é organizada em chamadas e precisa de ao menos um formulário obrigatório.")));

    private static IEnumerable<FieldError> RecusaDaPrimeiraFaseDepoisDaImportacao(ProcessoDosFormularios processo, IReadOnlyList<ColetaDoFormulario> coletas) =>
        processo.PrimeiraFaseDepoisDaImportacaoId is { } faseId && !coletas.Any(coleta => ObrigatorioNaFase(coleta, faseId))
            ? [new FieldError(CampoDosFormularios, new DomainError(
                FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio,
                "A primeira fase depois da importação precisa de formulário obrigatório que coleta os dados que a importação não traz."))]
            : [];

    private static bool ObrigatorioNaFase(ColetaDoFormulario coleta, Guid faseId) =>
        coleta.Formulario.Obrigatorio && coleta.Formulario.FaseId == faseId;

    private static FieldError Recusa(FormularioDeclarado formulario, string codigo, string mensagem) =>
        new($"{CampoDosFormularios}[{formulario.Codigo.Valor}]", new DomainError(codigo, mensagem));
}
