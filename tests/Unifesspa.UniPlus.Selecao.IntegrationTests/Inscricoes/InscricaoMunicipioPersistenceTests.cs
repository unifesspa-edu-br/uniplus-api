namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Prova, contra Postgres real, que o município é gravado e relido pela forma da situação da inscrição: só o código no
/// rascunho, e o código com o nome conferido na inscrição enviada — na coluna do tipo e na cifrada.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InscricaoMunicipioPersistenceTests : IClassFixture<InscricaoDbFixture>
{
    private const string Salvador = "2927408";
    private const string Maraba = "1504208";

    private static readonly Guid Versao = Guid.CreateVersion7();

    private readonly InscricaoDbFixture _fixture;

    public InscricaoMunicipioPersistenceTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private static Dictionary<string, DefinicaoDaResposta> Definicoes(bool cifrada = false) => new(StringComparer.Ordinal)
    {
        ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["NATURALIDADE_MUNICIPIO"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: cifrada),
        ["MUNICIPIO_RESIDENCIA"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: false),
    };

    private static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor);

    private static JsonElement Gravado(string codigo, string rotulo) => ValorDeMunicipio.Gravado(codigo, rotulo);

    private static Inscricao Reidratada(
        SituacaoInscricao situacao,
        Dictionary<string, JsonElement> respostas,
        Dictionary<string, JsonElement> derivados,
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        Dictionary<string, DefinicaoDaResposta> definicoes)
    {
        respostas["CPF"] = Json(PersonasDeTeste.ErickIsaacRamos.Valor);
        return Inscricao.Reidratar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), PersonasDeTeste.ErickIsaacRamos, "sub-erick", "https://issuer", situacao, Versao,
            ofertaCursoId: null, ofertaCursoSegundaOpcaoId: null,
            numeroInscricao: situacao == SituacaoInscricao.Rascunho ? null : "000900",
            canceladaEm: null, canceladaPor: null, motivoDoCancelamento: null,
            respostas, derivados, grupos, [], definicoes, revisao: 1, DateTimeOffset.UtcNow, updatedAt: null);
    }

    private async Task<Inscricao> GravarELerAsync(Inscricao inscricao)
    {
        await using (UniPlusDbContext escrita = _fixture.CreateDbContext())
        {
            await new InscricaoRepository(escrita, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System).AdicionarAsync(inscricao);
            await escrita.SaveChangesAsync();
        }

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        return (await new InscricaoRepository(leitura, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterPorIdAsync(inscricao.Id))!;
    }

    [Fact(DisplayName = "O rascunho grava o município só com o código e o relê como texto, no nível superior e no grupo")]
    public async Task Rascunho_GravaSoOCodigo()
    {
        Inscricao rascunho = Reidratada(
            SituacaoInscricao.Rascunho,
            new(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = Json(Salvador) },
            new(StringComparer.Ordinal),
            new(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new OcorrenciaDeGrupoInscricao("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = Json(Maraba) })],
            },
            Definicoes());

        Inscricao lida = await GravarELerAsync(rascunho);

        lida.Respostas["NATURALIDADE_MUNICIPIO"].GetString().Should().Be(Salvador);
        lida.Grupos["LOCAIS"].Single().Respostas["NATURALIDADE_MUNICIPIO"].GetString().Should().Be(Maraba);
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT valor_codigo, valor_rotulo FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id AND fato_codigo = 'NATURALIDADE_MUNICIPIO' AND ocorrencia_id IS NULL";
        comando.Parameters.AddWithValue("id", rascunho.Id);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();
        leitor["valor_codigo"].Should().Be(Salvador);
        leitor["valor_rotulo"].Should().BeOfType<DBNull>();
    }

    [Fact(DisplayName = "A inscrição enviada grava o município com o nome conferido e o relê igual, na resposta, no grupo e no derivado")]
    public async Task Enviada_GravaOCodigoEONome()
    {
        Inscricao enviada = Reidratada(
            SituacaoInscricao.Submetida,
            new(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = Gravado(Salvador, "Salvador") },
            new(StringComparer.Ordinal) { ["MUNICIPIO_RESIDENCIA"] = Gravado(Maraba, "Marabá") },
            new(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new OcorrenciaDeGrupoInscricao("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = Gravado(Maraba, "Marabá") })],
            },
            Definicoes());

        Inscricao lida = await GravarELerAsync(enviada);

        lida.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(Gravado(Salvador, "Salvador").GetRawText());
        lida.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(Gravado(Maraba, "Marabá").GetRawText());
        lida.Grupos["LOCAIS"].Single().Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(Gravado(Maraba, "Marabá").GetRawText());
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT valor_codigo, valor_rotulo FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id AND fato_codigo = 'NATURALIDADE_MUNICIPIO' AND ocorrencia_id IS NULL";
        comando.Parameters.AddWithValue("id", enviada.Id);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();
        leitor["valor_codigo"].Should().Be(Salvador);
        leitor["valor_rotulo"].Should().Be("Salvador");
    }

    [Theory(DisplayName = "O município na coluna cifrada volta como foi gravado: texto no rascunho e objeto na inscrição enviada")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cifrada_VoltaComoFoiGravada(bool enviada)
    {
        JsonElement valor = enviada ? Gravado(Salvador, "Salvador") : Json(Salvador);
        Inscricao inscricao = Reidratada(
            enviada ? SituacaoInscricao.Submetida : SituacaoInscricao.Rascunho,
            new(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = valor },
            new(StringComparer.Ordinal),
            new(StringComparer.Ordinal),
            Definicoes(cifrada: true));

        Inscricao lida = await GravarELerAsync(inscricao);

        lida.Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(valor.GetRawText());
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT valor_codigo, valor_rotulo, valor_cifrado FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id AND fato_codigo = 'NATURALIDADE_MUNICIPIO'";
        comando.Parameters.AddWithValue("id", inscricao.Id);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();
        leitor["valor_codigo"].Should().BeOfType<DBNull>();
        leitor["valor_rotulo"].Should().BeOfType<DBNull>();
        leitor["valor_cifrado"].Should().BeOfType<byte[]>();
    }

    [Theory(DisplayName = "O município cifrado dentro de grupo volta como foi gravado: texto no rascunho e objeto na inscrição enviada")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CifradaEmGrupo_VoltaComoFoiGravada(bool enviada)
    {
        JsonElement valor = enviada ? Gravado(Salvador, "Salvador") : Json(Salvador);
        Inscricao inscricao = Reidratada(
            enviada ? SituacaoInscricao.Submetida : SituacaoInscricao.Rascunho,
            new(StringComparer.Ordinal),
            new(StringComparer.Ordinal),
            new(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new OcorrenciaDeGrupoInscricao("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = valor })],
            },
            Definicoes(cifrada: true));

        Inscricao lida = await GravarELerAsync(inscricao);

        lida.Grupos["LOCAIS"].Single().Respostas["NATURALIDADE_MUNICIPIO"].GetRawText().Should().Be(valor.GetRawText());
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT valor_codigo, valor_rotulo, valor_cifrado FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id AND fato_codigo = 'NATURALIDADE_MUNICIPIO'";
        comando.Parameters.AddWithValue("id", inscricao.Id);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        (await leitor.ReadAsync()).Should().BeTrue();
        leitor["valor_codigo"].Should().BeOfType<DBNull>();
        leitor["valor_rotulo"].Should().BeOfType<DBNull>();
        leitor["valor_cifrado"].Should().BeOfType<byte[]>();
    }

    [Theory(DisplayName = "O município na forma que a situação não grava é defeito de quem grava: o rascunho não leva nome, e a enviada não leva só o código")]
    [InlineData(SituacaoInscricao.Rascunho, true)]
    [InlineData(SituacaoInscricao.Submetida, false)]
    public async Task FormaTrocada_NaoGrava(SituacaoInscricao situacao, bool comNome)
    {
        Inscricao inscricao = Reidratada(
            situacao,
            new(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = comNome ? Gravado(Salvador, "Salvador") : Json(Salvador) },
            new(StringComparer.Ordinal),
            new(StringComparer.Ordinal),
            Definicoes());
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();

        Func<Task> gravar = () => new InscricaoRepository(contexto, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System).AdicionarAsync(inscricao);

        await gravar.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "O comprovante grava e relê o município com o nome, no grupo e no derivado, e o hash de integridade confere depois de reler")]
    public async Task Comprovante_GravaERele_OMunicipioComNomeEOHashConfere()
    {
        Guid oferta = Guid.CreateVersion7();
        Dictionary<string, DefinicaoDaResposta> definicoes = Definicoes();
        RespostasAdmitidas admitidas = new(
            definicoes,
            new HashSet<string>(StringComparer.Ordinal) { "CPF", "NATURALIDADE_MUNICIPIO" },
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal) { ["LOCAIS"] = new HashSet<string>(StringComparer.Ordinal) { "NATURALIDADE_MUNICIPIO" } },
            new HashSet<Guid> { oferta },
            new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal));
        Inscricao inscricao = Inscricao.Iniciar(Guid.CreateVersion7(), PersonasDeTeste.ErickIsaacRamos, "sub-erick", "https://issuer", Versao).Value!;
        Dictionary<string, DefinicaoDaResposta> derivados = new(StringComparer.Ordinal)
        {
            ["MUNICIPIO_RESIDENCIA"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: false),
        };

        ComprovanteInscricao comprovante = inscricao.Submeter(
            "000901", oferta, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = Json(Salvador) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new OcorrenciaDeGrupoInscricao("local-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["NATURALIDADE_MUNICIPIO"] = Json(Maraba) })],
            },
            [], admitidas, FormularioDeTeste.AvaliacaoSemOcultos,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["MUNICIPIO_RESIDENCIA"] = Json(Salvador) }, derivados,
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), DateTimeOffset.UtcNow, [],
            new MunicipiosConferidos(
                new Dictionary<string, string>(StringComparer.Ordinal) { [Salvador] = "Salvador", [Maraba] = "Marabá" },
                new Dictionary<string, CidadeConferida>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["MUNICIPIO_RESIDENCIA"] = "Salvador" })).Value!;

        await using (UniPlusDbContext escrita = _fixture.CreateDbContext())
        {
            InscricaoRepository repositorio = new(escrita, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
            await repositorio.AdicionarAsync(inscricao);
            await repositorio.AdicionarComprovanteAsync(comprovante);
            await escrita.SaveChangesAsync();
        }

        await using UniPlusDbContext leitura = _fixture.CreateDbContext();
        ComprovanteInscricao lido = (await new InscricaoRepository(leitura, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterComprovantesAsync([inscricao.Id])).Single();

        lido.IntegridadeConfere().Should().BeTrue();
        lido.Respostas["NATURALIDADE_MUNICIPIO"].GetProperty("rotulo").GetString().Should().Be("Salvador");
        lido.Derivados["MUNICIPIO_RESIDENCIA"].GetProperty("rotulo").GetString().Should().Be("Salvador");
        lido.Grupos["LOCAIS"].Single().Respostas["NATURALIDADE_MUNICIPIO"].GetProperty("rotulo").GetString().Should().Be("Marabá");
    }
}
