namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
    private readonly ConfiguracaoPublicada _configuracao;

    public CenarioDoEnemDeTeste(params string[] edicoesDivulgadas)
        : this(semFormularioDeInscricao: false, edicoesDivulgadas)
    {
    }

    public CenarioDoEnemDeTeste(bool semFormularioDeInscricao, params string[] edicoesDivulgadas)
    {
        EtapaDoEnem = Etapa("Nota do ENEM", "NOTA_ENEM", notaDeOrigemNoEnem: true);
        EtapaSemEnem = Etapa("Prova objetiva", "PROVA_OBJETIVA", notaDeOrigemNoEnem: false);

        ConfiguracaoPublicada configuracao = MontarConfiguracao(semFormularioDeInscricao, edicoesDivulgadas);
        (VersaoConfiguracao versao, _) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        VersaoId = versao.Id;
        Certame = DivulgacaoDeTeste.Certame(ProcessoId);
        DivulgacaoDeTeste.ConfigurarDivulgada(Processos, Codecs, versao, configuracao);
        _versaoDivulgada = versao;
        _configuracao = configuracao;
    }

    private ConfiguracaoPublicada MontarConfiguracao(bool semFormularioDeInscricao, string[] edicoesDivulgadas)
    {
        (_, ConfiguracaoPublicada modelo) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        OpcaoDeclaradaFato[] opcoes = [.. edicoesDivulgadas.Select((e, i) => OpcaoDeclaradaFato.Criar(EdicaoDoEnem.Fato, e, $"ENEM {e}", i + 1).Value!)];
        return new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. modelo.Cronograma.CronogramaFases]),
            new AvaliacaoDeclarada([EtapaDoEnem, EtapaSemEnem], [.. modelo.Avaliacao.CriteriosDesempate], ClassificacaoPelosPesosDoEnem()),
            VagasDoEnem(modelo), new ExigenciasDeclaradas([.. modelo.Exigencias.DocumentosExigidos], []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([.. modelo.Coleta.Itens], [.. modelo.Coleta.GruposColetados]), [.. modelo.Coleta.GruposColetados], [.. modelo.Coleta.Formularios.Where(f => !semFormularioDeInscricao || f.Codigo != FormularioDeTeste.FormularioDaInscricao)], [], opcoes, [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);
    }

    private VagasDeclaradas VagasDoEnem(ConfiguracaoPublicada modelo) => new(
        [
            ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(grupoAreaEnem: ("TECNOLOGICA", "Tecnológica"), ofertaCursoOrigemId: OfertaTecnologica),
            ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(grupoAreaEnem: ("HUMANISTICA_I", "Humanística I"), ofertaCursoOrigemId: OfertaHumanistica),
        ], 1, modelo.Vagas.Fada is not null, modelo.Vagas.Fada, null, modelo.Vagas.AtendimentoEspecializado);

    /// <summary>Uma retificação divulgada prorrogou as inscrições até o instante.</summary>
    public void ProrrogarInscricoesAte(DateTimeOffset fim) =>
        DivulgacaoDeTeste.ConfigurarDivulgada(Processos, Codecs, _versaoDivulgada, _configuracao, fim);

    private static ConfiguracaoClassificacao ClassificacaoPelosPesosDoEnem() =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            RegraArredondamento = ReferenciaRegra.De(RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", ProcessoSeletivoConformeBuilder.HashFixo),
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
            EscalaNotaFinal = 100m,
        }).Value!;

    /// <summary>O processo vivo não pode ser lido: o escopo é o da versão divulgada.</summary>
    public void ProcessoVivoNaoFoiConsultado() =>
        _ = Processos.DidNotReceiveWithAnyArgs().ObterComConfiguracaoAsync(default, default);

    private static EtapaProcesso Etapa(string nome, string tipo, bool notaDeOrigemNoEnem) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe(tipo), tipo, nome, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: notaDeOrigemNoEnem).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: notaDeOrigemNoEnem ? null : FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}
