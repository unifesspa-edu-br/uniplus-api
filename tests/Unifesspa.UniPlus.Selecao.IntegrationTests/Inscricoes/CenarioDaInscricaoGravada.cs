namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A montagem que as classes de teste da inscrição gravada compartilham: o Postgres real da fixture, os dados comuns
/// (definições das respostas, o que o processo admite, a inscrição nova) e as operações de gravar, reler e consultar a
/// linha crua pelo repositório da inscrição.
/// </summary>
internal sealed class CenarioDaInscricaoGravada
{
    internal static readonly Guid OfertaDoProcesso = Guid.CreateVersion7();
    internal static readonly Guid VersaoDoTermoVeracidade = Guid.CreateVersion7();

    internal static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas = new(StringComparer.Ordinal)
    {
        ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["NOME_SOCIAL"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        ["DESEJA_NOME_SOCIAL"] = new(TipoDoValorDeResposta.SimOuNao, Multivalorado: false, Identificador: false),
        ["DATA_NASCIMENTO"] = new(TipoDoValorDeResposta.Data, Multivalorado: false, Identificador: false),
        ["MODALIDADES"] = new(TipoDoValorDeResposta.Opcao, Multivalorado: true, Identificador: false),
        ["ENDERECO_RESIDENCIAL"] = new(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false),
        ["RG_NUMERO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["PARENTESCO"] = new(TipoDoValorDeResposta.Opcao, Multivalorado: false, Identificador: false),
        ["SEXO_CIVIL"] = new(TipoDoValorDeResposta.Opcao, Multivalorado: false, Identificador: false),
        ["IDENTIDADE_GENERO"] = new(TipoDoValorDeResposta.Opcao, Multivalorado: false, Identificador: false, CifraIndividual: true),
        ["ORIENTACAO_SEXUAL"] = new(TipoDoValorDeResposta.Opcao, Multivalorado: false, Identificador: false, CifraIndividual: true),
    };

    internal static readonly RespostasAdmitidas Admitidas = new(
        DefinicoesDasRespostas,
        new HashSet<string>(StringComparer.Ordinal) { "CPF", "NOME_SOCIAL", "DESEJA_NOME_SOCIAL", "DATA_NASCIMENTO", "MODALIDADES", "ENDERECO_RESIDENCIAL", "RG_NUMERO", "SEXO_CIVIL", "IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = new HashSet<string>(StringComparer.Ordinal) { "RG_NUMERO", "PARENTESCO" },
        },
        new HashSet<Guid> { OfertaDoProcesso },
        new Dictionary<TermoNoFormulario, TermoAdmitido>() { [FormularioDeTeste.TermoDaInscricao("TERMO_VERACIDADE")] = new(VersaoDoTermoVeracidade, "sha256-abc") });

    internal static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDosDerivados = new(StringComparer.Ordinal)
    {
        ["FAIXA_ETARIA"] = new(TipoDoValorDeResposta.Numero, Multivalorado: false, Identificador: false),
        ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Opcao, Multivalorado: false, Identificador: false),
    };

    private static readonly Guid VersaoFormulario = Guid.CreateVersion7();

    private readonly InscricaoDbFixture _fixture;

    public CenarioDaInscricaoGravada(InscricaoDbFixture fixture) => _fixture = fixture;

    public IUniPlusEncryptionService Encryption => _fixture.Encryption;

    internal static Inscricao NovaInscricao(
        Cpf cpf, Guid processo, string conta = "conta-1", string emissor = "https://issuer", Guid? versaoFormulario = null)
    {
        Result<Inscricao> inicio = Inscricao.Iniciar(processo, cpf, conta, emissor, versaoFormulario ?? VersaoFormulario);
        inicio.IsSuccess.Should().BeTrue();
        return inicio.Value!;
    }

    internal static PrecondicaoIfMatch PrecondicaoDe(Inscricao inscricao) => PrecondicaoIfMatch.DeTags([inscricao.ETag]);

    internal static AceiteDeTermo AceiteDoTermoDeVeracidade() => new(FormularioDeTeste.CodigoDaInscricao, "TERMO_VERACIDADE", VersaoDoTermoVeracidade, "sha256-abc");

    internal static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() => new(StringComparer.Ordinal);

    internal static Dictionary<string, JsonElement> Respostas(params (string Codigo, object Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Codigo, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);

    internal static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Familia(params string[] membros) =>
        new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                .. membros.Select(m => new OcorrenciaDeGrupoInscricao(m, Respostas(("RG_NUMERO", $"rg-{m}"), ("PARENTESCO", "FILHO")))),
            ],
        };

