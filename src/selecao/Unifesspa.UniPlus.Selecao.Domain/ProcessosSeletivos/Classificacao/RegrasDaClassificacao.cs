namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As recusas de trocar a classificação que dependem de outras dimensões do processo: as etapas que
/// as regras de eliminação citam, a etapa de nota do ENEM, o quadro de pesos por área que o desempate
/// exige e as edições do ENEM que uma condição viva ainda cita. As invariantes internas da própria
/// classificação já foram conferidas em <see cref="ConfiguracaoClassificacao.Criar"/>.
/// </summary>
internal static class RegrasDaClassificacao
{
    internal static Result<PlanoDaClassificacao> Planejar(ConfiguracaoDeclarada configuracao, ConfiguracaoClassificacao classificacao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        // As recusas de campo se acumulam (todas de uma vez para quem edita); a das etapas, que nenhum
        // campo deste comando corrige, sai depois delas porque o errors[] só carrega erro de campo.
        List<FieldError> erros = [.. EtapasDeEliminacaoRecusadas(configuracao, classificacao)];
        DomainError? erroDaEtapaDoEnem = RegrasDasEtapas.ConferirEtapaDeNotaDoEnem(configuracao.Avaliacao.Etapas, classificacao);
        erros.AddRange(RecusasDoCalculoLocalEDoDesempate(configuracao, classificacao, erroDaEtapaDoEnem));

        List<OpcaoDeclaradaFato> edicoesARetirar = EdicoesDoEnemSemClassificacaoEnem(configuracao, classificacao);
        erros.AddRange(RecusaDasEdicoesDoEnemCitadas(configuracao, edicoesARetirar));

        DomainError? erroDasEtapas = classificacao.CalculaPelosPesosPorAreaDoEnem ? erroDaEtapaDoEnem : null;
        return ResultadoDoPlano(classificacao, erros, erroDasEtapas, edicoesARetirar);
    }

    private static Result<PlanoDaClassificacao> ResultadoDoPlano(
        ConfiguracaoClassificacao classificacao,
        List<FieldError> erros,
        DomainError? erroDasEtapas,
        List<OpcaoDeclaradaFato> edicoesARetirar)
    {
        if (erros.Count > 0)
        {
            return Result<PlanoDaClassificacao>.ValidationFailure(RegrasDoDesempate.AnexarAreasAceitas(erros, classificacao));
        }

        return erroDasEtapas is not null
            ? Result<PlanoDaClassificacao>.Failure(erroDasEtapas)
            : Result<PlanoDaClassificacao>.Success(new PlanoDaClassificacao(
                classificacao, new MudancaDaConfiguracao { OpcoesDeclaradas = Troca.Retirando(edicoesARetirar) }));
    }

    // A etapa de nota do ENEM e o desempate sem quadro pedem a mesma correção, nos mesmos campos:
    // cada campo sai uma vez, com a primeira das duas causas.
    private static List<FieldError> RecusasDoCalculoLocalEDoDesempate(
        ConfiguracaoDeclarada configuracao, ConfiguracaoClassificacao classificacao, DomainError? erroDaEtapaDoEnem)
    {
        List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoes =
            RegrasDoDesempate.ConferirDesempatePorAreaContraOQuadro(RegrasDoDesempate.EmOrdem(configuracao.Avaliacao.CriteriosDesempate), classificacao);
        List<FieldError> erros = [];
        if (ErroSemCalculoLocal(classificacao, erroDaEtapaDoEnem, violacoes) is { } erroSemCalculoLocal)
        {
            erros.AddRange(CamposSemCalculoLocalDoEnem(classificacao).Select(campo => new FieldError(campo, erroSemCalculoLocal)));
        }

        erros.AddRange(DesempateForaDoQuadro(violacoes));
        return erros;
    }

    private static DomainError? ErroSemCalculoLocal(
        ConfiguracaoClassificacao classificacao,
        DomainError? erroDaEtapaDoEnem,
        List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoes)
    {
        if (!classificacao.CalculaPelosPesosPorAreaDoEnem && erroDaEtapaDoEnem is not null)
        {
            return erroDaEtapaDoEnem;
        }

        return violacoes.FirstOrDefault(static v => v.IndiceArea is null) is { Erro: { } semQuadro } ? semQuadro : null;
    }

