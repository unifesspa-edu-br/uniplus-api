namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// "Minhas inscrições" (issue #1860, <c>GET /api/selecao/minhas-inscricoes</c>): todas as do dono
/// do token, em qualquer processo, com os cursos que escolheram na versão em que estão.
/// </summary>
public sealed class MinhasInscricoesQueryHandlerTests
{
    private static IUserContext UsuarioComCpf(Cpf cpf, string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    private static Inscricao ComRespostas(Cpf cpf, string sub, Guid processoId, Dictionary<string, JsonElement> respostas)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            processoId, cpf, sub, "https://issuer", Guid.CreateVersion7()).Value!;
        inscricao.Salvar(
            null, null, respostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            TestSupport.DefinicoesDeTeste.Das(respostas) with { NomeCivilDoLogin = cpf.Valor == PersonasDeTeste.KevinCarvalho.Valor ? PersonasDeTeste.NomeKevinCarvalho : PersonasDeTeste.NomeFernandaDrumond, NomeSocialDoLogin = respostas.TryGetValue("NOME_SOCIAL", out JsonElement nomeSocial) ? nomeSocial.GetString() : null },
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        return inscricao;
    }

    /// <summary>Nenhum processo divulgado: o nome do processo sai nulo e a lista não depende dele.</summary>
    private static ICertameDivulgadoRepository CertamesDivulgados() => Substitute.For<ICertameDivulgadoRepository>();

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, userContext, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterTodasPorCpfAsync(Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem o claim sub no token, recusa com SubObrigatorio em vez de listar vazio")]
    public async Task Handle_SemSubNoToken_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Inscricao>)[Inscricao.Iniciar(Guid.CreateVersion7(), cpf, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf, sub: ""), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.SubObrigatorio);
    }

    [Fact(DisplayName = "Nenhuma inscrição do CPF devolve lista vazia, não falha")]
    public async Task Handle_SemInscricoes_DevolveListaVazia()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Inscrição de outro sub para o mesmo CPF não aparece na lista")]
    public async Task Handle_InscricaoDeOutroSub_NaoAparece()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao deOutroSub = Inscricao.Iniciar(
            Guid.CreateVersion7(), cpf, "sub-outro", "https://issuer", Guid.CreateVersion7()).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[deOutroSub]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf, sub: "sub-1"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada inscrição vem com o nome divulgado do processo e os comprovantes numerados na ordem de emissão")]
    public async Task Handle_TrazONomeDoProcessoEOsComprovantes()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        DateTimeOffset instante = new(2026, 12, 10, 9, 0, 0, TimeSpan.Zero);
        Inscricao inscricao = ComRespostas(cpf, "sub-1", processoId, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        });
        IReadOnlyList<ComprovanteInscricao> comprovantes =
        [
            ComprovanteInscricao.Emitir(inscricao.Id, "00000001", instante, null,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], []),
            ComprovanteInscricao.Emitir(inscricao.Id, "00000001", instante.AddHours(1), null,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], []),
        ];

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[inscricao]);
        inscricaoRepository.ObterComprovantesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(comprovantes);
        ICertameDivulgadoRepository divulgados = Substitute.For<ICertameDivulgadoRepository>();
        divulgados.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(CertameDivulgado.Criar(
            processoId, numeroVersao: 1, Guid.CreateVersion7(), new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("pss-medicina-2027", "PSS Medicina 2027", "001/2026", ["AC"], instante, instante.AddDays(30)),
            """{"nome":"documento"}""", instante));

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, divulgados, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf), CancellationToken.None);

        MinhaInscricaoDto item = resultado.Value!.Should().ContainSingle().Subject;
        item.NomeDoProcesso.Should().Be("PSS Medicina 2027");
        item.Comprovantes.Should().Equal(new ComprovanteEmitidoDto(1, instante), new ComprovanteEmitidoDto(2, instante.AddHours(1)));
    }

    [Fact(DisplayName = "A cancelada e a nova inscrição do mesmo processo aparecem, cada uma com o próprio id")]
    public async Task Handle_CanceladaENova_AparecemAsDuasComIdProprio()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Guid processoId = Guid.CreateVersion7();
        Inscricao cancelada = Inscricao.Reidratar(
            Guid.CreateVersion7(), processoId, cpf, "sub-1", "https://issuer", SituacaoInscricao.Cancelada, Guid.CreateVersion7(), null, null,
            "000123", DateTimeOffset.UtcNow, "sub-1", "a pedido do candidato",
            new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [], new Dictionary<string, DefinicaoDaResposta>(), 2, DateTimeOffset.UtcNow, null);
        Inscricao nova = Inscricao.Iniciar(processoId, cpf, "sub-1", "https://issuer", Guid.CreateVersion7()).Value!;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[cancelada, nova]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>(), Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.Value!.Select(static i => i.InscricaoId).Should().BeEquivalentTo([cancelada.Id, nova.Id]);
        MinhaInscricaoDto daCancelada = resultado.Value!.Single(i => i.InscricaoId == cancelada.Id);
        daCancelada.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        daCancelada.MotivoDoCancelamento.Should().Be("a pedido do candidato");
        daCancelada.Acoes.Should().Be(new AcoesDaInscricaoDto(false, false, false, false));
    }

    private const string SchemaVersion = "0.0.22";

    /// <summary>A versão do processo cujas ofertas são as dadas — o que a inscrição fixa ao ser iniciada.</summary>
    private static (VersaoConfiguracao Versao, IRegistroCodecsEnvelope Registro) VersaoComOfertas(
        Guid processoId, params ConfiguracaoDistribuicaoVagas[] distribuicao)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Lista");
        EnvelopeReidratado envelope = new(
            new GrafoConfiguracao(
                [.. processoBase.Etapas], processoBase.OfertaAtendimento!, distribuicao,
                processoBase.BonusRegional, [.. processoBase.CriteriosDesempate], processoBase.Classificacao!,
                [.. processoBase.CronogramaFases], [.. processoBase.DocumentosExigidos], [], null, numeroDeOpcoesDeCurso: 2),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId, Encoding.UTF8.GetBytes("{}"), SchemaVersion, "canonical-json/sha256@v1",
            Guid.CreateVersion7(), new string('a', 64), "user-sub-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns([new CapacidadeCodec(SchemaVersion, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(envelope));
        return (versao, registro);
    }

    private static Inscricao ComOpcoes(Cpf cpf, Guid processoId, Guid versaoId, Guid? primeira, Guid? segunda) =>
        Inscricao.Reidratar(
            Guid.CreateVersion7(), processoId, cpf, "sub-1", "https://issuer", SituacaoInscricao.Rascunho, versaoId, primeira, segunda,
            null, null, null, null,
            new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [], new Dictionary<string, DefinicaoDaResposta>(), 1, DateTimeOffset.UtcNow, null);

    [Fact(DisplayName = "As opções de curso saem na ordem da inscrição, com o nome que a versão dela congelou")]
    public async Task Handle_TrazOsCursosEscolhidosEmOrdem()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        Guid direito = Guid.CreateVersion7();
        (VersaoConfiguracao versao, IRegistroCodecsEnvelope registro) = VersaoComOfertas(
            processoId, OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"), OfertasDeTeste.DistribuicaoDoCurso(direito, "Direito", "AC"));
        // A 1ª opção é a que tem o maior id: a ordem sai da escolha, não da ordem das ofertas na versão.
        Inscricao inscricao = ComOpcoes(cpf, processoId, versao.Id, primeira: direito, segunda: medicina);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[inscricao]);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(processoId, Arg.Any<CancellationToken>()).Returns([versao]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), processoRepository, registro, Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.Value!.Single().OpcoesDeCurso.Should().Equal(
            new OpcaoDeCursoDaInscricaoDto(1, "Direito", "Bacharelado"), new OpcaoDeCursoDaInscricaoDto(2, "Medicina", "Bacharelado"));
    }

    [Fact(DisplayName = "Rascunho que ainda não escolheu curso vem sem opções, e uma versão que não se lê não derruba a lista")]
    public async Task Handle_SemCursoEscolhidoOuVersaoIlegivel_VemSemOpcoes()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        (VersaoConfiguracao versao, IRegistroCodecsEnvelope registro) = VersaoComOfertas(
            processoId, OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"));
        Inscricao semCurso = ComOpcoes(cpf, processoId, versao.Id, primeira: null, segunda: null);
        Inscricao versaoSumida = ComOpcoes(cpf, processoId, Guid.CreateVersion7(), primeira: medicina, segunda: null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[semCurso, versaoSumida]);
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterVersoesAsync(processoId, Arg.Any<CancellationToken>()).Returns([versao]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(
            new MinhasInscricoesQuery(), inscricaoRepository, CertamesDivulgados(), processoRepository, registro, Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System, UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().HaveCount(2).And.OnlyContain(static i => i.OpcoesDeCurso.Count == 0);
    }
}
