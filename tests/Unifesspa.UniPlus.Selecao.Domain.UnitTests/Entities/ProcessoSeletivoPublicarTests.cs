namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

public sealed class ProcessoSeletivoPublicarTests
{
    [Fact(DisplayName = "Publicacao_AtomicaSituacaoESnapshot — processo conforme publica, congela a versão 1 e transita a situação")]
    public void Publicar_ProcessoConforme_TransitaSituacaoECongelaVersao()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DadosEdital dados = NovosDados();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital(dados));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Publicado);

        VersaoConfiguracao versao = resultado.Value!;
        versao.NumeroVersao.Should().Be(1, "a abertura abre a cadeia de configuração");
        versao.ProcessoSeletivoId.Should().Be(processo.Id);
        versao.AtoCriadorHash.Should().Be(HashFixo, "o hash do documento publicado é a metade-hash da referência por valor ao ato");
        versao.AtoCriadorId.Should().NotBeEmpty("a raiz decide o id do ato — a versão precisa referenciá-lo antes de o ato existir (ADR-0108)");
        versao.AtoCriadorRetificaId.Should().BeNull("a abertura não emenda ato algum: retificar é uma relação, e aqui não há a quem se relacionar");
    }

    [Fact(DisplayName = "Publicar — o id do ato é um Guid v7 ancorado no MESMO instante da vigência da versão que ele cria")]
    public void Publicar_IdDoAto_AncoradoNoInstanteDaVersao()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DateTimeOffset instante = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital() with { Relogio = new RelogioFixo(instante) });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        VersaoConfiguracao versao = resultado.Value!;

        AfirmarQueOIdDoAtoNasceDoInstanteDaVersao(versao, instante);
    }

    private static void AfirmarQueOIdDoAtoNasceDoInstanteDaVersao(VersaoConfiguracao versao, DateTimeOffset instante)
    {
        versao.VigenteAPartirDe.Should().Be(instante);
        versao.AtoCriadorId.Version.Should().Be(7, "Guid v7 — o id carrega o instante, e é ordenável por ele (ADR-0032)");

        // O id do ato e a vigência da versão descrevem o mesmo fato: se cada um viesse de
        // uma leitura de relógio própria, discordariam. O timestamp embutido no v7 tem
        // resolução de milissegundo — é a granularidade em que a igualdade é verificável.
        DateTimeOffset instanteNoId = InstanteDoGuidV7(versao.AtoCriadorId);
        instanteNoId.Should().Be(
            new DateTimeOffset(instante.UtcDateTime.AddTicks(-(instante.UtcDateTime.Ticks % TimeSpan.TicksPerMillisecond)), TimeSpan.Zero),
            "uma leitura do relógio por operação atômica — o ato e a versão que ele cria nascem do mesmo instante");
    }

    [Fact(DisplayName = "Publicacao_AtomicaSituacaoESnapshot — evento carrega os identificadores forenses completos")]
    public void Publicar_ProcessoConforme_EmiteEventoComIdentificadoresCompletos()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DadosEdital dados = NovosDados();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital(dados));

        resultado.IsSuccess.Should().BeTrue();
        Domain.Events.ProcessoPublicadoEvent evento = processo.DomainEvents
            .OfType<Domain.Events.ProcessoPublicadoEvent>().Should().ContainSingle().Subject;

        AfirmarEventoComOsIdentificadoresDaVersao(evento, processo, resultado.Value!);
    }

    private static void AfirmarEventoComOsIdentificadoresDaVersao(Domain.Events.ProcessoPublicadoEvent evento, ProcessoSeletivo processo, VersaoConfiguracao versao)
    {
        evento.ProcessoSeletivoId.Should().Be(processo.Id);
        // EditalId é o nome histórico do membro — contrato do envelope durável e do schema
        // Avro (ver ProcessoPublicadoEvent). O VALOR sempre foi o do ato criador, e continua.
        evento.EditalId.Should().Be(versao.AtoCriadorId);
        evento.SnapshotPublicacaoId.Should().Be(versao.Id);
        evento.HashConfiguracao.Should().Be(versao.HashConfiguracao);
        evento.HashEdital.Should().Be(HashFixo);
        evento.OccurredOn.Should().Be(
            versao.VigenteAPartirDe,
            "o fato ocorreu no instante do SISTEMA — o mesmo que ordena as versões (ADR-0104)");
    }

    [Fact(DisplayName = "Publicacao_RecusaSemParametrosObrigatorios — processo sem etapas recusa com checklist de pendências (CA-03)")]
    public void Publicar_SemEtapas_RecusaComConformidadeInsuficiente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS incompleto", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        // Nenhuma dimensão obrigatória definida — Etapas/Atendimento/Distribuição/Classificação ausentes.

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        processo.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho, "publicação recusada não transita a situação");
        processo.DequeueDomainEvents().Should().BeEmpty("nada é enfileirado numa publicação recusada");
    }

    [Fact(DisplayName = "Lifecycle_TransicaoInvalidaRecusada — publicar processo já publicado é recusado (CA-09)")]
    public void Publicar_ProcessoJaPublicado_RecusaTransicaoInvalida()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(CenarioDoEdital())
            .IsSuccess.Should().BeTrue();
        processo.DequeueDomainEvents();

        Result<VersaoConfiguracao> segundaTentativa = processo.Publicar(CenarioDoEdital());

        segundaTentativa.IsFailure.Should().BeTrue();
        segundaTentativa.Error!.Code.Should().Be("ProcessoSeletivo.TransicaoInvalida");
        processo.DequeueDomainEvents().Should().BeEmpty("a segunda tentativa não emite ato nem versão");
    }

    [Theory(DisplayName = "PosPublicacao_MutacaoBloqueada_422 — todo Definir* recusa mutação após publicação (CA-04)")]
    [InlineData("etapas")]
    [InlineData("atendimentoEspecializado")]
    [InlineData("distribuicaoVagas")]
    [InlineData("bonusRegional")]
    [InlineData("criteriosDesempate")]
    [InlineData("classificacao")]
    [InlineData("cronogramaFases")]
    [InlineData("documentosExigidos")]
    public void DefinirX_ProcessoPublicado_RecusaMutacao(string dimensao)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        processo.Publicar(CenarioDoEdital())
            .IsSuccess.Should().BeTrue();

        Result resultado = MutacaoDaDimensao(processo, dimensao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    private static ConfiguracaoClassificacao ClassificacaoImportada() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
        RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
    }).Value!;

    private static Result MutacaoDaDimensao(ProcessoSeletivo processo, string dimensao) =>
        dimensao switch
        {
            "etapas" => processo.DefinirEtapas([EtapaDaProvaObjetiva("Nova Etapa", CaraterEtapa.Classificatoria, 1m)]),
            "atendimentoEspecializado" => processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!),
            "distribuicaoVagas" => processo.DefinirDistribuicaoVagas([], FatosDeModalidadeDeTeste.DoCatalogo),
            "bonusRegional" => processo.DefinirBonusRegional(aplica: false, bonus: null),
            "criteriosDesempate" => processo.DefinirCriteriosDesempate([]),
            "classificacao" => processo.DefinirClassificacao(ClassificacaoImportada()),
            "cronogramaFases" => processo.DefinirCronogramaFases([FaseConforme()], []),
            "documentosExigidos" => processo.DefinirDocumentosExigidos([]),
            _ => throw new InvalidOperationException("Dimensão desconhecida."),
        };

    /// <summary>
    /// Lê o timestamp Unix em milissegundos dos 48 bits mais significativos de um Guid v7
    /// (RFC 9562 §5.7). Existe para provar que o id do ato nasce do MESMO instante que a
    /// vigência da versão — não para o domínio usá-lo de volta.
    /// </summary>
    private static DateTimeOffset InstanteDoGuidV7(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);

        long milissegundos = 0;
        for (int i = 0; i < 6; i++)
        {
            milissegundos = (milissegundos << 8) | bytes[i];
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milissegundos);
    }

    /// <summary>Relógio fixo — o teste precisa de um instante determinístico, não de um TimeProvider real.</summary>
    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
