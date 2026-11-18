namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using NSubstitute;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um certame divulgado com uma etapa de nota do ENEM, uma etapa sem ela e as edições declaradas, lido
/// pela versão divulgada. O processo vivo nunca é consultado: se um handler o consultasse, o teste
/// falharia.
/// </summary>
internal sealed class CenarioDoEnemDeTeste
{
    public Guid ProcessoId { get; } = Guid.CreateVersion7();

    public EtapaProcesso EtapaDoEnem { get; }

    public EtapaProcesso EtapaSemEnem { get; }

    public ICertameDivulgadoRepository Certame { get; }

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();

    /// <summary>A versão divulgada do certame, a que apura as notas.</summary>
    public Guid VersaoId { get; }

    /// <summary>Oferta do edital no grupo Tecnológica, a 1ª opção dos inscritos de teste.</summary>
    public Guid OfertaTecnologica { get; } = Guid.CreateVersion7();

    /// <summary>Oferta do edital no grupo Humanística I, a 2ª opção dos inscritos de teste.</summary>
    public Guid OfertaHumanistica { get; } = Guid.CreateVersion7();

    /// <summary>Um instante depois do encerramento das inscrições do ato de teste.</summary>
    public static DateTimeOffset DepoisDoEncerramento { get; } = DivulgacaoDeTeste.FimDoPeriodo.AddDays(1);

    private readonly VersaoConfiguracao _versaoDivulgada;
    private readonly GrafoConfiguracao _grafo;

    public CenarioDoEnemDeTeste(params string[] edicoesDivulgadas)
        : this(semFormularioDeInscricao: false, edicoesDivulgadas)
    {
    }

    public CenarioDoEnemDeTeste(bool semFormularioDeInscricao, params string[] edicoesDivulgadas)
    {
        EtapaDoEnem = Etapa("Nota do ENEM", "NOTA_ENEM", notaDeOrigemNoEnem: true);
        EtapaSemEnem = Etapa("Prova objetiva", "PROVA_OBJETIVA", notaDeOrigemNoEnem: false);

        (_, GrafoConfiguracao modelo) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        OpcaoDeclaradaFato[] opcoes =
        [
            .. edicoesDivulgadas.Select((e, i) => OpcaoDeclaradaFato.Criar(EdicaoDoEnem.Fato, e, $"ENEM {e}", i + 1).Value!),
        ];
        GrafoConfiguracao grafo = new(
            [EtapaDoEnem, EtapaSemEnem], modelo.AtendimentoEspecializado,
            [
                ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(grupoAreaEnem: ("TECNOLOGICA", "Tecnológica"), ofertaCursoOrigemId: OfertaTecnologica),
                ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(grupoAreaEnem: ("HUMANISTICA_I", "Humanística I"), ofertaCursoOrigemId: OfertaHumanistica),
            ],
            modelo.BonusRegional, [.. modelo.CriteriosDesempate], ClassificacaoPelosPesosDoEnem(), [.. modelo.CronogramaFases], [.. modelo.DocumentosExigidos], [], null,
            fatosColetados: [.. modelo.FatosColetados], formularios: [.. modelo.Formularios.Where(f => !semFormularioDeInscricao || f.Finalidade != FinalidadeFormulario.Inscricao)], gruposColetados: [.. modelo.GruposColetados],
            opcoesDeclaradas: opcoes, numeroDeOpcoesDeCurso: 1);

        (VersaoConfiguracao versao, _) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        VersaoId = versao.Id;
        Certame = DivulgacaoDeTeste.Certame(ProcessoId);
        DivulgacaoDeTeste.ConfigurarDivulgada(Processos, Codecs, versao, grafo);
        _versaoDivulgada = versao;
        _grafo = grafo;
    }

    /// <summary>Uma retificação divulgada prorrogou as inscrições até o instante.</summary>
    public void ProrrogarInscricoesAte(DateTimeOffset fim) =>
        DivulgacaoDeTeste.ConfigurarDivulgada(Processos, Codecs, _versaoDivulgada, _grafo, fim);

    private static ConfiguracaoClassificacao ClassificacaoPelosPesosDoEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.FormulaMediaPonderada, "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            RegraArredondamento = ReferenciaRegra.De(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

    /// <summary>O processo vivo não pode ser lido: o escopo é o da versão divulgada.</summary>
    public void ProcessoVivoNaoFoiConsultado() =>
        _ = Processos.DidNotReceiveWithAnyArgs().ObterComConfiguracaoAsync(default, default);

    private static EtapaProcesso Etapa(string nome, string tipo, bool notaDeOrigemNoEnem) =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe(tipo), tipo, nome, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: notaDeOrigemNoEnem).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: notaDeOrigemNoEnem ? null : FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}
