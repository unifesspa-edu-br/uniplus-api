namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ObterIndicadoresDoProcessoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid UnidadeId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Agora = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Handler encaminha a consulta e a devolve sem alterar o resultado")]
    public async Task Handler_EncaminhaConsultaEDevolveResultado()
    {
        ObterIndicadoresDoProcessoQuery query = new(ProcessoId);
        CancellationToken cancellationToken = new(canceled: true);
        IndicadoresDoProcessoDto dto = new(
            ProcessoId,
            Agora,
            [],
            new IndicadoresDoProcessoLinksDto("/processos/indicadores", "/processos"));
        Result<IndicadoresDoProcessoDto> esperado = Result<IndicadoresDoProcessoDto>.Success(dto);
        IObterIndicadoresDoProcesso service = Substitute.For<IObterIndicadoresDoProcesso>();
        service.ExecutarAsync(query, cancellationToken).Returns(esperado);

        Result<IndicadoresDoProcessoDto> resultado = await ObterIndicadoresDoProcessoQueryHandler.Handle(
            query, service, cancellationToken);

        resultado.Value.Should().BeSameAs(dto);
        await service.Received(1).ExecutarAsync(query, cancellationToken);
    }

    [Fact(DisplayName = "Processo publicado retorna os três códigos ordenados, contagens, textos, instante UTC e links")]
    public async Task Publicado_RetornaContratoExato()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Publicado, Agora);
        fixture.Reader.LerAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new ContagensDoProcesso(12, 12, 4));

        Result<IndicadoresDoProcessoDto> resultado = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        IndicadoresDoProcessoDto dto = resultado.Value!;
        dto.ProcessoSeletivoId.Should().Be(ProcessoId);
        dto.ConsultadoEm.Should().Be(Agora);
        dto.ConsultadoEm.Offset.Should().Be(TimeSpan.Zero);
        dto.Indicadores.Select(i => i.Codigo).Should().Equal(
            CodigoIndicadorDoProcesso.Inscricoes,
            CodigoIndicadorDoProcesso.Inscritos,
            CodigoIndicadorDoProcesso.Rascunhos);
        dto.Indicadores.Select(i => i.Valor).Should().Equal(12L, 12L, 4L);
        dto.Indicadores.Select(i => i.Label).Should().Equal("Inscrições", "Inscritos", "Rascunhos");
        dto.Indicadores.Select(i => i.Descricao).Should().Equal(
            "Submetidas e não canceladas",
            "Pessoas distintas com inscrição submetida e não cancelada",
            "Inscrições ainda não enviadas");
        dto.Links.Self.Should().Be($"/api/selecao/processos-seletivos/{ProcessoId:D}/indicadores");
        dto.Links.Processo.Should().Be($"/api/selecao/processos-seletivos/{ProcessoId:D}");
        await fixture.Reader.Received(1).LerAsync(ProcessoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo em rascunho autorizado responde com zeros sem consultar inscrições")]
    public async Task RascunhoComPermissaoDeConfigurar_RetornaZeros()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Rascunho, Agora);

        Result<IndicadoresDoProcessoDto> resultado = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Indicadores.Select(i => i.Valor).Should().Equal(0L, 0L, 0L);
        resultado.Value.Indicadores[2].Descricao.Should().Be("Inscrições ainda não enviadas");
        await fixture.Reader.DidNotReceiveWithAnyArgs().LerAsync(default, default);
        await fixture.Verificador.Received(1).ExigirAsync(
            Arg.Any<PermissionRequirement>(),
            Arg.Any<ResourceContext?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo inexistente ou rascunho sem permissão de configurar é ocultado como não encontrado")]
    public async Task ProcessoInexistenteOuRascunhoSemAcesso_Retorna404DeDominio()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Rascunho, Agora, ResultadoDoAcesso.Negado);
        fixture.Processos.ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns((EstadoDoProcesso?)null);

        Result<IndicadoresDoProcessoDto> inexistente = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);
        inexistente.IsFailure.Should().BeTrue();
        inexistente.Error!.Code.Should().Be("Indicadores.ProcessoNaoEncontrado");

        fixture.Processos.ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new EstadoDoProcesso(UnidadeId, StatusProcesso.Rascunho, JaPublicado: false));
        Result<IndicadoresDoProcessoDto> semPermissao = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);
        semPermissao.IsFailure.Should().BeTrue();
        semPermissao.Error!.Code.Should().Be("Indicadores.ProcessoNaoEncontrado");
        await fixture.Reader.DidNotReceiveWithAnyArgs().LerAsync(default, default);
    }

    [Theory(DisplayName = "Descrição dos rascunhos vem da janela da versão publicada")]
    [InlineData("2026-11-12T15:00:00+00:00", "2026-11-12T16:00:00+00:00", "Em preenchimento")]
    [InlineData("2026-11-12T16:00:00+00:00", "2026-11-13T16:00:00+00:00", "Inscrições ainda não enviadas")]
    [InlineData("2026-11-10T15:00:00+00:00", "2026-11-12T14:59:59+00:00", "Não enviados")]
    public async Task VersaoDivulgada_ClassificaDescricaoDaJanela(string inicio, string fim, string esperada)
    {
        Fixture fixture = CriarFixture(StatusProcesso.Publicado, Agora);
        ConfigurarVersaoDivulgada(
            fixture,
            DateTimeOffset.Parse(inicio, System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(fim, System.Globalization.CultureInfo.InvariantCulture));

        Result<IndicadoresDoProcessoDto> resultado = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Indicadores[2].Descricao.Should().Be(esperada);
    }

    [Fact(DisplayName = "Prorrogação publicada por retificação mantém Rascunhos em preenchimento durante a janela prorrogada")]
    public async Task RetificacaoProrrogandoInscricoes_JanelaDivulgadaDeterminaDescricao()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Publicado, Agora);
        DateTimeOffset fimOriginal = Agora.AddDays(-1);
        DateTimeOffset fimProrrogado = Agora.AddDays(1);
        (Guid atoDaRetificacao, VersaoConfiguracao versaoRetificada, Guid atoOriginal) = ConfigurarVersaoDivulgada(
            fixture,
            Agora.AddDays(-2),
            fimProrrogado,
            motivoRetificacao: "Prorrogação do período de inscrição",
            fimOriginal: fimOriginal);

        Result<IndicadoresDoProcessoDto> resultado = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);

        Agora.Should().BeAfter(fimOriginal).And.BeBefore(fimProrrogado);
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Indicadores[2].Descricao.Should().Be("Em preenchimento");
        await fixture.Processos.Received(1).ObterVersoesPorAtoCriadorAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(atoDaRetificacao)),
            Arg.Any<CancellationToken>());
        await fixture.Processos.DidNotReceive().ObterVersoesPorAtoCriadorAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(atoOriginal)),
            Arg.Any<CancellationToken>());
        fixture.Codecs.Received(1).Reidratar(versaoRetificada);
    }

    [Fact(DisplayName = "Ausência de versão divulgada ou formulário usa a descrição padrão sem transformar falha de leitura em sucesso")]
    public async Task AusenciaDeVersaoOuFormulario_UsaDescricaoPadraoEPropagaFalhaDeConsulta()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Publicado, Agora);
        Result<IndicadoresDoProcessoDto> semVersao = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);
        semVersao.Value!.Indicadores[2].Descricao.Should().Be("Inscrições ainda não enviadas");

        ConfigurarVersaoDivulgada(fixture, Agora.AddDays(-1), Agora.AddDays(1), semFormulario: true);
        Result<IndicadoresDoProcessoDto> semFormulario = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);
        semFormulario.Value!.Indicadores[2].Descricao.Should().Be("Inscrições ainda não enviadas");

        fixture.Codecs.Reidratar(Arg.Any<VersaoConfiguracao>())
            .Returns(Result<EnvelopeReidratado>.Failure(new DomainError("Snapshot.Ilegivel", "Falha na leitura.")));
        Result<IndicadoresDoProcessoDto> falha = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);
        falha.IsFailure.Should().BeTrue();
        falha.Error!.Code.Should().Be("Snapshot.Ilegivel");
    }

    [Fact(DisplayName = "A recusa por servidor inscrito permanece 403 e a consulta de contagens não é executada")]
    public async Task ServidorInscrito_PropagaImpedimento()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Publicado, Agora, ResultadoDoAcesso.Impedido);

        Result<IndicadoresDoProcessoDto> resultado = await fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ImpedidoPorInscricaoNoProcesso");
        await fixture.Reader.DidNotReceiveWithAnyArgs().LerAsync(default, default);
    }

    [Fact(DisplayName = "Falha na leitura das contagens não vira zeros nem resposta parcial")]
    public async Task FalhaDoReader_PropagaErro()
    {
        Fixture fixture = CriarFixture(StatusProcesso.Publicado, Agora);
        fixture.Reader.LerAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns<Task<ContagensDoProcesso>>(_ => throw new InvalidOperationException("Falha de leitura"));

        Func<Task> executar = () => fixture.Service.ExecutarAsync(
            new ObterIndicadoresDoProcessoQuery(ProcessoId), CancellationToken.None);

        await executar.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Falha de leitura");
    }

    private static Fixture CriarFixture(
        StatusProcesso status,
        DateTimeOffset agora,
        ResultadoDoAcesso acesso = ResultadoDoAcesso.Permitido)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterEstadoAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new EstadoDoProcesso(UnidadeId, status, JaPublicado: status != StatusProcesso.Rascunho));
        ICertameDivulgadoRepository divulgado = Substitute.For<ICertameDivulgadoRepository>();
        IIndicadoresDoProcessoReader reader = Substitute.For<IIndicadoresDoProcessoReader>();
        reader.LerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ContagensDoProcesso(0, 0, 0));
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(acesso);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        var service = new ObterIndicadoresDoProcesso(
            processos, divulgado, reader, verificador, codecs, new RelogioFixo(agora));
        return new Fixture(service, processos, divulgado, reader, verificador, codecs);
    }

    private static (Guid Ato, VersaoConfiguracao Versao, Guid AtoOriginal) ConfigurarVersaoDivulgada(
        Fixture fixture,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        bool semFormulario = false,
        string? motivoRetificacao = null,
        DateTimeOffset? fimOriginal = null)
    {
        Guid ato = Guid.CreateVersion7();
        Guid atoOriginal = fimOriginal is null ? Guid.Empty : Guid.CreateVersion7();
        CertameDivulgado certame = (CertameDivulgado)RuntimeHelpers.GetUninitializedObject(typeof(CertameDivulgado));
        typeof(CertameDivulgado).GetProperty(nameof(CertameDivulgado.AtoCriadorId), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(certame, ato);
        fixture.Divulgado.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(certame);

        ProcessoSeletivo processo = Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands
            .ProcessoSeletivoConformeBuilder.Criar("Processo de indicadores", out _);
        GrafoConfiguracao grafo = semFormulario
            ? new GrafoConfiguracao(
                [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas],
                processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
                [], [.. processo.DocumentosExigidos], [], null, numeroDeOpcoesDeCurso: 1)
            : VersoesPublicadasDeTeste.DoProcesso(processo);
        EnvelopeReidratado envelope = new(
            grafo,
            DadosEdital.Criar("001/2026", inicio, fim, Guid.CreateVersion7()).Value!,
            new string('a', 64),
            "America/Belem",
            motivoRetificacao is null ? null : new RetificacaoInfo(atoOriginal, motivoRetificacao),
            conformidade: null);
        fixture.Codecs.Reidratar(Arg.Any<VersaoConfiguracao>())
            .Returns(Result<EnvelopeReidratado>.Success(envelope));
        VersaoConfiguracao versao = (VersaoConfiguracao)RuntimeHelpers.GetUninitializedObject(typeof(VersaoConfiguracao));
        typeof(VersaoConfiguracao).GetProperty(nameof(VersaoConfiguracao.SchemaVersion), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(versao, "1.1");
        fixture.Codecs.Capacidades.Returns([new CapacidadeCodec("1.1", TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        fixture.Processos.ObterVersoesPorAtoCriadorAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(ato)), Arg.Any<CancellationToken>())
            .Returns([versao]);

        if (fimOriginal is { } prazoOriginal)
        {
            VersaoConfiguracao versaoOriginal = (VersaoConfiguracao)RuntimeHelpers.GetUninitializedObject(typeof(VersaoConfiguracao));
            typeof(VersaoConfiguracao).GetProperty(nameof(VersaoConfiguracao.SchemaVersion), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(versaoOriginal, "1.1");
            EnvelopeReidratado envelopeOriginal = new(
                grafo,
                DadosEdital.Criar("001/2026", inicio, prazoOriginal, Guid.CreateVersion7()).Value!,
                new string('b', 64),
                "America/Belem",
                retificacao: null,
                conformidade: null);
            fixture.Codecs.Reidratar(Arg.Is<VersaoConfiguracao>(v => ReferenceEquals(v, versaoOriginal)))
                .Returns(Result<EnvelopeReidratado>.Success(envelopeOriginal));
            fixture.Processos.ObterVersoesPorAtoCriadorAsync(
                    Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(atoOriginal)),
                    Arg.Any<CancellationToken>())
                .Returns([versaoOriginal]);
        }

        return (ato, versao, atoOriginal);
    }

    private sealed record Fixture(
        ObterIndicadoresDoProcesso Service,
        IProcessoSeletivoRepository Processos,
        ICertameDivulgadoRepository Divulgado,
        IIndicadoresDoProcessoReader Reader,
        IVerificadorDeAcesso Verificador,
        IRegistroCodecsEnvelope Codecs);
}
