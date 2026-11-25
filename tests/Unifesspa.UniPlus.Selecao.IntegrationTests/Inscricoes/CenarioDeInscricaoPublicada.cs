namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A montagem que as classes de teste da inscrição sobre o processo publicado compartilham: um processo
/// publicado e divulgado, servido pelos handlers de inscrição e pelo <see cref="InscricaoRepository"/>
/// contra Postgres real, com os repositórios de produção (<see cref="ProcessoSeletivoRepository"/>,
/// <see cref="CertameDivulgadoRepository"/>) e o codec vivo de produção (<see cref="EnvelopeCodec"/>),
/// em vez de um grafo fabricado à parte com repositórios substitutos.
/// </summary>
internal sealed class CenarioDeInscricaoPublicada
{
    private readonly InscricaoDbFixture _fixture;

    public CenarioDeInscricaoPublicada(InscricaoDbFixture fixture) => _fixture = fixture;

    internal static IRegistroCodecsEnvelope RegistroCodecs { get; } = new EnvelopeCodec();

    public string StringDeConexao => _fixture.ConnectionString;

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public InscricaoRepository NovoRepositorioDeInscricao(UniPlusDbContext db) =>
        RepositoriosDaInscricaoDeTeste.Inscricoes(db, _fixture);

    public ComprovanteDaInscricaoRepository NovoRepositorioDeComprovantes(UniPlusDbContext db) =>
        RepositoriosDaInscricaoDeTeste.Comprovantes(db, _fixture);

    public async Task<(Guid ProcessoId, InscricaoDto Inscricao)> PublicarEIniciarAsync(UniPlusDbContext db, ProcessoSeletivo processo, Cpf cpf)
    {
        (Guid processoId, _) = await PublicacaoDoProcessoDeTeste.PublicarEDivulgarAsync(db, processo);
        Result<InscricaoDto> iniciar = await IniciarRascunhoAsync(db, processoId, UsuarioComCpf(cpf));
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        return (processoId, iniciar.Value!);
    }

    public static async Task<ContextoDaVersaoFixada> ContextoAsync(UniPlusDbContext db, Guid versaoId, Guid processoId)
    {
        Result<ContextoDaVersaoFixada> contexto = await new ContextoDaVersaoFixadaResolver(ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs)
            .ResolverAsync(versaoId, processoId, ofertaCursoId: null, null, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);
        return contexto.Value!;
    }

