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

        bool calculaPelosPesosPorArea = classificacao.CalculaPelosPesosPorAreaDoEnem;
        DomainError? erroDaEtapaDoEnem = RegrasDasEtapas.ConferirEtapaDeNotaDoEnem(estado.Etapas, classificacao);
        DomainError? erroDasEtapas = calculaPelosPesosPorArea ? erroDaEtapaDoEnem : null;
        DomainError? erroSemCalculoLocal = calculaPelosPesosPorArea ? null : erroDaEtapaDoEnem;

        List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoesDeArea =
            RegrasDoDesempate.ConferirDesempatePorAreaContraOQuadro(RegrasDoDesempate.EmOrdem(estado.CriteriosDesempate), classificacao);
        if (erroSemCalculoLocal is null && violacoesDeArea.FirstOrDefault(static v => v.IndiceArea is null) is { Erro: { } semQuadro })
        {
            erroSemCalculoLocal = semQuadro;
        }

        // A etapa de nota do ENEM e o desempate sem quadro pedem a mesma correção, nos mesmos campos:
        // cada campo sai uma vez, com a primeira das duas causas.
        if (erroSemCalculoLocal is not null)
        {
            erros.AddRange(CamposSemCalculoLocalDoEnem(classificacao).Select(campo => new FieldError(campo, erroSemCalculoLocal)));
        }

        erros.AddRange(DesempateForaDoQuadro(violacoesDeArea));

        // As edições do ENEM só existem com classificação baseada em ENEM; uma condição viva que cite
        // uma delas ficaria sem como se satisfazer.
        List<OpcaoDeclaradaFato> edicoesARetirar = EdicoesDoEnemSemClassificacaoEnem(estado, classificacao);
        if (edicoesARetirar.Count > 0 && ReferenciasVivas.ReferenciaDinamicaSeriaInvalidada(estado, EdicaoDoEnem.Fato, []))
        {
            erros.Add(new("baseadoEmEnem", new DomainError(
                OpcaoDeclaradaFatoErrorCodes.ReferenciadaPorExigenciaViva,
                "Uma condição viva (exigência documental, pré-condição de campo ou regra de derivação) cita uma edição do ENEM, que deixaria de existir com a classificação sem ENEM — ajuste ou remova a condição antes.")));
        }

        if (erros.Count > 0)
        {
            return Result<PlanoDaClassificacao>.ValidationFailure(RegrasDoDesempate.AnexarAreasAceitas(erros, classificacao));
        }

        return erroDasEtapas is not null
            ? Result<PlanoDaClassificacao>.Failure(erroDasEtapas)
            : Result<PlanoDaClassificacao>.Success(new PlanoDaClassificacao(classificacao, edicoesARetirar));
    }

    private static List<FieldError> EtapasDeEliminacaoRecusadas(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao)
    {
        List<FieldError> erros = [];
        foreach ((RegraEliminacao regra, int indice) in classificacao.RegrasEliminacao.Select(static (regra, indice) => (regra, indice)))
        {
            string campo = $"regrasEliminacao[{indice}].etapaRef";
            if (regra.Args.EtapaReferenciada() is { } etapaRef && !estado.Etapas.Any(e => e.Id == etapaRef))
            {
                erros.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
                    $"A regra de eliminação referencia a etapa {etapaRef}, que não existe neste processo (INV-B4).")));
            }
            else if (RegrasDasEtapas.AusenciaNaEtapaDoEnem(regra.Args, estado.Etapas))
            {
                erros.Add(new(campo, RegrasDasEtapas.AusenciaNaEtapaDoEnemRecusada()));
            }
        }

        return erros;
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
