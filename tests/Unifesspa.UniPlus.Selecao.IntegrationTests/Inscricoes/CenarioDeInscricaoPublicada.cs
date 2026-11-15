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
/// <see cref="CertameDivulgadoRepository"/>) e o codec vivo de produção (<see cref="RegistroCodecsEnvelope"/>),
/// em vez de um grafo fabricado à parte com repositórios substitutos.
/// </summary>
internal sealed class CenarioDeInscricaoPublicada
{
    private readonly InscricaoDbFixture _fixture;

    public CenarioDeInscricaoPublicada(InscricaoDbFixture fixture) => _fixture = fixture;

    internal static IRegistroCodecsEnvelope RegistroCodecs { get; } = new RegistroCodecsEnvelope();

    public string StringDeConexao => _fixture.ConnectionString;

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public InscricaoRepository NovoRepositorioDeInscricao(UniPlusDbContext db) =>
        new(db, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);

    /// <summary>Inicia a inscrição do usuário no processo publicado, sem conferência de endereço pelo geo.</summary>
    public async Task<Result<InscricaoDto>> IniciarRascunhoAsync(
        UniPlusDbContext db, Guid processoId, IUserContext usuario, IConferenciaEnderecoGeo? geo = null) =>
        await IniciarInscricaoCommandHandler.Handle(
            new IniciarInscricaoCommand(processoId),
            NovoRepositorioDeInscricao(db), new CertameDivulgadoRepository(db),
            ProcessoSeletivoRepositoryDeTeste.Criar(db), Substitute.For<IDocumentoDaInscricaoRepository>(), RegistroCodecs, Fuso(), geo ?? Substitute.For<IConferenciaEnderecoGeo>(), EnderecosSemCopia(),
            DentroDoPeriodo, db, usuario, CancellationToken.None);

