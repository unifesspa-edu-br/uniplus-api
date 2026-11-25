namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class TestesDaConsultaDosIndicadoresDoProcesso
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid UnidadeId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Agora = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CodigoIndicadorDoProcesso.Inscricoes, "INSCRICOES")]
    [InlineData(CodigoIndicadorDoProcesso.Inscritos, "INSCRITOS")]
    [InlineData(CodigoIndicadorDoProcesso.Rascunhos, "RASCUNHOS")]
    public void CodigoDoIndicador_ValorDeclarado_RetornaCodigoDoContrato(CodigoIndicadorDoProcesso indicador, string codigo) =>
        indicador.Codigo().Should().Be(codigo);

    [Fact(DisplayName = "Processo publicado retorna o contrato exato e consulta as contagens")]
    public async Task Publicado_RetornaContratoExato()
    {
        Cenario cenario = CriarCenario();
        cenario.Leitor.LerAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new ContagensDoProcesso(12, 9, 4));

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConsultadoEm.Should().Be(Agora);
        resultado.Value.ConsultadoEm.Offset.Should().Be(TimeSpan.Zero);
        resultado.Value.Should().BeEquivalentTo(ContratoEsperado, options => options.WithStrictOrdering());
        await cenario.Leitor.Received(1).LerAsync(ProcessoId, Arg.Any<CancellationToken>());
        await cenario.Verificador.Received(1).ExigirAsync(
            Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Rascunho autorizado responde com zeros sem consultar inscrições")]
    public async Task RascunhoComPermissaoDeConfigurar_RetornaZeros()
    {
        Cenario cenario = CriarCenario(estaEmRascunho: true);

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Indicadores.Select(item => item.Valor).Should().Equal(0L, 0L, 0L);
        resultado.Value.Indicadores[2].Descricao.Should().Be("Inscrições ainda não enviadas");
        await cenario.Leitor.DidNotReceiveWithAnyArgs().LerAsync(default, default);
        await ConferirPermissaoAsync(cenario.Verificador, PermissaoSelecao.ProcessosConfigurar);
    }

    [Fact(DisplayName = "Processo inexistente não consulta o ponto de decisão")]
    public async Task ProcessoInexistente_Retorna404DeDominio()
    {
        Cenario cenario = CriarCenario(estaEmRascunho: true);
        cenario.Processos.ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns((EstadoDoProcesso?)null);

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.Error!.Code.Should().Be(AcessoAoProcesso.NaoEncontrado(ProcessoId).Code);
        await cenario.Verificador.DidNotReceiveWithAnyArgs()
            .ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "Rascunho sem permissão de configurar é ocultado como não encontrado")]
    public async Task RascunhoSemAcesso_Retorna404DeDominio()
    {
        Cenario cenario = CriarCenario(estaEmRascunho: true, acesso: ResultadoDoAcesso.Negado);

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.Error!.Code.Should().Be(AcessoAoProcesso.NaoEncontrado(ProcessoId).Code);
        await cenario.Leitor.DidNotReceiveWithAnyArgs().LerAsync(default, default);
        await ConferirPermissaoAsync(cenario.Verificador, PermissaoSelecao.ProcessosConfigurar);
    }

    [Fact(DisplayName = "Processo publicado sem permissão de acompanhar é ocultado como inexistente")]
    public async Task PublicadoSemPermissaoDeAcompanhar_RetornaErroDeProcessoInexistente()
    {
        Cenario cenario = CriarCenario(acesso: ResultadoDoAcesso.Negado);
        Cenario inexistente = CriarCenario();
        inexistente.Processos.ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns((EstadoDoProcesso?)null);

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);
        Result<IndicadoresDoProcessoDto> resultadoInexistente = await ExecutarAsync(inexistente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().BeEquivalentTo(resultadoInexistente.Error);
        await cenario.Verificador.Received(1).ExigirAsync(PermissaoSelecao.ProcessosAcompanhar.Requisito,
            AcessoAoProcesso.ResourceDoProcesso(UnidadeId, ProcessoId), Arg.Any<CancellationToken>());
        await ConferirAusenciaDeLeiturasAdicionaisAsync(cenario);
    }

    [Theory(DisplayName = "Descrição dos rascunhos acompanha os limites da janela divulgada")]
    [InlineData(-1, 1, "Em preenchimento")]
    [InlineData(1, 2, "Inscrições ainda não enviadas")]
    [InlineData(-2, -1, "Não enviados")]
    public async Task JanelaDivulgada_ClassificaDescricao(int diasDoInicio, int diasDoFim, string esperada)
    {
        Cenario cenario = CriarCenario();
        ConfigurarVersaoDivulgada(cenario, Agora.AddDays(diasDoInicio), Agora.AddDays(diasDoFim));

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.Value!.Indicadores[2].Descricao.Should().Be(esperada);
    }

    [Fact(DisplayName = "Retificação divulgada mantém os rascunhos em preenchimento durante a janela prorrogada")]
    public async Task RetificacaoProrrogandoInscricoes_JanelaDivulgadaDeterminaDescricao()
    {
        Cenario cenario = CriarCenario();
        Guid atoDaRetificacao = ConfigurarRetificacaoDivulgada(cenario, Agora.AddDays(1));

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.Value!.Indicadores[2].Descricao.Should().Be("Em preenchimento");
        await cenario.Processos.Received(1).ObterVersoesPorAtoCriadorAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(atoDaRetificacao)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ausência de versão ou formulário usa a descrição padrão")]
    public async Task SemVersaoOuFormulario_UsaDescricaoPadrao()
    {
        Cenario cenario = CriarCenario();

        Result<IndicadoresDoProcessoDto> semVersao = await ExecutarAsync(cenario);
        ConfigurarVersaoDivulgada(cenario, Agora.AddDays(-1), Agora.AddDays(1), semFormulario: true);
        Result<IndicadoresDoProcessoDto> semFormulario = await ExecutarAsync(cenario);

        semVersao.Value!.Indicadores[2].Descricao.Should().Be("Inscrições ainda não enviadas");
        semFormulario.Value!.Indicadores[2].Descricao.Should().Be("Inscrições ainda não enviadas");
    }

    [Fact(DisplayName = "Falha ao reidratar configuração congelada não vira resposta de sucesso")]
    public async Task FalhaDeReidratacao_PropagaErro()
    {
        Cenario cenario = CriarCenario();
        VersaoConfiguracao versao = ConfigurarVersaoDivulgada(cenario, Agora.AddDays(-1), Agora.AddDays(1));
        cenario.Codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Failure(
            new DomainError("Snapshot.Ilegivel", "Falha na leitura.")));

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.Error!.Code.Should().Be("Snapshot.Ilegivel");
    }

    [Fact(DisplayName = "Servidor inscrito recebe impedimento e a consulta de contagens não é executada")]
    public async Task ServidorInscrito_PropagaImpedimento()
    {
        Cenario cenario = CriarCenario(acesso: ResultadoDoAcesso.Impedido);

        Result<IndicadoresDoProcessoDto> resultado = await ExecutarAsync(cenario);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ImpedidoPorInscricaoNoProcesso");
        await ConferirAusenciaDeLeiturasAdicionaisAsync(cenario);
    }

    [Fact(DisplayName = "Falha na leitura das contagens não vira zeros nem resposta parcial")]
    public async Task FalhaDoReader_PropagaErro()
    {
        Cenario cenario = CriarCenario();
        cenario.Leitor.LerAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns<Task<ContagensDoProcesso>>(_ => throw new InvalidOperationException("Falha de leitura"));

        Func<Task> executar = () => ExecutarAsync(cenario);

        await executar.Should().ThrowAsync<InvalidOperationException>().WithMessage("Falha de leitura");
    }

    private static Task<Result<IndicadoresDoProcessoDto>> ExecutarAsync(Cenario cenario) =>
        ObterIndicadoresDoProcessoQueryHandler.Handle(
            new ObterIndicadoresDoProcessoQuery(ProcessoId),
            cenario.Processos,
            cenario.Leitor,
            cenario.Verificador,
            cenario.Descricao,
            new RelogioFixo(Agora),
            CancellationToken.None);

    private static async Task ConferirAusenciaDeLeiturasAdicionaisAsync(Cenario cenario)
    {
        await cenario.Processos.Received(1).ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>());
        cenario.Processos.ReceivedCalls().Should().ContainSingle();
        await cenario.Leitor.DidNotReceiveWithAnyArgs().LerAsync(default, default);
        await cenario.Divulgado.DidNotReceiveWithAnyArgs().ObterParaLeituraAsync(default, default);
        await cenario.Processos.DidNotReceiveWithAnyArgs().ObterVersoesPorAtoCriadorAsync(default!, default);
    }

    private static async Task ConferirPermissaoAsync(IVerificadorDeAcesso verificador, PermissaoSelecao permissao) =>
        await verificador.Received(1).ExigirAsync(
            permissao.Requisito, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());

    private static Cenario CriarCenario(
        bool estaEmRascunho = false,
        ResultadoDoAcesso acesso = ResultadoDoAcesso.Permitido)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new EstadoDoProcesso(UnidadeId, estaEmRascunho ? SituacaoDoProcessoSeletivo.Rascunho : SituacaoDoProcessoSeletivo.Publicado, JaPublicado: !estaEmRascunho));
        ICertameDivulgadoRepository divulgado = Substitute.For<ICertameDivulgadoRepository>();
        IIndicadoresDoProcessoReader leitor = Substitute.For<IIndicadoresDoProcessoReader>();
        leitor.LerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new ContagensDoProcesso(0, 0, 0));
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(acesso);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        DescricaoDosRascunhos descricao = new(divulgado, processos, codecs);
        return new(processos, divulgado, leitor, verificador, codecs, descricao);
    }

    private static VersaoConfiguracao ConfigurarVersaoDivulgada(
        Cenario cenario,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        bool semFormulario = false)
    {
        Guid ato = Guid.CreateVersion7();
        ConfigurarCertame(cenario, ato);
        VersaoConfiguracao versao = NovaVersao(ato);
        ConfigurarConfiguracaoCongelada(cenario, versao, inicio, fim, semFormulario);
        cenario.Processos.ObterVersoesPorAtoCriadorAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(ato)), Arg.Any<CancellationToken>())
            .Returns([versao]);
        return versao;
    }

    private static Guid ConfigurarRetificacaoDivulgada(Cenario cenario, DateTimeOffset fim)
    {
        Guid atoOriginal = Guid.CreateVersion7();
        Guid atoRetificacao = Guid.CreateVersion7();
        ConfigurarCertame(cenario, atoRetificacao);
        VersaoConfiguracao versao = NovaVersaoRetificada(atoOriginal, atoRetificacao);
        ConfigurarConfiguracaoCongelada(cenario, versao, Agora.AddDays(-2), fim);
        ConfigurarVersaoLida(cenario, versao, atoRetificacao);
        return atoRetificacao;
    }

    private static VersaoConfiguracao NovaVersaoRetificada(Guid atoOriginal, Guid atoRetificacao) =>
        VersaoConfiguracao.Suceder(
            NovaVersao(atoOriginal),
            new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), "1.1", "SHA-256", new string('b', 64)),
            atoRetificacao,
            "ator",
            Agora);

    private static void ConfigurarVersaoLida(Cenario cenario, VersaoConfiguracao versao, Guid ato) =>
        cenario.Processos.ObterVersoesPorAtoCriadorAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(ato)), Arg.Any<CancellationToken>())
            .Returns([versao]);

    private static void ConfigurarCertame(Cenario cenario, Guid ato)
    {
        CertameDivulgado certame = CertameDivulgado.Criar(
            ProcessoId,
            1,
            ato,
            new string('a', 64),
            "1",
            new FacetasDoCertameDivulgado("001/2026", "Processo de indicadores", "001/2026", [], Agora, Agora.AddDays(10)),
            "{}",
            Agora);
        cenario.Divulgado.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(certame);
    }

    private static VersaoConfiguracao NovaVersao(Guid ato) => VersaoConfiguracao.Abrir(
        ProcessoId,
        new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), "1.1", "SHA-256", new string('a', 64)),
        ato,
        "ator",
        Agora);

    private static void ConfigurarConfiguracaoCongelada(
        Cenario cenario,
        VersaoConfiguracao versao,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        bool semFormulario = false)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("Processo de indicadores");
        ConfiguracaoPublicada configuracao = ConfiguracaoDoProcesso(processo, semFormulario);
        EnvelopeReidratado envelope = new(
            configuracao,
            DadosEdital.Criar("001/2026", inicio, fim, Guid.CreateVersion7()).Value!,
            new string('a', 64),
            "America/Belem",
            retificacao: null,
            conformidade: null,
            dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada());
        cenario.Codecs.LeAVersao(versao.SchemaVersion).Returns(true);
        cenario.Codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelope));
    }

    private static ConfiguracaoPublicada ConfiguracaoDoProcesso(ProcessoSeletivo processo, bool semFormulario)
    {
        ConfiguracaoPublicada configuracao = ConfiguracaoPublicadaDeTeste.DoProcesso(processo);
        return semFormulario
            ? ConfiguracaoPublicadaDeTeste.ComColeta(configuracao, new ColetaDeclarada([], [], [], [], [], [], null))
            : configuracao;
    }

    private static readonly IndicadoresDoProcessoDto ContratoEsperado = new(
        ProcessoId,
        Agora,
        [
            new("INSCRICOES", 12L, "Inscrições", "Submetidas e não canceladas"),
            new("INSCRITOS", 9L, "Inscritos", "Pessoas distintas com inscrição submetida e não cancelada"),
            new("RASCUNHOS", 4L, "Rascunhos", "Inscrições ainda não enviadas")
        ],
        null!);

    private sealed record Cenario(
        IProcessoSeletivoRepository Processos,
        ICertameDivulgadoRepository Divulgado,
        IIndicadoresDoProcessoReader Leitor,
        IVerificadorDeAcesso Verificador,
        IRegistroCodecsEnvelope Codecs,
        DescricaoDosRascunhos Descricao);

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
