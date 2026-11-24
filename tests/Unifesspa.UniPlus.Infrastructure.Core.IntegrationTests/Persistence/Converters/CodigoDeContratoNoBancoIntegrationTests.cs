namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

/// <summary>
/// O enum com código de contrato contra PostgreSQL real: o que o banco guarda, lido por consulta direta no banco, é o código declarado,
/// e não o nome nem o número do membro; a coluna opcional vazia é <c>NULL</c> e a coleção primitiva é <c>text[]</c> de
/// códigos. O código que o enum não declara, escrito direto no banco, faz a leitura falhar.
/// </summary>
public sealed class CodigoDeContratoNoBancoIntegrationTests(BancoDeTurmasFixture banco) : IClassFixture<BancoDeTurmasFixture>
{
    private const string Tabela = ContextoDeTurmas.Tabela;
    private const string ModalidadeGravada = $"SELECT modalidade FROM {Tabela} WHERE id = @id";
    private const string AlternativaGravada = $"SELECT alternativa FROM {Tabela} WHERE id = @id";
    private const string AlternativaNulaDaLinha = $"SELECT count(*) FROM {Tabela} WHERE id = @id AND alternativa IS NULL";
    private const string PreferidasGravadas = $"SELECT preferidas FROM {Tabela} WHERE id = @id";
    private const string TiposDasColunas = $"SELECT column_name, udt_name FROM information_schema.columns WHERE table_name = '{Tabela}'";

    private const string ComModalidadeInvalida =
        $"INSERT INTO {Tabela} (id, modalidade, alternativa, preferidas) VALUES (@id, 'inexistente', NULL, ARRAY['EM_SALA']::text[])";

    private const string ComAlternativaInvalida =
        $"INSERT INTO {Tabela} (id, modalidade, alternativa, preferidas) VALUES (@id, 'EM_SALA', 'inexistente', ARRAY['EM_SALA']::text[])";

    private const string ComPreferidaInvalida =
        $"INSERT INTO {Tabela} (id, modalidade, alternativa, preferidas) VALUES (@id, 'EM_SALA', NULL, ARRAY['EM_SALA', 'inexistente']::text[])";

    [Theory(DisplayName = "O escalar é gravado como o código declarado, e não como o nome nem o número do membro")]
    [InlineData(FormatoDeAula.Presencial, "EM_SALA")]
    [InlineData(FormatoDeAula.Remoto, "A_DISTANCIA")]
    public async Task Escalar_GravadoComoOCodigoDeContrato(FormatoDeAula formato, string codigoEsperado)
    {
        Guid id = await GravarAsync(new Turma { Modalidade = formato });

        string? gravado = await LerColunaAsync<string>(ModalidadeGravada, id);

        gravado.Should().Be(codigoEsperado).And.NotBe(formato.ToString())
            .And.NotBe(((int)formato).ToString(CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "O escalar opcional preenchido é gravado como o código declarado")]
    public async Task EscalarOpcionalPreenchido_GravadoComoOCodigoDeContrato()
    {
        Guid id = await GravarAsync(new Turma { Modalidade = FormatoDeAula.Presencial, Alternativa = FormatoDeAula.Remoto });

        (await LerColunaAsync<string>(AlternativaGravada, id)).Should().Be("A_DISTANCIA");
    }

    [Fact(DisplayName = "O escalar opcional vazio é gravado como `NULL` e volta ausente na leitura da entidade")]
    public async Task EscalarOpcionalVazio_GravadoComoNuloEVoltaAusente()
    {
        Guid id = await GravarAsync(new Turma { Modalidade = FormatoDeAula.Presencial, Alternativa = null });

        (await LerColunaAsync<long>(AlternativaNulaDaLinha, id)).Should().Be(1, "a linha existe e a coluna guarda NULL, e não um texto");
        (await LerTurmaAsync(id)).Alternativa.Should().BeNull();
    }

    [Fact(DisplayName = "A coleção primitiva é gravada como o array de códigos declarados, na ordem")]
    public async Task ColecaoPrimitiva_GravadaComoArrayDeCodigos()
    {
        Guid id = await GravarAsync(new Turma
        {
            Modalidade = FormatoDeAula.Presencial,
            Preferidas = [FormatoDeAula.Remoto, FormatoDeAula.Presencial],
        });

        (await LerColunaAsync<string[]>(PreferidasGravadas, id)).Should().Equal("A_DISTANCIA", "EM_SALA");
    }

    [Fact(DisplayName = "As colunas do enum com código são texto, e a da coleção é `text[]`")]
    public async Task Colunas_TemTipoDeTexto()
    {
        Dictionary<string, string> tipos = await TiposDasColunasAsync();

        tipos["modalidade"].Should().Be("text");
        tipos["alternativa"].Should().Be("text");
        tipos["preferidas"].Should().Be("_text");
    }

    [Fact(DisplayName = "Gravar e ler a entidade devolve os valores do enum nos três lugares")]
    public async Task IdaEVolta_DevolveOsValoresDoEnum()
    {
        Guid id = await GravarAsync(new Turma
        {
            Modalidade = FormatoDeAula.Remoto,
            Alternativa = FormatoDeAula.Presencial,
            Preferidas = [FormatoDeAula.Presencial, FormatoDeAula.Remoto],
        });

        Turma lida = await LerTurmaAsync(id);

        lida.Modalidade.Should().Be(FormatoDeAula.Remoto);
        lida.Alternativa.Should().Be(FormatoDeAula.Presencial);
        lida.Preferidas.Should().Equal(FormatoDeAula.Presencial, FormatoDeAula.Remoto);
    }

    [Fact(DisplayName = "Código inválido no escalar, escrito direto no banco, faz a leitura da entidade falhar")]
    public Task LeituraComCodigoInvalidoNoEscalar_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComModalidadeInvalida);

