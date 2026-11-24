namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// "Minhas inscrições" (issue #1860, <c>GET /api/selecao/minhas-inscricoes</c>): todas as do dono
/// do token, em qualquer processo, com os cursos que escolheram na versão em que estão.
/// </summary>
public sealed class MinhasInscricoesQueryHandlerTests
{
    private static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    private static Inscricao ComRespostas(Cpf cpf, string contaId, Guid processoId, Dictionary<string, JsonElement> respostas)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            processoId, cpf, contaId, "https://issuer", Guid.CreateVersion7()).Value!;
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), TestSupport.DefinicoesDeTeste.Das(respostas) with { NomeCivilDoLogin = cpf.Valor == PersonasDeTeste.KevinCarvalho.Valor ? PersonasDeTeste.NomeKevinCarvalho : PersonasDeTeste.NomeFernandaDrumond, NomeSocialDoLogin = respostas.TryGetValue("NOME_SOCIAL", out JsonElement nomeSocial) ? nomeSocial.GetString() : null }, Unifesspa.UniPlus.Selecao.Domain.ValueObjects.PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        return inscricao;
    }

    private static ProcessosDasInscricoes Processos(
        ICertameDivulgadoRepository divulgados, IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registro) =>
        new(divulgados, processoRepository, registro, Substitute.For<IResolvedorFusoInstitucional>(), TimeProvider.System);

    /// <summary>Nenhum processo divulgado: o nome do processo sai nulo e a lista não depende dele.</summary>
    private static ICertameDivulgadoRepository CertamesDivulgados() => Substitute.For<ICertameDivulgadoRepository>();

    [Fact(DisplayName = "Sem o claim cpf no token, recusa sem consultar repositório nenhum")]
    public async Task Handle_SemCpfNoToken_Recusa()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.Cpf.Returns((string?)null);
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), Processos(CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>()), userContext, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await inscricaoRepository.DidNotReceive().ObterTodasPorCpfAsync(Arg.Any<Cpf>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem o claim sub no token, recusa com ContaIdObrigatorio em vez de listar vazio")]
    public async Task Handle_SemContaIdNoToken_Recusa()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Inscricao>)[Inscricao.Iniciar(Guid.CreateVersion7(), cpf, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), Processos(CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>()), UsuarioComCpf(cpf, contaId: ""), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ContaIdObrigatorio);
    }

    [Fact(DisplayName = "Nenhuma inscrição do CPF devolve lista vazia, não falha")]
    public async Task Handle_SemInscricoes_DevolveListaVazia()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), Processos(CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>()), UsuarioComCpf(cpf), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Inscrição de outra conta para o mesmo CPF não aparece na lista")]
    public async Task Handle_InscricaoDeOutroContaId_NaoAparece()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Inscricao deOutroContaId = Inscricao.Iniciar(
            Guid.CreateVersion7(), cpf, "conta-outro", "https://issuer", Guid.CreateVersion7()).Value!;

        IInscricaoRepository inscricaoRepository = Substitute.For<IInscricaoRepository>();
        inscricaoRepository.ObterTodasPorCpfAsync(cpf, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Inscricao>)[deOutroContaId]);

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), inscricaoRepository, SubmeterDeTeste.ComprovantesDe(inscricaoRepository), Processos(CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>()), UsuarioComCpf(cpf, contaId: "conta-1"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada inscrição vem com o nome divulgado do processo e os comprovantes numerados na ordem de emissão")]
    public async Task Handle_TrazONomeDoProcessoEONumeroDoUltimoComprovante()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        DateTimeOffset instante = new(2026, 12, 10, 9, 0, 0, TimeSpan.Zero);
        Inscricao inscricao = InscricaoComNome(cpf, processoId);
        IReadOnlyList<EmissaoDoComprovante> emissoes = [new(inscricao.Id, instante), new(inscricao.Id, instante.AddHours(1))];

        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await ListarAsync([inscricao],
            divulgados: DivulgacaoDoProcesso(processoId, instante), emissoes: emissoes);

        MinhaInscricaoDto item = resultado.Value!.Should().ContainSingle().Subject;
        item.NomeDoProcesso.Should().Be("PSS Medicina 2027");
        item.NumeroDoUltimoComprovante.Should().Be(2);
    }

    [Fact(DisplayName = "A cancelada e a nova inscrição do mesmo processo aparecem, cada uma com o próprio id")]
    public async Task Handle_CanceladaENova_AparecemAsDuasComIdProprio()
    {
        Cpf cpf = PersonasDeTeste.KevinCarvalho;
        Guid processoId = Guid.CreateVersion7();
        Inscricao cancelada = InscricaoCancelada(cpf, processoId);
        Inscricao nova = Inscricao.Iniciar(processoId, cpf, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!;
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await ListarAsync([cancelada, nova],
            emissoes: [new EmissaoDoComprovante(cancelada.Id, DateTimeOffset.UtcNow)]);

        resultado.Value!.Select(static i => i.InscricaoId).Should().BeEquivalentTo([cancelada.Id, nova.Id]);
        MinhaInscricaoDto daCancelada = resultado.Value!.Single(i => i.InscricaoId == cancelada.Id);
        daCancelada.Situacao.Should().Be(CodigoComRotulo<SituacaoInscricao>.De(SituacaoInscricao.Cancelada));
        daCancelada.MotivoDoCancelamento.Should().Be("a pedido do candidato");
        daCancelada.CanceladaEm.Should().NotBeNull();
        daCancelada.NumeroDoUltimoComprovante.Should().BeNull("a cancelada tem comprovante emitido, mas a rota o recusa");
        daCancelada.Acoes.Should().Be(new AcoesDaInscricaoDto(false, false, false, false));
    }

    [Fact]
    public void De_InscricaoEEmissoesNulas_RecusaInscricaoPrimeiro()
    {
        Action acao = () => MinhaInscricaoDto.De(null!, null, false, null!, null!);

        acao.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("inscricao");
    }

    [Fact]
    public void De_InscricaoCanceladaEEmissoesNulas_RecusaEmissoesAntesDasOfertas()
    {
        Inscricao cancelada = InscricaoCancelada(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        Action acao = () => MinhaInscricaoDto.De(cancelada, null, false, null!, null!);

        acao.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("emissoes");
    }

    private const string SchemaVersion = "0.0.22";

    private static EnvelopeReidratado EnvelopeComOfertas(params ConfiguracaoDistribuicaoVagas[] distribuicao)
    {
        ProcessoSeletivo processoBase = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — Lista");
        return new EnvelopeReidratado(
            new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processoBase.CronogramaFases]),
            new AvaliacaoDeclarada([.. processoBase.Etapas], [.. processoBase.CriteriosDesempate], processoBase.Classificacao!),
            new VagasDeclaradas(distribuicao, 2, processoBase.BonusRegional is not null,
                processoBase.BonusRegional, null, processoBase.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processoBase.DocumentosExigidos], []),
            new ColetaDeclarada([], [], [], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada());
    }

    private static VersaoConfiguracao NovaVersao(Guid processoId) =>
        VersaoConfiguracao.Abrir(
            processoId, new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), SchemaVersion, "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Um registro de codecs que reidrata cada versão no envelope dela.</summary>
    private static IRegistroCodecsEnvelope RegistroDas(params (VersaoConfiguracao Versao, EnvelopeReidratado Envelope)[] versoes)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.LeAVersao(SchemaVersion).Returns(true);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(chamada =>
            Result<EnvelopeReidratado>.Success(versoes.Single(v => v.Versao.Id == chamada.Arg<VersaoConfiguracao>().Id).Envelope));
        return registro;
    }

    private static Inscricao ComOpcoes(Cpf cpf, Guid processoId, Guid versaoId, Guid? primeira, Guid? segunda) =>
        Inscricao.Reidratar(Guid.CreateVersion7(), processoId,
            TitularDaInscricao.Reidratar(cpf, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, null, CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(versaoId, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(primeira, segunda), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 1, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

    [Fact(DisplayName = "As opções de curso saem na ordem da inscrição, com o nome que a versão dela congelou")]
    public async Task Handle_TrazOsCursosEscolhidosEmOrdem()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        Guid direito = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = RegistroDas((versao, EnvelopeComOfertas(
            OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"), OfertasDeTeste.DistribuicaoDoCurso(direito, "Direito", "AC"))));
        // A 1ª opção é a que tem o maior id: a ordem sai da escolha, não da ordem das ofertas na versão.
        Inscricao inscricao = ComOpcoes(cpf, processoId, versao.Id, primeira: direito, segunda: medicina);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await ListarAsync([inscricao], [versao], registro);

        resultado.Value!.Single().OpcoesDeCurso.Should().Equal(
            new OpcaoDeCursoDaInscricaoDto(1, "Direito", "Bacharelado"), new OpcaoDeCursoDaInscricaoDto(2, "Medicina", "Bacharelado"));
    }

    [Fact(DisplayName = "Rascunho que ainda não escolheu curso vem sem opções, e uma versão que não se lê não derruba a lista")]
    public async Task Handle_SemCursoEscolhidoOuVersaoIlegivel_VemSemOpcoes()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = RegistroDas((versao, EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"))));
        Inscricao semCurso = ComOpcoes(cpf, processoId, versao.Id, primeira: null, segunda: null);
        Inscricao versaoSumida = ComOpcoes(cpf, processoId, Guid.CreateVersion7(), primeira: medicina, segunda: null);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await ListarAsync([semCurso, versaoSumida], [versao], registro);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Should().HaveCount(2).And.OnlyContain(static i => i.OpcoesDeCurso.Count == 0);
    }

    [Fact(DisplayName = "O nome do curso vem da versão em que a inscrição está, não da versão mais recente do processo")]
    public async Task Handle_NomeDoCursoEhODaVersaoDaInscricao()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid medicina = Guid.CreateVersion7();
        VersaoConfiguracao daInscricao = NovaVersao(processoId);
        VersaoConfiguracao retificada = NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = RegistroDas(
            (daInscricao, EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina", "AC"))),
            (retificada, EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(medicina, "Medicina (retificado)", "AC"))));
        Inscricao inscricao = ComOpcoes(cpf, processoId, daInscricao.Id, primeira: medicina, segunda: null);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await ListarAsync([inscricao], [daInscricao, retificada], registro);

        resultado.Value!.Single().OpcoesDeCurso.Should().Equal(new OpcaoDeCursoDaInscricaoDto(1, "Medicina", "Bacharelado"));
    }

    [Fact(DisplayName = "Sem a 1ª opção nomeada pela versão, a 2ª não sobe para o lugar dela")]
    public async Task Handle_SemAPrimeiraOpcaoNaVersao_NaoListaASegundaComoPrimeira()
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Guid processoId = Guid.CreateVersion7();
        Guid direito = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId);
        IRegistroCodecsEnvelope registro = RegistroDas((versao, EnvelopeComOfertas(OfertasDeTeste.DistribuicaoDoCurso(direito, "Direito", "AC"))));
        Inscricao inscricao = ComOpcoes(cpf, processoId, versao.Id, primeira: Guid.CreateVersion7(), segunda: direito);
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await ListarAsync([inscricao], [versao], registro);

        resultado.Value!.Single().OpcoesDeCurso.Should().BeEmpty();
    }
    private static Task<Result<IReadOnlyList<MinhaInscricaoDto>>> ListarAsync(
        IReadOnlyList<Inscricao> inscricoes, VersaoConfiguracao[]? versoes = null, IRegistroCodecsEnvelope? registro = null,
        ICertameDivulgadoRepository? divulgados = null, IReadOnlyList<EmissaoDoComprovante>? emissoes = null)
    {
        IInscricaoRepository repositorio = Substitute.For<IInscricaoRepository>();
        repositorio.ObterTodasPorCpfAsync(inscricoes[0].Titular.Cpf, Arg.Any<CancellationToken>()).Returns(inscricoes);
        IComprovanteDaInscricaoRepository comprovantes = Substitute.For<IComprovanteDaInscricaoRepository>();
        comprovantes.ObterEmissoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(emissoes ?? []);
        return MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), repositorio, comprovantes,
            Processos(divulgados ?? CertamesDivulgados(), ProcessosComVersoes(versoes ?? []), registro ?? Substitute.For<IRegistroCodecsEnvelope>()),
            UsuarioComCpf(inscricoes[0].Titular.Cpf), CancellationToken.None);
    }

    private static IProcessoSeletivoRepository ProcessosComVersoes(VersaoConfiguracao[] versoes)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        foreach (IGrouping<Guid, VersaoConfiguracao> porProcesso in versoes.GroupBy(static v => v.ProcessoSeletivoId))
        {
            processos.ObterVersoesAsync(porProcesso.Key, Arg.Any<CancellationToken>()).Returns([.. porProcesso]);
        }
        return processos;
    }

    private static Inscricao InscricaoComNome(Cpf cpf, Guid processoId) =>
        ComRespostas(cpf, "conta-1", processoId, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        });

    private static Inscricao InscricaoCancelada(Cpf cpf, Guid processoId) =>
        Inscricao.Reidratar(Guid.CreateVersion7(), processoId,
            TitularDaInscricao.Reidratar(cpf, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Cancelada, "000123", CancelamentoDaInscricao.Reidratar(DateTimeOffset.UtcNow, "conta-1", "a pedido do candidato")),
            FormularioDaInscricao.Reidratar(Guid.CreateVersion7(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 2, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

    private static ICertameDivulgadoRepository DivulgacaoDoProcesso(Guid processoId, DateTimeOffset instante)
    {
        ICertameDivulgadoRepository divulgados = Substitute.For<ICertameDivulgadoRepository>();
        divulgados.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(CertameDivulgado.Criar(
            processoId, numeroVersao: 1, Guid.CreateVersion7(), new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("pss-medicina-2027", "PSS Medicina 2027", "001/2026", ["AC"], instante, instante.AddDays(30)),
            """{"nome":"documento"}""", instante));
        return divulgados;
    }
}