    /// <summary>Inicia a inscrição da Fernanda no processo publicado e devolve o ETag com que ela é enviada.</summary>
    public async Task<string> IniciarComoFernandaAsync(UniPlusDbContext db, Guid processoId)
    {
        Result<InscricaoDto> iniciar = await IniciarRascunhoAsync(db, processoId, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), GeoQueConfirma());
        iniciar.IsSuccess.Should().BeTrue(iniciar.Error?.Message);
        return iniciar.Value!.ETag;
    }

    public async Task<Result<InscricaoDto>> ObterAsync(UniPlusDbContext db, Guid processoId, IUserContext usuario) =>
        await ObterInscricaoQueryHandler.Handle(
            new ObterInscricaoQuery(processoId), NovoRepositorioDeInscricao(db), new CertameDivulgadoRepository(db),
            ProcessoSeletivoRepositoryDeTeste.Criar(db), Substitute.For<IDocumentoDaInscricaoRepository>(), RegistroCodecs, Fuso(),
            Substitute.For<IConferenciaEnderecoGeo>(), EnderecosSemCopia(), usuario, DentroDoPeriodo, CancellationToken.None);

    public async Task<Result<FormularioRenderizavelDto>> ObterFormularioAsync(UniPlusDbContext db, Guid processoId, IUserContext usuario) =>
        await ObterFormularioDaInscricaoQueryHandler.Handle(
            new ObterFormularioDaInscricaoQuery(processoId), NovoRepositorioDeInscricao(db),
            new CertameDivulgadoRepository(db), ProcessoSeletivoRepositoryDeTeste.Criar(db), RegistroCodecs, usuario,
            CancellationToken.None);

    public async Task<Result<AvaliacaoDaInscricaoDto>> AvaliarAsync(
        UniPlusDbContext db, Guid processoId, IUserContext usuario, Dictionary<string, JsonElement>? respostas) =>
        await AvaliarInscricaoQueryHandler.Handle(
            new AvaliarInscricaoQuery(processoId, new AvaliacaoDaInscricaoRequest(respostas, null)),
            NovoRepositorioDeInscricao(db),
            ProcessoSeletivoRepositoryDeTeste.Criar(db), Substitute.For<IDocumentoDaInscricaoRepository>(), new CertameDivulgadoRepository(db),
            RegistroCodecs, Fuso(), Substitute.For<IConferenciaEnderecoGeo>(), EnderecosSemCopia(), usuario, DentroDoPeriodo, CancellationToken.None);

    public async Task<Result<ReciboDaInscricaoDto>> EnviarAsync(UniPlusDbContext db, Guid processoId, InscricaoRequest pedido, string etag, string numero) =>
        await SubmeterInscricaoCommandHandler.Handle(
            new SubmeterInscricaoCommand(processoId, pedido, PrecondicaoIfMatch.DeTags([etag])),
            NovoRepositorioDeInscricao(db),
            ProcessoSeletivoRepositoryDeTeste.Criar(db), new CertameDivulgadoRepository(db), Substitute.For<IDocumentoDaInscricaoRepository>(), RegistroCodecs, Fuso(), GeoQueConfirma(),
            GeradorQueDevolve(numero), db, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), DentroDoPeriodo, CancellationToken.None);

    public async Task<Result<InscricaoDto>> SalvarAsync(
        UniPlusDbContext db, Guid processoId, InscricaoRequest pedido, string etag, IConferenciaEnderecoGeo? geo = null) =>
        await SalvarInscricaoCommandHandler.Handle(
            new SalvarInscricaoCommand(processoId, pedido, PrecondicaoIfMatch.DeTags([etag])),
            NovoRepositorioDeInscricao(db),
            ProcessoSeletivoRepositoryDeTeste.Criar(db), Substitute.For<IDocumentoDaInscricaoRepository>(), new CertameDivulgadoRepository(db), RegistroCodecs, Fuso(), geo ?? GeoQueConfirma(),
            EnderecosSemCopia(), db, UsuarioComCpf(PersonasDeTeste.FernandaDrumond), DentroDoPeriodo, CancellationToken.None);

    public async Task<(Inscricao Inscricao, IReadOnlyList<ComprovanteInscricao> Comprovantes)> RelerAsync(Guid processoId)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        InscricaoRepository repositorio = new(leitura, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao inscricao = (await repositorio.ObterAtivaPorProcessoECpfAsync(processoId, PersonasDeTeste.FernandaDrumond, CancellationToken.None))!;
        return (inscricao, await repositorio.ObterComprovantesAsync([inscricao.Id], CancellationToken.None));
    }

    public async Task<List<(int Posicao, int Origem, string Codigo)>> LinhasDaModalidadeAsync(Guid inscricaoId)
    {
        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT posicao, origem, valor_codigo FROM selecao.respostas_da_inscricao
            WHERE inscricao_id = @id AND fato_codigo = 'MODALIDADE' ORDER BY posicao
            """;
        command.Parameters.AddWithValue("id", inscricaoId);
        List<(int, int, string)> linhas = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            linhas.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2)));
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
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
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
            ["NOME_MAE"] = JsonSerializer.SerializeToElement("Maria Drumond"),
            ["ESTADO_CIVIL"] = JsonSerializer.SerializeToElement("SOLTEIRO"),
            ["SEXO"] = JsonSerializer.SerializeToElement("FEMININO"),
            ["SEXO_CIVIL"] = JsonSerializer.SerializeToElement("FEMININO"),
            ["IDENTIDADE_GENERO"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
            ["ORIENTACAO_SEXUAL"] = JsonSerializer.SerializeToElement("PREFIRO_NAO_INFORMAR"),
            ["COR_RACA"] = JsonSerializer.SerializeToElement("NAO_DECLARADO"),
            ["EMAIL"] = JsonSerializer.SerializeToElement("fernanda.drumond@example.com"),
            ["TELEFONE"] = JsonSerializer.SerializeToElement("94999999999"),
            ["TIPO_ENDERECO"] = JsonSerializer.SerializeToElement("URBANO"),
            ["ENDERECO_RESIDENCIAL"] = JsonSerializer.SerializeToElement(new { cep = "68500000", cidade = new { codigoIbge = "1504208", uf = "PA" } }),
        };
        foreach ((string fato, JsonElement valor) in doProcesso)
        {
            respostas[fato] = valor;
        }

        TermoExigidoFormulario termo = processo.TermosExigidos.Single();
        return new InscricaoRequest(
            processo.DistribuicaoVagas.Single().OfertaCursoOrigemId,
            respostas,
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
            },
            [new AceiteDeTermoInput(termo.Codigo, termo.VersaoId, termo.HashVersao)]);
    }

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
