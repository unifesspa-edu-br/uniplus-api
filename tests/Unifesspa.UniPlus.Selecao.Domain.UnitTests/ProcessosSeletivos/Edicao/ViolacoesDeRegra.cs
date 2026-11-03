namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Para cada operação do protocolo comum que tem regra de negócio, a chamada que a viola, o código
/// da recusa e a dimensão que a recusa não pode mexer. As operações sem regra declaram o motivo.
/// </summary>
internal sealed record ViolacaoDeRegra(
    Func<ProcessoSeletivo, PrecondicaoIfMatch, Result> Chamar,
    string Codigo,
    Func<ProcessoSeletivo, object?[]> Dimensao,
    Action<ProcessoSeletivo>? Preparar = null)
{
    internal static readonly IReadOnlyDictionary<string, string> SemRegra = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(ProcessoSeletivo.DefinirConfiguracaoDivulgacao)] = "aceita qualquer divulgação, inclusive a ausente, que é o padrão minimizado",
        [nameof(ProcessoSeletivo.DefinirTaxaInscricao)] = "a fábrica da taxa valida a forma e a ausência é um estado declarável",
        [nameof(ProcessoSeletivo.DefinirCascataRemanejamento)] = "a fábrica da cascata valida a forma e a cobertura é conferida na publicação",
        [nameof(ProcessoSeletivo.DefinirLocalidade)] = "a atribuição é incondicional: a localidade já vem validada pela fábrica",
    };

    internal static readonly IReadOnlyDictionary<string, ViolacaoDeRegra> PorOperacao = new Dictionary<string, ViolacaoDeRegra>(StringComparer.Ordinal)
    {
        [nameof(ProcessoSeletivo.DefinirOpcoesDeclaradas)] = new(
            (p, pre) => p.DefinirOpcoesDeclaradas("FATO_DE_TESTE", [], pre),
            OpcaoDeclaradaFatoErrorCodes.ListaVazia,
            static p => [.. p.OpcoesDeclaradas]),
        [nameof(ProcessoSeletivo.DefinirEtapas)] = new(
            (p, pre) => p.DefinirEtapas([EtapaSoEliminatoria()], pre),
            "ProcessoSeletivo.NenhumaEtapaComponeNota",
            static p => [.. p.Etapas]),
        [nameof(ProcessoSeletivo.DefinirOfertaAtendimento)] = new(
            (p, pre) => p.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, pre),
            "ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva",
            static p => [p.OfertaAtendimento],
            Preparar: ExigenciaQueCitaACondicaoPcd),
        [nameof(ProcessoSeletivo.DefinirDistribuicaoVagas)] = new(
            (p, pre) => p.DefinirDistribuicaoVagas([], FatosDeModalidadeDeTeste.DoCatalogo, pre),
            "ProcessoSeletivo.DistribuicaoVagasVazia",
            static p => [.. p.DistribuicaoVagas]),
        [nameof(ProcessoSeletivo.DefinirOpcoesDeCurso)] = new(
            (p, pre) => p.DefinirOpcoesDeCurso(3, pre),
            ProcessoSeletivoErrorCodes.OpcoesDeCursoInvalido,
            static p => [p.NumeroDeOpcoesDeCurso]),
        [nameof(ProcessoSeletivo.DefinirBonusRegional)] = new(
            (p, pre) => p.DefinirBonusRegional(aplica: true, bonus: null, pre),
            ProcessoSeletivoErrorCodes.BonusRegionalAplicaSemConfiguracao,
            static p => [p.AplicaBonusRegional, p.BonusRegional]),
        [nameof(ProcessoSeletivo.DefinirCriteriosDesempate)] = new(
            (p, pre) => p.DefinirCriteriosDesempate([CriterioMaiorIdade(1), CriterioMaiorIdade(1)], pre),
            "ProcessoSeletivo.OrdemDesempateDuplicada",
            static p => [.. p.CriteriosDesempate]),
        [nameof(ProcessoSeletivo.DefinirClassificacao)] = new(
            (p, pre) => p.DefinirClassificacao(ClassificacaoQueEliminaPorEtapaInexistente(), pre),
            "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
            static p => [p.Classificacao]),
        [nameof(ProcessoSeletivo.DefinirFormulario)] = new(
            (p, pre) =>
            {
                FormularioProcesso formulario = p.Formularios.Single(f => f.Finalidade == FinalidadeFormulario.Inscricao);
                return p.DefinirFormulario(FinalidadeFormulario.Inscricao, Guid.CreateVersion7(), formulario.Titulo, [.. formulario.Etapas], pre);
            },
            "FormularioProcesso.FaseForaDoCronograma",
            static p => [.. p.Formularios.SelectMany(static f => (object?[])[f.Finalidade, f.FaseId, f.Titulo, .. f.Etapas])]),
        [nameof(ProcessoSeletivo.DefinirTermosDoFormulario)] = new(
            (p, pre) => p.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [], pre),
            FormularioProcessoErrorCodes.FormularioInexistente,
            static p => [.. p.TermosExigidos]),
        [nameof(ProcessoSeletivo.DefinirCronogramaFases)] = new(
            (p, pre) => p.DefinirCronogramaFases([ProcessoConformeFactory.FaseConforme(), ProcessoConformeFactory.FaseConforme()], [], pre),
            "ProcessoSeletivo.OrdemFaseDuplicada",
            static p => [.. p.CronogramaFases]),
        [nameof(ProcessoSeletivo.DefinirDocumentosExigidos)] = new(
            (p, pre) => p.DefinirDocumentosExigidos([NoExigencia.CriarFolha(Exigencia(Guid.CreateVersion7()), 0).Value!], pre),
            "DocumentoExigido.FaseNaoPertenceAoProcesso",
            static p => [.. p.NosExigencia]),
        [nameof(ProcessoSeletivo.DefinirFatosColetados)] = new(
            (p, pre) => p.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [], pre),
            "RascunhoRetificacao.FatoJaColetadoRemovido",
            static p => [.. p.Campos]),
        [nameof(ProcessoSeletivo.DefinirRegrasDerivacao)] = new(
            (p, pre) => p.DefinirRegrasDerivacao([Derivado("D1"), Derivado("D1")], pre),
            "ConfiguracaoDerivacaoFato.CodigoFatoDuplicado",
            static p => [.. p.RegrasDerivacao]),
        [nameof(ProcessoSeletivo.DefinirReferenciaTemporalFatos)] = new(
            (p, pre) => p.DefinirReferenciaTemporalFatos(ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, Guid.CreateVersion7()).Value!, pre),
            "ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso",
            static p => [p.ReferenciaTemporalFatos]),
        [nameof(ProcessoSeletivo.DefinirIdentificadorLegivel)] = new(
            (p, pre) =>
            {
                if (p.Rascunho is { } sessao)
                {
                    typeof(RascunhoRetificacao)
                        .GetProperty(nameof(RascunhoRetificacao.VersaoBaseComIdentificadorLegivel))!
                        .SetValue(sessao, true);
                }

                return p.DefinirIdentificadorLegivel(IdentificadoresDeTeste.Novo(), pre);
            },
            ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel,
            static p => [p.IdentificadorLegivel]),
        [nameof(ProcessoSeletivo.DefinirAlgoritmoContagemPrazo)] = new(
            (p, pre) => p.DefinirAlgoritmoContagemPrazo(null!, pre),
            "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
            static p => [p.AlgoritmoContagemPrazo]),
    };

    private static EtapaProcesso EtapaSoEliminatoria() => EtapaProcesso.Criar(
        "Entrevista", CaraterEtapa.Eliminatoria,
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        peso: null, notaMinima: 5m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!;

    private static CriterioDesempate CriterioMaiorIdade(int ordem) => CriterioDesempate.Criar(
        ordem,
        ReferenciaRegra.Criar(CriterioDesempateCodigo.MaiorIdade, "v1", new string('f', 64)).Value!,
        new ArgsDesempateMaiorIdade()).Value!;

    // A regra de eliminação cita uma etapa que o processo não tem (INV-B4).
    private static ConfiguracaoClassificacao ClassificacaoQueEliminaPorEtapaInexistente() => ConfiguracaoClassificacao.Criar(
        ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!,
        ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('b', 64)).Value!,
        2,
        ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)).Value!,
        [
            RegraEliminacao.Criar(
                ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", new string('d', 64)).Value!,
                new ArgsElimNotaMinimaEtapa(Guid.CreateVersion7(), 4m)).Value!,
        ],
        false,
        null,
        [],
        escalaNotaFinal: 100m).Value!;

    private static ConfiguracaoDerivacaoFato Derivado(string codigo) =>
        ConfiguracaoDerivacaoFato.Criar(codigo, [RegraDerivacaoConfigurada.Criar(0, "AC", []).Value!]).Value!;

    private static DocumentoExigido Exigencia(Guid faseId, params CondicaoGatilho[] condicoes) => DocumentoExigido.Criar(
        faseId, Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
        condicoes.Length == 0 ? Aplicabilidade.Geral : Aplicabilidade.Condicional,
        obrigatorio: true, consequenciaIndeferimento: null, condicoes: condicoes,
        basesLegais: [DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!],
        idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
        finalidade: FinalidadeFormulario.Inscricao).Value!;

    // Uma exigência viva cita a condição de atendimento PCD: redefinir a oferta sem ela deixaria a condição sem como se satisfazer.
    private static void ExigenciaQueCitaACondicaoPcd(ProcessoSeletivo processo)
    {
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")], [], []).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        CondicaoGatilho condicao = CondicaoGatilho.Criar(0, "CONDICAO_ATENDIMENTO", Operador.Igual, JsonSerializer.SerializeToElement("PCD")).Value!;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(Exigencia(processo.CronogramaFases.First().Id, condicao), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
    }
}
