namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// As recusas de trocar a classificação que dependem de outras dimensões do processo: as etapas que
/// as regras de eliminação citam, a etapa de nota do ENEM, o quadro de pesos por área que o desempate
/// exige e as edições do ENEM que uma condição viva ainda cita. As invariantes internas da própria
/// classificação já foram conferidas em <see cref="ConfiguracaoClassificacao.Criar"/>.
/// </summary>
internal static class RegrasDaClassificacao
{
    internal static Result<PlanoDaClassificacao> Planejar(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao)
    {
        ArgumentNullException.ThrowIfNull(estado);

        // As recusas de campo se acumulam (todas de uma vez para quem edita); a das etapas, que nenhum
        // campo deste comando corrige, sai depois delas porque o errors[] só carrega erro de campo.
        List<FieldError> erros = [.. EtapasDeEliminacaoRecusadas(estado, classificacao)];
        DomainError? erroDaEtapaDoEnem = RegrasDasEtapas.ConferirEtapaDeNotaDoEnem(estado.Etapas, classificacao);
        erros.AddRange(RecusasDoCalculoLocalEDoDesempate(estado, classificacao, erroDaEtapaDoEnem));

        List<OpcaoDeclaradaFato> edicoesARetirar = EdicoesDoEnemSemClassificacaoEnem(estado, classificacao);
        erros.AddRange(RecusaDasEdicoesDoEnemCitadas(estado, edicoesARetirar));

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
        EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao, DomainError? erroDaEtapaDoEnem)
    {
        List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoes =
            RegrasDoDesempate.ConferirDesempatePorAreaContraOQuadro(RegrasDoDesempate.EmOrdem(estado.CriteriosDesempate), classificacao);
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
    private static List<FieldError> RecusaDasEdicoesDoEnemCitadas(EstadoDaConfiguracao estado, List<OpcaoDeclaradaFato> edicoesARetirar)
    {
        if (edicoesARetirar.Count == 0 || !ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, EdicaoDoEnem.Fato, []))
        {
            return [];
        }

        return [new("baseadoEmEnem", new DomainError(
            OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva,
            "Uma condição viva (exigência documental, pré-condição de campo ou regra de derivação) cita uma edição do ENEM, que deixaria de existir com a classificação sem ENEM — ajuste ou remova a condição antes."))];
    }

    internal static DomainError EtapaRefEliminacaoInexistente(Guid etapaRef) => new(
        "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
        $"A regra de eliminação referencia a etapa {etapaRef}, que não existe neste processo.");

    private static List<FieldError> EtapasDeEliminacaoRecusadas(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao)
    {
        List<FieldError> erros = [];
        foreach ((RegraEliminacao regra, int indice) in classificacao.RegrasEliminacao.Select(static (regra, indice) => (regra, indice)))
        {
            if (RecusaDaEtapaDaEliminacao(regra, estado.Etapas) is { } recusa)
            {
                erros.Add(new($"regrasEliminacao[{indice}].etapaRef", recusa));
            }
        }

        return erros;
    }

    /// <summary>
    /// A recusa da etapa que a regra de eliminação cita: a etapa não existe, ou é a etapa do ENEM,
    /// cuja ausência já tem regra própria. A primeira que vale.
    /// </summary>
    internal static DomainError? RecusaDaEtapaDaEliminacao(RegraEliminacao regra, IReadOnlyCollection<EtapaProcesso> etapas)
    {
        if (regra.Args.EtapaReferenciada() is { } etapaRef && !etapas.Any(e => e.Id == etapaRef))
        {
            return EtapaRefEliminacaoInexistente(etapaRef);
        }

        return RegrasDasEtapas.AusenciaNaEtapaDoEnem(regra.Args, etapas) ? RegrasDasEtapas.AusenciaNaEtapaDoEnemRecusada() : null;
    }

    // Uma recusa por critério afetado, e não por área: a classificação é a mesma para todas.
    private static IEnumerable<FieldError> DesempateForaDoQuadro(List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoesDeArea) =>
        violacoesDeArea
            .Where(static v => v.IndiceArea is not null)
            .GroupBy(static v => v.Ordem)
            .Select(static criterio => new FieldError(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError(
                DesempatePorAreaEnemErrorCodes.ForaDoQuadro,
                $"O critério de desempate na ordem {criterio.Key} cita área que o quadro de pesos por área desta classificação não tem em todos os grupos: {string.Join(", ", criterio.Select(static v => v.Codigo))}.")));

    private static List<OpcaoDeclaradaFato> EdicoesDoEnemSemClassificacaoEnem(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao) =>
        classificacao.BaseadoEmEnem
            ? []
            : [.. estado.OpcoesDeclaradas.Where(static o => string.Equals(o.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal))];

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
