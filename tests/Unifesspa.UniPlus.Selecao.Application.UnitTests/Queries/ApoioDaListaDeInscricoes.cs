namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
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
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que os testes da lista "Minhas inscrições" montam em comum: o usuário do token, as inscrições, as
/// versões e os repositórios de teste, e a consulta que lista no instante do relógio informado. Os testes
/// usam esta classe por composição, e cada um escolhe o relógio.
/// </summary>
internal sealed class ApoioDaListaDeInscricoes(TimeProvider relogio)
{
    internal const string SchemaVersion = "0.0.22";

    public LoggerCapturadoDeTeste<ProcessosDasInscricoes> Logger { get; } = new();

    internal static IUserContext UsuarioComCpf(Cpf cpf, string contaId = "conta-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.UserId.Returns(contaId);
        return contexto;
    }

    /// <summary>Nenhum processo divulgado: o nome do processo sai nulo e a lista não depende dele.</summary>
    internal static ICertameDivulgadoRepository CertamesDivulgados() => Substitute.For<ICertameDivulgadoRepository>();

    internal ProcessosDasInscricoes Processos(
        ICertameDivulgadoRepository divulgados, IProcessoSeletivoRepository processoRepository, IRegistroCodecsEnvelope registro,
        IResolvedorFusoInstitucional? fuso = null) =>
        new(
            divulgados,
            new PeriodoDaInscricaoResolver(divulgados, processoRepository, registro, fuso ?? DivulgacaoDeTeste.Fuso()),
            new ContextoDaVersaoFixadaResolver(processoRepository, registro),
            relogio,
            Logger);

    internal ProcessosDasInscricoes ProcessosSemDivulgacao() =>
        Processos(CertamesDivulgados(), Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IRegistroCodecsEnvelope>());

    internal Task<Result<IReadOnlyList<MinhaInscricaoDto>>> ListarAsync(
        IReadOnlyList<Inscricao> inscricoes, VersaoConfiguracao[]? versoes = null, IRegistroCodecsEnvelope? registro = null,
        ICertameDivulgadoRepository? divulgados = null, IReadOnlyList<EmissaoDoComprovante>? emissoes = null) =>
        ListarAsync(inscricoes,
            Processos(divulgados ?? CertamesDivulgados(), ProcessosComVersoes(versoes ?? []), registro ?? Substitute.For<IRegistroCodecsEnvelope>()),
            emissoes);

    internal static Task<Result<IReadOnlyList<MinhaInscricaoDto>>> ListarAsync(
        IReadOnlyList<Inscricao> inscricoes, ProcessosDasInscricoes processos, IReadOnlyList<EmissaoDoComprovante>? emissoes = null)
    {
        IInscricaoRepository repositorio = Substitute.For<IInscricaoRepository>();
        repositorio.ObterTodasPorCpfAsync(inscricoes[0].Titular.Cpf, Arg.Any<CancellationToken>()).Returns(inscricoes);
        IComprovanteDaInscricaoRepository comprovantes = Substitute.For<IComprovanteDaInscricaoRepository>();
        comprovantes.ObterEmissoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(emissoes ?? []);
        return MinhasInscricoesQueryHandler.Handle(new MinhasInscricoesQuery(), repositorio, comprovantes, processos,
            UsuarioComCpf(inscricoes[0].Titular.Cpf), CancellationToken.None);
    }

    internal static IProcessoSeletivoRepository ProcessosComVersoes(VersaoConfiguracao[] versoes)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        foreach (IGrouping<Guid, VersaoConfiguracao> porProcesso in versoes.GroupBy(static v => v.ProcessoSeletivoId))
        {
            processos.ObterVersoesAsync(porProcesso.Key, Arg.Any<CancellationToken>()).Returns([.. porProcesso]);
        }
        return processos;
    }

    internal static Inscricao Rascunho(Cpf cpf, Guid processoId) =>
        Inscricao.Iniciar(processoId, cpf, "conta-1", "https://issuer", Guid.CreateVersion7()).Value!;

    internal static Inscricao Enviada(Cpf cpf, Guid processoId) => InscricaoEnviadaDeTeste.Enviar(Rascunho(cpf, processoId));

    internal static Inscricao ComRespostas(Cpf cpf, string contaId, Guid processoId, Dictionary<string, JsonElement> respostas)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            processoId, cpf, contaId, "https://issuer", Guid.CreateVersion7()).Value!;
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), DefinicoesDeTeste.Das(respostas) with { NomeCivilDoLogin = cpf.Valor == PersonasDeTeste.KevinCarvalho.Valor ? PersonasDeTeste.NomeKevinCarvalho : PersonasDeTeste.NomeFernandaDrumond, NomeSocialDoLogin = respostas.TryGetValue("NOME_SOCIAL", out JsonElement nomeSocial) ? nomeSocial.GetString() : null }, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        return inscricao;
    }

    internal static Inscricao InscricaoComNome(Cpf cpf, Guid processoId) =>
        ComRespostas(cpf, "conta-1", processoId, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        });

    internal static Inscricao InscricaoCancelada(Cpf cpf, Guid processoId) =>
        Inscricao.Reidratar(Guid.CreateVersion7(), processoId,
            TitularDaInscricao.Reidratar(cpf, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Cancelada, "000123", CancelamentoDaInscricao.Reidratar(DivulgacaoDeTeste.DentroDoPeriodo, "conta-1", "a pedido do candidato")),
            FormularioDaInscricao.Reidratar(Guid.CreateVersion7(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 2, new DatasDeGravacaoDaInscricao(default, null));

    internal static Inscricao ComOpcoes(Cpf cpf, Guid processoId, Guid versaoId, Guid? primeira, Guid? segunda) =>
        Inscricao.Reidratar(Guid.CreateVersion7(), processoId,
            TitularDaInscricao.Reidratar(cpf, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, null, CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(versaoId, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(primeira, segunda), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 1, new DatasDeGravacaoDaInscricao(default, null));

    internal static EnvelopeReidratado EnvelopeComOfertas(params ConfiguracaoDistribuicaoVagas[] distribuicao)
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

    internal static VersaoConfiguracao NovaVersao(Guid processoId, string schemaVersion = SchemaVersion) =>
        VersaoConfiguracao.Abrir(
            processoId, new DocumentoCongelado(Encoding.UTF8.GetBytes("{}"), schemaVersion, "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "user-conta-1", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Um registro de codecs que reidrata cada versão no envelope dela.</summary>
    internal static IRegistroCodecsEnvelope RegistroDas(params (VersaoConfiguracao Versao, EnvelopeReidratado Envelope)[] versoes)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.LeAVersao(SchemaVersion).Returns(true);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(chamada =>
            Result<EnvelopeReidratado>.Success(versoes.Single(v => v.Versao.Id == chamada.Arg<VersaoConfiguracao>().Id).Envelope));
        return registro;
    }

    internal static ICertameDivulgadoRepository DivulgacaoDoProcesso(Guid processoId, DateTimeOffset instante)
    {
        ICertameDivulgadoRepository divulgados = Substitute.For<ICertameDivulgadoRepository>();
        divulgados.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(CertameDivulgado.Criar(
            processoId, numeroVersao: 1, Guid.CreateVersion7(), new string('a', 64), versaoProjecao: "1",
            new FacetasDoCertameDivulgado("pss-medicina-2027", "PSS Medicina 2027", "001/2026", ["AC"], instante, instante.AddDays(30)),
            """{"nome":"documento"}""", instante));
        return divulgados;
    }
}