    /// <summary>Envia a inscrição com o conteúdo dado, ou com o envio mais simples quando não se dá nenhum.</summary>
    internal static Result<ComprovanteInscricao> Enviar(Inscricao inscricao, string numero, ConteudoDoEnvio? conteudo = null)
    {
        ConteudoDoEnvio envio = conteudo ?? new ConteudoDoEnvio();

        return inscricao.Submeter(numero,
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, envio.SegundaOfertaDeCurso), envio.Respostas, SemGrupos(), []), envio.Admitidas,
            new ApuracaoDoEnvio(envio.Avaliacao, envio.Derivados, DefinicoesDosDerivados, envio.Arquivos, null), PrecondicaoDe(inscricao), envio.Instante);
    }

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public InscricaoRepository Repositorio(UniPlusDbContext contexto) =>
        RepositoriosDaInscricaoDeTeste.Inscricoes(contexto, _fixture);

    public ComprovanteDaInscricaoRepository Comprovantes(UniPlusDbContext contexto) =>
        RepositoriosDaInscricaoDeTeste.Comprovantes(contexto, _fixture);

    /// <summary>Grava a inscrição e os comprovantes dela, na ordem dada, numa só transação.</summary>
    public async Task GravarAsync(Inscricao inscricao, params ComprovanteInscricao[] comprovantes)
    {
        await using UniPlusDbContext contexto = CriarDbContext();
        InscricaoRepository repositorio = Repositorio(contexto);
        await repositorio.AdicionarAsync(inscricao);
        foreach (ComprovanteInscricao comprovante in comprovantes)
        {
            await RepositoriosDaInscricaoDeTeste.Comprovantes(contexto, _fixture).AdicionarComprovanteAsync(comprovante);
        }

        await contexto.SaveChangesAsync();
    }

    public async Task<Inscricao> LerAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext contexto = CriarDbContext();
        return (await Repositorio(contexto).ObterPorIdAsync(inscricaoId))!;
    }

    public async Task<Inscricao> LerAtivaAsync(Inscricao inscricao, Cpf cpf)
    {
        await using UniPlusDbContext contexto = CriarDbContext();
        return (await Repositorio(contexto).ObterAtivaPorProcessoECpfAsync(inscricao.ProcessoSeletivoId, cpf))!;
    }

    public async Task<IReadOnlyList<ComprovanteInscricao>> ComprovantesGravadosAsync(Guid inscricaoId)
    {
        await using UniPlusDbContext contexto = CriarDbContext();
        return await RepositoriosDaInscricaoDeTeste.Comprovantes(contexto, _fixture).ObterComprovantesAsync([inscricaoId], CancellationToken.None);
    }

    public async Task CancelarAsync(Guid inscricaoId, string? motivo, DateTimeOffset instante)
    {
        await using UniPlusDbContext contexto = CriarDbContext();
        InscricaoRepository repositorio = Repositorio(contexto);
        Inscricao lida = (await repositorio.ObterPorIdAsync(inscricaoId))!;
        lida.Cancelar(motivo, "conta-1", instante, PrecondicaoDe(lida)).IsSuccess.Should().BeTrue();
        await repositorio.AtualizarAsync(lida);
        await contexto.SaveChangesAsync();
    }

    public async Task<NpgsqlConnection> AbrirConexaoAsync()
    {
        NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        return conexao;
    }

    /// <summary>Lê a linha crua do banco, sem passar pelo repositório: a consulta recebe o identificador da inscrição em <c>@id</c>.</summary>
    public async Task<List<T>> ConsultarAsync<T>(string consulta, Guid inscricaoId, Func<NpgsqlDataReader, T> ler)
    {
        await using NpgsqlConnection conexao = await AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(consulta, conexao);
        comando.Parameters.AddWithValue("id", inscricaoId);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        List<T> linhas = [];
        while (await leitor.ReadAsync())
        {
            linhas.Add(ler(leitor));
        }

        return linhas;
    }

    /// <summary>Altera a linha crua do banco, sem passar pelo repositório: o comando recebe o identificador da inscrição em <c>@id</c>.</summary>
    public async Task<int> ExecutarAsync(string comandoSql, Guid inscricaoId)
    {
        await using NpgsqlConnection conexao = await AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(comandoSql, conexao);
        comando.Parameters.AddWithValue("id", inscricaoId);

        return await comando.ExecuteNonQueryAsync();
    }
}