    /// <summary>Inicia a inscrição do usuário no processo publicado, sem conferência de endereço pelo geo.</summary>
    public async Task<Result<InscricaoDto>> IniciarRascunhoAsync(
        UniPlusDbContext db, Guid processoId, IUserContext usuario, IConferenciaEnderecoGeo? geo = null) =>
        await IniciarInscricaoCommandHandler.Handle(new IniciarInscricaoCommand(processoId), NovoRepositorioDeInscricao(db), usuario,
            new InicioDaInscricao(NovoRepositorioDeInscricao(db), new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db),
                new PeriodoDaInscricaoResolver(new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs, Fuso()), usuario, DentroDoPeriodo),
            Leitura(db, geo ?? Substitute.For<IConferenciaEnderecoGeo>()), db, CancellationToken.None);

    /// <summary>Inicia a inscrição da Fernanda no processo publicado e devolve o ETag com que ela é enviada.</summary>
    public async Task<string> IniciarComoFernandaAsync(UniPlusDbContext db, Guid processoId)
    {
        Result<InscricaoDto> iniciar = await IniciarRascunhoAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), GeoQueConfirma());
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        return iniciar.Value!.ETag;
    }

    public async Task<Result<InscricaoDto>> ObterAsync(UniPlusDbContext db, Guid processoId, IUserContext usuario) =>
        await ObterInscricaoQueryHandler.Handle(new ObterInscricaoQuery(processoId), new InscricaoDoDono(NovoRepositorioDeInscricao(db), usuario), Leitura(db), CancellationToken.None);

    private static LeituraDaInscricao Leitura(UniPlusDbContext db, IConferenciaEnderecoGeo? geo = null) =>
        new(new VersaoDaInscricaoResolver(new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs),
            new ContextoDaVersaoFixadaResolver(ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs),
            new DocumentosEnviadosDaInscricao(Substitute.For<IDocumentoDaInscricaoRepository>()),
            new PreparacaoDaAvaliacaoDaInscricao(geo ?? Substitute.For<IConferenciaEnderecoGeo>()), EnderecosSemCopia(), Fuso(), DentroDoPeriodo);

    public async Task<Result<FormularioApresentadoDoProcessoDto>> ObterFormularioAsync(UniPlusDbContext db, Guid processoId, IUserContext usuario) =>
        await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(processoId), NovoRepositorioDeInscricao(db),
            new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs, usuario,
            CancellationToken.None);

    public async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarAsync(
        UniPlusDbContext db, Guid processoId, IUserContext usuario, Dictionary<string, JsonElement>? respostas,
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? grupos = null, Guid? faseId = null) =>
        await AvaliarInscricaoQueryHandler.Handle(new AvaliarInscricaoQuery(processoId, new AvaliacaoDaInscricaoRequest(respostas, grupos), faseId), NovoRepositorioDeInscricao(db), usuario,
            new ConsultaDaAvaliacaoDaInscricao(new PeriodoDaInscricaoResolver(new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs, Fuso()),
                new VersaoDaInscricaoResolver(new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs), new ContextoDaVersaoFixadaResolver(ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs),
                new DocumentosEnviadosDaInscricao(Substitute.For<IDocumentoDaInscricaoRepository>()), new PreparacaoDaAvaliacaoDaInscricao(Substitute.For<IConferenciaEnderecoGeo>()), Fuso(), EnderecosSemCopia()), DentroDoPeriodo, CancellationToken.None);

    public async Task<Result<ReciboDaInscricaoDto>> EnviarAsync(UniPlusDbContext db, Guid processoId, InscricaoRequest pedido, string etag, string numero,
        IComprovanteDaInscricaoRepository? comprovantes = null, IDocumentoDaInscricaoRepository? documentos = null) =>
        await SubmeterInscricaoCommandHandler.Handle(
            new SubmeterInscricaoCommand(processoId, pedido, PrecondicaoIfMatch.DeTags([etag])),
            new InscricaoDoDono(NovoRepositorioDeInscricao(db), UsuarioComCpf(PersonasDeTeste.FernandaDrumond)),
            Submissao(db, numero, comprovantes, documentos), db, CancellationToken.None);

    private SubmissaoDaInscricao Submissao(UniPlusDbContext db, string numero,
        IComprovanteDaInscricaoRepository? comprovantes, IDocumentoDaInscricaoRepository? repositorioDeDocumentos)
    {
        IProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(db);
        ICertameDivulgadoRepository certames = new CertameDivulgadoRepository(db);
        DocumentosEnviadosDaInscricao documentos = new(repositorioDeDocumentos ?? Substitute.For<IDocumentoDaInscricaoRepository>());
        IConferenciaEnderecoGeo geo = GeoQueConfirma();
        return new SubmissaoDaInscricao(
            new PreparacaoDoEnvioDaInscricao(new PeriodoDaInscricaoResolver(certames, processos, RegistroCodecs, Fuso()),
                new VersaoDaInscricaoResolver(certames, processos, RegistroCodecs), new ContextoDaVersaoFixadaResolver(processos, RegistroCodecs), DentroDoPeriodo),
            new ConferenciaDoEnvioDaInscricao(new ConferenciaDoEnderecoDaInscricao(geo), new ConferenciaDosMunicipiosDaInscricao(geo),
                documentos, Fuso(), UsuarioComCpf(PersonasDeTeste.FernandaDrumond)),
            new RegistroDoEnvioDaInscricao(NovoRepositorioDeInscricao(db), comprovantes ?? NovoRepositorioDeComprovantes(db), GeradorQueDevolve(numero), documentos));
    }

    public async Task<Result<InscricaoDto>> SalvarAsync(
        UniPlusDbContext db, Guid processoId, InscricaoRequest pedido, string etag, IConferenciaEnderecoGeo? geo = null)
    {
        IInscricaoRepository inscricoes = NovoRepositorioDeInscricao(db);
        IConferenciaEnderecoGeo geoDoSalvar = geo ?? GeoQueConfirma();
        IProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(db);
        ICertameDivulgadoRepository certames = new CertameDivulgadoRepository(db);
        IResolvedorFusoInstitucional fuso = Fuso();
        return await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(processoId, pedido, PrecondicaoIfMatch.DeTags([etag])),
            new RascunhoDoCandidato(
                new InscricaoAtivaDoCandidato(inscricoes), new PeriodoDaInscricaoResolver(certames, processos, RegistroCodecs, fuso),
                new VersaoDaInscricaoResolver(certames, processos, RegistroCodecs), new ContextoDaVersaoFixadaResolver(processos, RegistroCodecs), fuso, DentroDoPeriodo),
            new SalvamentoDoRascunho(new ConferenciaDoEnderecoDaInscricao(geoDoSalvar), inscricoes, db),
            new RespostaDoRascunho(new DocumentosEnviadosDaInscricao(Substitute.For<IDocumentoDaInscricaoRepository>()), new PreparacaoDaAvaliacaoDaInscricao(geoDoSalvar), EnderecosSemCopia()),
            UsuarioComCpf(PersonasDeTeste.FernandaDrumond),
            CancellationToken.None);
    }

    public async Task<(Inscricao Inscricao, IReadOnlyList<ComprovanteInscricao> Comprovantes)> RelerAsync(Guid processoId)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        InscricaoRepository repositorio = RepositoriosDaInscricaoDeTeste.Inscricoes(leitura, _fixture);
        Inscricao inscricao = (await repositorio.ObterAtivaPorProcessoECpfAsync(processoId, PersonasDeTeste.FernandaDrumond, CancellationToken.None))!;
        return (inscricao, await NovoRepositorioDeComprovantes(leitura).ObterComprovantesAsync([inscricao.Id], CancellationToken.None));
    }

    public async Task<List<(int Posicao, string Origem, string Codigo)>> LinhasDaModalidadeAsync(Guid inscricaoId)
    {
        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT posicao, origem, valor_codigo FROM selecao.respostas_da_inscricao
            WHERE inscricao_id = @id AND codigo_do_dado = 'MODALIDADE' ORDER BY posicao
            """;
        command.Parameters.AddWithValue("id", inscricaoId);
        return await LerLinhasDaModalidadeAsync(command);
    }

    private static async Task<List<(int, string, string)>> LerLinhasDaModalidadeAsync(NpgsqlCommand command)
    {
        List<(int, string, string)> linhas = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            linhas.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return linhas;
    }

    /// <summary>Dentro da janela da fase que coleta a inscrição no processo publicado por este teste.</summary>
    internal static readonly TimeProvider DentroDoPeriodo = new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Os endereços dos documentos do ato: estes testes semeiam a divulgação sem a cópia ao acervo, e nenhum é exposto.</summary>
    internal static EnderecosDosDocumentosDoAto EnderecosSemCopia() =>
        new(Substitute.For<ICertameDivulgadoRepository>(), Substitute.For<IEnderecoNoAcervoPublico>());

    internal static IResolvedorFusoInstitucional Fuso()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    internal static IUserContext UsuarioComCpf(Cpf cpf)
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(cpf.Valor);
        contexto.NomeCompleto.Returns(PersonasDeTeste.NomeFernandaDrumond);
        contexto.UserId.Returns("conta-fernanda-drumond");
        contexto.Issuer.Returns("https://idp.unifesspa.edu.br/realms/unifesspa");
        return contexto;
    }

    internal static InscricaoRequest PedidoDeEnvio(ProcessoSeletivo processo, IReadOnlyDictionary<string, JsonElement> doProcesso)
    {
        Dictionary<string, JsonElement> respostas = RespostasDeIdentificacao();
        AcrescentarDeclaracoesPessoais(respostas);
        AcrescentarContatos(respostas);
        foreach ((string fato, JsonElement valor) in doProcesso)
        {
            respostas[fato] = valor;
        }

        TermoExigidoFormulario termo = processo.TermosExigidos.Single();
        return new InscricaoRequest(
            processo.DistribuicaoVagas.Single().OfertaCursoOrigemId,
            respostas,
            GruposDoPedido(),
            [new AceiteDeTermoInput(termo.Codigo, termo.VersaoId, termo.HashVersao)]);
    }

    private static Dictionary<string, JsonElement> RespostasDeIdentificacao() => new(StringComparer.Ordinal)
    {
        ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
        ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
        ["NACIONALIDADE"] = JsonSerializer.SerializeToElement("BRASILEIRA"),
        ["CPF"] = JsonSerializer.SerializeToElement(PersonasDeTeste.FernandaDrumond.Valor),
        ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
        ["RG_ORGAO_EMISSOR"] = JsonSerializer.SerializeToElement("SSP"),
        ["RG_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["RG_DATA_EMISSAO"] = JsonSerializer.SerializeToElement("2010-01-01"),
        ["DATA_NASCIMENTO"] = JsonSerializer.SerializeToElement("2000-01-01"),
        ["NATURALIDADE_UF"] = JsonSerializer.SerializeToElement("PA"),
        ["NATURALIDADE_MUNICIPIO"] = JsonSerializer.SerializeToElement("1504208"),
    };

    private static void AcrescentarDeclaracoesPessoais(Dictionary<string, JsonElement> respostas)
    {
        respostas["NOME_MAE"] = JsonSerializer.SerializeToElement("Maria Drumond");
        respostas["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("SOLTEIRO");
        respostas["SEXO"] = JsonSerializer.SerializeToElement("FEMININO");
        respostas["SEXO_CIVIL"] = JsonSerializer.SerializeToElement("FEMININO");
        respostas["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR");
        respostas["ORIENTACAO_SEXUAL"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR");
        respostas["COR_RACA"] = JsonSerializer.SerializeToElement("NAO_DECLARADO");
    }

    private static void AcrescentarContatos(Dictionary<string, JsonElement> respostas)
    {
        respostas["EMAIL"] = JsonSerializer.SerializeToElement("fernanda.drumond@example.com");
        respostas["TELEFONE"] = JsonSerializer.SerializeToElement("94999999999");
        respostas["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO");
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "68500000", cidade = new { codigoIbge = "1504208", uf = "PA" } });
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> GruposDoPedido() =>
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricaoInput("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO_MEMBRO"] = JsonSerializer.SerializeToElement("7654321"),
                        ["PARENTESCO"] = JsonSerializer.SerializeToElement("Mãe"),
                    }),
                ],
            };

    internal static IConferenciaEnderecoGeo GeoQueConfirma()
    {
        IConferenciaEnderecoGeo geo = Substitute.For<IConferenciaEnderecoGeo>();
        geo.ConferirMunicipioAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        geo.ConferirCepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        geo.ResolverCepAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<CepResolvido>.Success(new CepResolvido("1504208", "Marabá", "PA", "logradouro")));
        return geo;
    }

    internal static IGeradorDeNumeroInscricao GeradorQueDevolve(string numero)
    {
        IGeradorDeNumeroInscricao gerador = Substitute.For<IGeradorDeNumeroInscricao>();
        gerador.ProximoAsync(Arg.Any<CancellationToken>()).Returns(numero);
        return gerador;
    }
}
