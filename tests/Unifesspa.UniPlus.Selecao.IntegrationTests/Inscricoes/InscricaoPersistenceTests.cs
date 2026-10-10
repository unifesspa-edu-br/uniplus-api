namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Prova, contra Postgres real (Testcontainers), a gravação da inscrição do candidato: a linha
/// crua não contém o CPF nem o número de documento em texto claro (ADR-0121), cada valor fica na
/// coluna do tipo com a origem (ADR-0141), o índice único por (processo, CPF) funciona, e o
/// round-trip via repositório devolve a inscrição inteira — inclusive os documentos dentro de
/// grupos repetíveis.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InscricaoPersistenceTests : IClassFixture<InscricaoDbFixture>
{
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid VersaoFormulario = Guid.CreateVersion7();

    private static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDasRespostas = new(StringComparer.Ordinal)
    {
        ["NOME_SOCIAL"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        ["DESEJA_NOME_SOCIAL"] = new(TipoDoValorDeResposta.Booleano, Multivalorado: false, Identificador: false),
        ["DATA_NASCIMENTO"] = new(TipoDoValorDeResposta.Data, Multivalorado: false, Identificador: false),
        ["MODALIDADES"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: true, Identificador: false),
        ["ENDERECO_RESIDENCIAL"] = new(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false),
        ["RG_NUMERO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
        ["PARENTESCO"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
    };

    private static readonly Guid OfertaDoProcesso = Guid.CreateVersion7();

    private static readonly RespostasAdmitidas Admitidas = new(DefinicoesDasRespostas, new HashSet<Guid> { OfertaDoProcesso });

    private static readonly Dictionary<string, DefinicaoDaResposta> DefinicoesDosDerivados = new(StringComparer.Ordinal)
    {
        ["FAIXA_ETARIA"] = new(TipoDoValorDeResposta.Numero, Multivalorado: false, Identificador: false),
        ["UF_RESIDENCIA"] = new(TipoDoValorDeResposta.Codigo, Multivalorado: false, Identificador: false),
    };

    private readonly InscricaoDbFixture _fixture;

    public InscricaoPersistenceTests(InscricaoDbFixture fixture) => _fixture = fixture;

    private static Inscricao NovaInscricao(Cpf cpf, Guid processo) =>
        Inscricao.Iniciar(processo, cpf, "sub-1", "https://issuer", VersaoFormulario).Value!;

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() => new(StringComparer.Ordinal);

    private static Dictionary<string, JsonElement> Respostas(params (string Codigo, object Valor)[] respostas) =>
        respostas.ToDictionary(static r => r.Codigo, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Familia(params string[] membros) =>
        new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                .. membros.Select(m => new OcorrenciaDeGrupoInscricao(m, Respostas(("RG_NUMERO", $"rg-{m}"), ("PARENTESCO", "FILHO")))),
            ],
        };

    [Fact(DisplayName = "Persistir e ler faz round-trip do CPF, das respostas e dos documentos dentro de um grupo")]
    public async Task Persistir_E_Ler_FazRoundTripCompleto()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        original.Salvar(
            OfertaDoProcesso,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
            },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["RG_NUMERO"] = JsonSerializer.SerializeToElement("25.062.156-3"),
                        ["PARENTESCO"] = JsonSerializer.SerializeToElement("FILHO"),
                    }),
                ],
            },
            [new AceiteDeTermo("TERMO_VERACIDADE", Guid.NewGuid(), "sha256-abc")],
            Admitidas,
            PrecondicaoIfMatch.DeTags([original.ETag]));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao? lida = await readRepository.ObterPorIdAsync(original.Id);

        lida.Should().NotBeNull();
        lida!.Cpf.Valor.Should().Be(PersonasDeTeste.FernandaDrumond.Valor);
        lida.Respostas.Should().ContainKey("RG_NUMERO").WhoseValue.GetString().Should().Be("12.868.927-4");
        lida.Respostas.Should().ContainKey("NOME_SOCIAL");
        lida.Grupos.Should().ContainKey("COMPOSICAO_FAMILIAR")
            .WhoseValue.Should().ContainSingle(o => o.Id == "membro-1")
            .Which.Respostas.Should().ContainKey("RG_NUMERO").WhoseValue.GetString().Should().Be("25.062.156-3");
        lida.Aceites.Should().ContainSingle(a => a.Codigo == "TERMO_VERACIDADE");
        lida.CreatedAt.Should().NotBe(default, "o repositório carimba CreatedAt — o AuditableInterceptor não alcança InscricaoRecord");
    }

    [Fact(DisplayName = "ObterPorProcessoECpf localiza a inscrição pelo CPF do dono, sem decifrar a tabela")]
    public async Task ObterPorProcessoECpf_LocalizaPeloDono()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, processo);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Inscricao? lida = await readRepository.ObterPorProcessoECpfAsync(processo, PersonasDeTeste.KevinCarvalho);

        lida.Should().NotBeNull();
        lida!.Id.Should().Be(original.Id);

        Inscricao? outroProcesso = await readRepository.ObterPorProcessoECpfAsync(Guid.CreateVersion7(), PersonasDeTeste.KevinCarvalho);
        outroProcesso.Should().BeNull("o mesmo CPF noutro processo não é a mesma inscrição");
    }

    [Fact(DisplayName = "A linha crua não contém o CPF nem o número de documento em texto claro")]
    public async Task ColunaCifrada_NuncaContemCpfNemDocumentoEmTextoClaro()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        original.Salvar(null, Respostas(("RG_NUMERO", "12.868.927-4")), SemGrupos(), [], Admitidas, PrecondicaoIfMatch.DeTags([original.ETag]));

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.cpf_ciphertext, r.valor_texto, r.valor_cifrado
            FROM selecao.inscricoes i JOIN selecao.respostas_da_inscricao r ON r.inscricao_id = i.id
            WHERE i.id = @id AND r.fato_codigo = 'RG_NUMERO'
            """;
        command.Parameters.AddWithValue("id", original.Id);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        Encoding.UTF8.GetString((byte[])reader["cpf_ciphertext"]).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        reader["valor_texto"].Should().Be(DBNull.Value, "o número de documento nunca fica na coluna de texto em claro");
        Encoding.UTF8.GetString((byte[])reader["valor_cifrado"]).Should().NotContain("12.868.927-4");
    }

    [Fact(DisplayName = "Cada valor fica na coluna do tipo, com a origem: informado pelo candidato ou derivado")]
    public async Task Submeter_GravaCadaValorNaColunaDoTipoComAOrigem()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        Dictionary<string, JsonElement> respostas = Respostas(
            ("DESEJA_NOME_SOCIAL", false),
            ("DATA_NASCIMENTO", "2001-03-09"),
            ("MODALIDADES", new[] { "LB_PPI", "AC" }));
        respostas["ENDERECO_RESIDENCIAL"] = JsonSerializer.Deserialize<JsonElement>(
            """{"cep":"68500000","logradouro":"Folha 31","cidade":{"codigoIbge":"1504208","nome":"Marabá","uf":"PA"},"origem":"geo-api"}""");
        original.Submeter(
            "000321", OfertaDoProcesso, respostas, SemGrupos(), [], Admitidas,
            Respostas(("FAIXA_ETARIA", 25), ("UF_RESIDENCIA", "PA")), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([original.ETag]), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(original);
        await writeContext.SaveChangesAsync();

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT fato_codigo, posicao, origem, valor_booleano, valor_numero, valor_codigo, valor_data
            FROM selecao.respostas_da_inscricao WHERE inscricao_id = @id
            """;
        command.Parameters.AddWithValue("id", original.Id);
        Dictionary<string, List<object?[]>> linhas = new(StringComparer.Ordinal);
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                object?[] colunas = new object?[7];
                reader.GetValues(colunas!);
                string fato = (string)colunas[0]!;
                linhas.TryAdd(fato, []);
                linhas[fato].Add(colunas);
            }
        }

        linhas["DESEJA_NOME_SOCIAL"].Single()[3].Should().Be(false);
        linhas["DATA_NASCIMENTO"].Single()[6].Should().Be(new DateOnly(2001, 3, 9));
        linhas["MODALIDADES"].Select(static l => ((int)l[1]!, (string)l[5]!)).Should().BeEquivalentTo([(0, "LB_PPI"), (1, "AC")]);
        linhas["FAIXA_ETARIA"].Single()[4].Should().Be(25m);
        linhas["UF_RESIDENCIA"].Single()[2].Should().Be((int)OrigemDaResposta.Derivada);
        linhas["DATA_NASCIMENTO"].Single()[2].Should().Be((int)OrigemDaResposta.Informada);

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Inscricao lida = (await new InscricaoRepository(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterPorIdAsync(original.Id))!;
        lida.Derivados.Should().ContainKey("UF_RESIDENCIA");
        lida.Respostas.Should().NotContainKey("UF_RESIDENCIA");
        lida.Respostas["MODALIDADES"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("LB_PPI", "AC");
        lida.Respostas["ENDERECO_RESIDENCIAL"].GetProperty("cidade").GetProperty("codigoIbge").GetString().Should().Be("1504208");
    }

    [Fact(DisplayName = "Regravar troca, acrescenta e remove linhas, inclusive ocorrências de grupo, sem colidir no índice único")]
    public async Task Atualizar_SincronizaAsLinhas()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        original.Salvar(
            null, Respostas(("NOME_SOCIAL", "Ariel"), ("MODALIDADES", new[] { "AC", "LB_PPI" })), Familia("membro-1", "membro-2"), [],
            Admitidas, PrecondicaoIfMatch.DeTags([original.ETag]));

        await using (SelecaoDbContext context = _fixture.CreateDbContext())
        {
            await new InscricaoRepository(context, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System).AdicionarAsync(original);
            await context.SaveChangesAsync();
        }

        await using (SelecaoDbContext context = _fixture.CreateDbContext())
        {
            InscricaoRepository repositorio = new(context, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
            Inscricao lida = (await repositorio.ObterPorIdAsync(original.Id))!;
            Dictionary<string, JsonElement> mudanca = Respostas(("MODALIDADES", new[] { "LB_PPI" }));
            mudanca["NOME_SOCIAL"] = JsonSerializer.SerializeToElement<string?>(null);
            lida.Salvar(null, mudanca, Familia("membro-2", "membro-3"), [], Admitidas, PrecondicaoIfMatch.DeTags([lida.ETag]))
                .IsSuccess.Should().BeTrue();
            await repositorio.AtualizarAsync(lida);
            await context.SaveChangesAsync();
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Inscricao relida = (await new InscricaoRepository(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterPorIdAsync(original.Id))!;
        relida.Respostas.Should().NotContainKey("NOME_SOCIAL");
        relida.Respostas["MODALIDADES"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("LB_PPI");
        relida.Grupos["COMPOSICAO_FAMILIAR"].Select(static o => o.Id).Should().Equal("membro-2", "membro-3");
        relida.Grupos["COMPOSICAO_FAMILIAR"][1].Respostas["RG_NUMERO"].GetString().Should().Be("rg-membro-3");
    }

    [Fact(DisplayName = "A inscrição enviada e gravada recusa salvar rascunho e continua como foi enviada")]
    public async Task Salvar_DepoisDeSubmeter_RecusaEPreservaOEnviado()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        original.Submeter(
            "000654", OfertaDoProcesso, Respostas(("NOME_SOCIAL", "Kevin")), SemGrupos(), [], Admitidas,
            Respostas(("UF_RESIDENCIA", "PA")), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([original.ETag]), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        await using (SelecaoDbContext context = _fixture.CreateDbContext())
        {
            await new InscricaoRepository(context, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System).AdicionarAsync(original);
            await context.SaveChangesAsync();
        }

        await using (SelecaoDbContext context = _fixture.CreateDbContext())
        {
            InscricaoRepository repositorio = new(context, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
            Inscricao lida = (await repositorio.ObterPorIdAsync(original.Id))!;
            Result salvo = lida.Salvar(null, Respostas(("NOME_SOCIAL", "Kevin C.")), SemGrupos(), [], Admitidas, PrecondicaoIfMatch.DeTags([lida.ETag]));
            salvo.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
            await context.SaveChangesAsync();
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        Inscricao relida = (await new InscricaoRepository(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System)
            .ObterPorIdAsync(original.Id))!;
        relida.Derivados["UF_RESIDENCIA"].GetString().Should().Be("PA");
        relida.Respostas["NOME_SOCIAL"].GetString().Should().Be("Kevin");
    }

    [Fact(DisplayName = "O comprovante é lido do banco, na ordem de emissão: o 1 é a primeira submissão e o 2 a correção")]
    public async Task ObterComprovante_LeOsComprovantesGravados()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        DateTimeOffset primeira = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        string hashDaCorrecao;

        await using (SelecaoDbContext context = _fixture.CreateDbContext())
        {
            InscricaoRepository repositorio = new(context, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
            ComprovanteInscricao envio = original.Submeter(
                "000777", OfertaDoProcesso, Respostas(("NOME_SOCIAL", "Ariel")), SemGrupos(), [], Admitidas,
                Respostas(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([original.ETag]), primeira).Value!;
            ComprovanteInscricao correcao = original.Submeter(
                "000777", OfertaDoProcesso, Respostas(("NOME_SOCIAL", "Ariel D.")), SemGrupos(), [], Admitidas,
                Respostas(), DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([original.ETag]), primeira.AddMinutes(5)).Value!;
            hashDaCorrecao = correcao.HashIntegridade;
            await repositorio.AdicionarAsync(original);
            await repositorio.AdicionarComprovanteAsync(correcao);
            await repositorio.AdicionarComprovanteAsync(envio);
            await context.SaveChangesAsync();
        }

        IUserContext dono = Substitute.For<IUserContext>();
        dono.Cpf.Returns(PersonasDeTeste.FernandaDrumond.Valor);
        dono.UserId.Returns("sub-1");

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository leitura = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        Result<ComprovanteInscricaoDto> segundo = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(processo, 2), leitura, dono, CancellationToken.None);
        Result<ComprovanteInscricaoDto> terceiro = await ObterComprovanteQueryHandler.Handle(
            new ObterComprovanteQuery(processo, 3), leitura, dono, CancellationToken.None);

        segundo.IsSuccess.Should().BeTrue(segundo.Error?.Message);
        segundo.Value!.HashIntegridade.Should().Be(hashDaCorrecao, "o 2 é a correção, emitida depois, mesmo gravada antes");
        terceiro.Error!.Code.Should().Be("Inscricao.ComprovanteNaoEncontrado");
    }

    [Fact(DisplayName = "O hash recalculado do comprovante relido do banco confere, mesmo com número em notação exponencial")]
    public async Task ComprovanteGravado_TemIntegridadeReproduzivel()
    {
        Guid processo = Guid.CreateVersion7();
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, processo);
        Dictionary<string, JsonElement> derivados = new(StringComparer.Ordinal)
        {
            ["FAIXA_ETARIA"] = JsonDocument.Parse("1e3").RootElement.Clone(),
        };

        await using (SelecaoDbContext context = _fixture.CreateDbContext())
        {
            InscricaoRepository repositorio = new(context, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
            ComprovanteInscricao envio = original.Submeter(
                "000778", OfertaDoProcesso, Respostas(("NOME_SOCIAL", "Ariel")), SemGrupos(), [], Admitidas,
                derivados, DefinicoesDosDerivados, PrecondicaoIfMatch.DeTags([original.ETag]),
                new DateTimeOffset(2026, 10, 10, 12, 0, 0, 123, TimeSpan.Zero)).Value!;
            await repositorio.AdicionarAsync(original);
            await repositorio.AdicionarComprovanteAsync(envio);
            await context.SaveChangesAsync();
        }

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository leitura = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        IReadOnlyList<ComprovanteInscricao> gravados = await leitura.ObterComprovantesAsync([original.Id], CancellationToken.None);

        gravados.Should().ContainSingle().Which.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "A consulta de candidatos por fato e valor usa índice, sem operador JSON")]
    public async Task ConsultaPorFatoEValor_UsaIndice()
    {
        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SET enable_seqscan = off;
            EXPLAIN SELECT inscricao_id FROM selecao.respostas_da_inscricao
            WHERE fato_codigo = 'CONCORRER_RENDA' AND valor_booleano = true
            """;
        List<string> plano = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            plano.Add(reader.GetString(0));
        }

        string.Join('\n', plano).Should().Contain("ix_respostas_da_inscricao_fato_booleano");
    }

    [Fact(DisplayName = "ObterTodasPorCpf devolve as inscrições do CPF em todos os processos, nunca as de outro CPF (issue #1860)")]
    public async Task ObterTodasPorCpf_DevolveTodasDoCpfEmQualquerProcesso()
    {
        Guid processoA = Guid.CreateVersion7();
        Guid processoB = Guid.CreateVersion7();
        Inscricao naA = NovaInscricao(PersonasDeTeste.FernandaDrumond, processoA);
        Inscricao naB = NovaInscricao(PersonasDeTeste.FernandaDrumond, processoB);
        Inscricao deOutroCpf = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        InscricaoRepository writeRepository = new(writeContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await writeRepository.AdicionarAsync(naA);
        await writeRepository.AdicionarAsync(naB);
        await writeRepository.AdicionarAsync(deOutroCpf);
        await writeContext.SaveChangesAsync();

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        InscricaoRepository readRepository = new(readContext, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        IReadOnlyList<Inscricao> lidas = await readRepository.ObterTodasPorCpfAsync(PersonasDeTeste.FernandaDrumond);

        // O fixture é compartilhado entre os testes desta classe — outros já gravaram inscrições
        // da mesma persona; a prova é que as duas NOVAS aparecem, e nenhuma do outro CPF aparece,
        // não que a lista inteira do CPF seja só estas duas.
        IReadOnlySet<Guid> ids = lidas.Select(static i => i.Id).ToHashSet();
        ids.Should().Contain([naA.Id, naB.Id]);
        ids.Should().NotContain(deOutroCpf.Id);
    }

    [Fact(DisplayName = "Dois inícios simultâneos do mesmo CPF no mesmo processo colidem no índice único, não geram duas linhas")]
    public async Task IndiceUnico_RecusaDuasInscricoesDoMesmoCpfNoMesmoProcesso()
    {
        Guid processo = Guid.CreateVersion7();

        await using SelecaoDbContext primeiroContexto = _fixture.CreateDbContext();
        InscricaoRepository primeiroRepositorio = new(primeiroContexto, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await primeiroRepositorio.AdicionarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));
        await primeiroContexto.SaveChangesAsync();

        await using SelecaoDbContext segundoContexto = _fixture.CreateDbContext();
        InscricaoRepository segundoRepositorio = new(segundoContexto, _fixture.Encryption, _fixture.BlindIndex, TimeProvider.System);
        await segundoRepositorio.AdicionarAsync(NovaInscricao(PersonasDeTeste.FernandaDrumond, processo));

        Func<Task> segundaGravacao = async () => await segundoContexto.SaveChangesAsync();
        await segundaGravacao.Should().ThrowAsync<DbUpdateException>(
            "o índice único (processo_seletivo_id, cpf_indice) recusa a segunda linha — quem chama traduz isso em 'devolver a existente', não um 500");
    }
}