    [Fact(DisplayName = "Código inválido no escalar opcional, escrito direto no banco, faz a leitura da entidade falhar")]
    public Task LeituraComCodigoInvalidoNoEscalarOpcional_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComAlternativaInvalida);

    [Fact(DisplayName = "Código inválido no elemento da coleção, escrito direto no banco, faz a leitura da entidade falhar")]
    public Task LeituraComCodigoInvalidoNoElementoDaColecao_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComPreferidaInvalida);

    private async Task ConferirQueALeituraFalhaPeloCodigoAsync(string insercao)
    {
        Guid id = await InserirPorSqlAsync(insercao);

        Func<Task> leitura = () => LerTurmaAsync(id);

        Exception excecao = (await leitura.Should().ThrowAsync<Exception>()).Which;
        CadeiaDe(excecao).OfType<ArgumentException>().Should().Contain(
            erro => erro.Message.Contains(nameof(FormatoDeAula), StringComparison.Ordinal),
            "o EF pode embrulhar a exceção do conversor, que acusa o código e o enum, sem valor substituto");
    }

    private static IEnumerable<Exception> CadeiaDe(Exception excecao)
    {
        for (Exception? atual = excecao; atual is not null; atual = atual.InnerException)
        {
            yield return atual;
        }
    }

    private async Task<Guid> GravarAsync(Turma turma)
    {
        turma.Id = Guid.CreateVersion7();
        await using ContextoDeTurmas contexto = banco.CriarContexto();
        contexto.Turmas.Add(turma);
        await contexto.SaveChangesAsync();

        return turma.Id;
    }

    private async Task<Turma> LerTurmaAsync(Guid id)
    {
        await using ContextoDeTurmas contexto = banco.CriarContexto();

        return await contexto.Turmas.AsNoTracking().SingleAsync(turma => turma.Id == id);
    }

    private async Task<Guid> InserirPorSqlAsync(string insercao)
    {
        Guid id = Guid.CreateVersion7();
        await using NpgsqlConnection conexao = await banco.AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(insercao, conexao);
        comando.Parameters.Add(new NpgsqlParameter<Guid>("id", id));
        await comando.ExecuteNonQueryAsync();

        return id;
    }

    private async Task<TColuna?> LerColunaAsync<TColuna>(string consulta, Guid id)
    {
        await using NpgsqlConnection conexao = await banco.AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(consulta, conexao);
        comando.Parameters.Add(new NpgsqlParameter<Guid>("id", id));

        return (TColuna?)await comando.ExecuteScalarAsync();
    }

    private async Task<Dictionary<string, string>> TiposDasColunasAsync()
    {
        await using NpgsqlConnection conexao = await banco.AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(TiposDasColunas, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Dictionary<string, string> tipos = [];

        while (await leitor.ReadAsync())
        {
            tipos[leitor.GetString(0)] = leitor.GetString(1);
        }

        return tipos;
    }
}