    // As edições do ENEM só existem com classificação baseada em ENEM; uma condição viva que cite
    // uma delas ficaria sem como se satisfazer.
    private static List<FieldError> RecusaDasEdicoesDoEnemCitadas(ConfiguracaoDeclarada configuracao, List<OpcaoDeclaradaFato> edicoesARetirar)
    {
        if (edicoesARetirar.Count == 0 || !ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(configuracao, EdicaoDoEnem.Fato, []))
        {
            return [];
        }

        return [new("baseadoEmEnem", new DomainError(
            OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva,
            "Uma condição viva (exigência documental, pré-condição de campo ou regra de derivação) cita uma edição do ENEM, que deixaria de existir com a classificação sem ENEM — ajuste ou remova a condição antes."))];
    }

    private static List<FieldError> EtapasDeEliminacaoRecusadas(ConfiguracaoDeclarada configuracao, ConfiguracaoClassificacao classificacao) =>
    [
        .. classificacao.RegrasEliminacao.SelectMany((regra, indice) =>
            RecusaDaEtapaDaRegra(regra, configuracao) is { } recusa
                ? [new FieldError($"regrasEliminacao[{indice}].etapaRef", recusa)]
                : Array.Empty<FieldError>()),
    ];

    private static DomainError? RecusaDaEtapaDaRegra(RegraEliminacao regra, ConfiguracaoDeclarada configuracao) =>
        regra.Parametros.EtapaReferenciada() is { } etapaRef && !configuracao.Avaliacao.Etapas.Any(e => e.Id == etapaRef)
            ? ReferenciaAEtapaInexistente.PelaEliminacao(etapaRef)
            : RegrasDasEtapas.AusenciaNaEtapaDoEnem(regra.Parametros, configuracao.Avaliacao.Etapas)
                ? RegrasDasEtapas.AusenciaNaEtapaDoEnemRecusada()
                : null;

    // Uma recusa por critério afetado, e não por área: a classificação é a mesma para todas.
    private static IEnumerable<FieldError> DesempateForaDoQuadro(List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoesDeArea) =>
        violacoesDeArea
            .Where(static v => v.IndiceArea is not null)
            .GroupBy(static v => v.Ordem)
            .Select(static criterio => new FieldError(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(
                DesempatePorAreaEnemErrorCodes.ForaDoQuadro,
                $"O critério de desempate na ordem {criterio.Key} cita área que o quadro de pesos por área desta classificação não tem em todos os grupos: {string.Join(", ", criterio.Select(static v => v.Codigo))}.")));

    private static List<OpcaoDeclaradaFato> EdicoesDoEnemSemClassificacaoEnem(ConfiguracaoDeclarada configuracao, ConfiguracaoClassificacao classificacao) =>
        classificacao.BaseadoEmEnem
            ? []
            : [.. configuracao.Coleta.OpcoesDeclaradas.Where(static o => string.Equals(o.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal))];

    /// <summary>
    /// Os campos da classificação que impedem o cálculo local do ENEM — e com ele o quadro de pesos por
    /// área e a etapa de nota do ENEM: a declaração de ENEM e a fórmula, cada um que esteja em desacordo
    /// com <see cref="ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem"/>. Com os dois de acordo, só
    /// pode faltar o quadro, que se corrige pela resolução declarada.
    /// </summary>
    private static string[] CamposSemCalculoLocalDoEnem(ConfiguracaoClassificacao classificacao) =>
        (classificacao.BaseadoEmEnem, ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(classificacao.RegraCalculo, baseadoEmEnem: true)) switch
        {
            (false, false) => ["baseadoEmEnem", "regraCalculoCodigo"],
            (false, true) => ["baseadoEmEnem"],
            (true, false) => ["regraCalculoCodigo"],
            (true, true) => [ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem],
        };
}
