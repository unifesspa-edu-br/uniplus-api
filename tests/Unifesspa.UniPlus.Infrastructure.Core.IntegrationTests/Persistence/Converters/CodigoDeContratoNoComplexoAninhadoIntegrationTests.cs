namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

/// <summary>
/// O enum com código de contrato dentro de um tipo complexo aninhado, contra PostgreSQL real. O EF nomeia a coluna pelo caminho
/// da propriedade (<c>Material_Rodape_Formato</c>) e guarda a coleção primitiva do complexo como <c>text[]</c>; o texto gravado
/// é o código declarado, e o código que o enum não declara, escrito direto no banco, faz a leitura falhar.
/// </summary>
public sealed class CodigoDeContratoNoComplexoAninhadoIntegrationTests(BancoDeTurmasFixture banco) : IClassFixture<BancoDeTurmasFixture>
{
    private const string Tabela = ContextoDeTurmas.TabelaDeOfertas;
    private const string FormatoDoExterno = "Material_Formato";
    private const string FormatoDoInterno = "Material_Rodape_Formato";
    private const string AlternativaDoInterno = "Material_Rodape_Alternativa";
    private const string PreferidasDoInterno = "Material_Rodape_Preferidas";

    private const string ComFormatoDoExternoInvalido =
        $"INSERT INTO {Tabela} (id, \"{FormatoDoExterno}\", \"{FormatoDoInterno}\", \"{PreferidasDoInterno}\") VALUES (@id, 'inexistente', 'em_sala', ARRAY[]::text[])";

    private const string ComFormatoDoInternoInvalido =
        $"INSERT INTO {Tabela} (id, \"{FormatoDoExterno}\", \"{FormatoDoInterno}\", \"{PreferidasDoInterno}\") VALUES (@id, 'em_sala', 'inexistente', ARRAY[]::text[])";

    private const string ComAlternativaInvalida =
        $"INSERT INTO {Tabela} (id, \"{FormatoDoExterno}\", \"{FormatoDoInterno}\", \"{AlternativaDoInterno}\", \"{PreferidasDoInterno}\") VALUES (@id, 'em_sala', 'em_sala', 'inexistente', ARRAY[]::text[])";

    private const string ComPreferidaInvalida =
        $"INSERT INTO {Tabela} (id, \"{FormatoDoExterno}\", \"{FormatoDoInterno}\", \"{PreferidasDoInterno}\") VALUES (@id, 'em_sala', 'em_sala', ARRAY['em_sala', 'inexistente']::text[])";

    [Theory(DisplayName = "O enum do complexo e do complexo aninhado é gravado como o código declarado, e não como o nome nem o número")]
    [InlineData(FormatoDeAula.Presencial, "em_sala")]
    [InlineData(FormatoDeAula.Remoto, "a_distancia")]
    public async Task EscalarNosDoisNiveis_GravadoComoOCodigoDeContrato(FormatoDeAula formato, string codigoEsperado)
    {
        Guid id = await GravarAsync(OfertaCom(formato, formato));

        foreach (string coluna in new[] { FormatoDoExterno, FormatoDoInterno })
        {
            string? gravado = await LerColunaAsync<string>(coluna, id);

            gravado.Should().Be(codigoEsperado).And.NotBe(formato.ToString())
                .And.NotBe(((int)formato).ToString(CultureInfo.InvariantCulture));
        }
    }

    [Fact(DisplayName = "O escalar opcional do complexo aninhado preenchido é gravado como o código declarado")]
    public async Task EscalarOpcionalPreenchido_GravadoComoOCodigoDeContrato()
    {
        Oferta oferta = OfertaCom(FormatoDeAula.Presencial, FormatoDeAula.Presencial);
        oferta.Material.Rodape.Alternativa = FormatoDeAula.Remoto;
        Guid id = await GravarAsync(oferta);

        (await LerColunaAsync<string>(AlternativaDoInterno, id)).Should().Be("a_distancia");
    }

    [Fact(DisplayName = "O escalar opcional vazio do complexo aninhado é `NULL` e volta ausente na leitura da entidade")]
    public async Task EscalarOpcionalVazio_GravadoComoNuloEVoltaAusente()
    {
        Guid id = await GravarAsync(OfertaCom(FormatoDeAula.Presencial, FormatoDeAula.Presencial));

        (await LerColunaAsync<string>(AlternativaDoInterno, id)).Should().BeNull("a coluna guarda NULL, e não um texto");
        (await LerOfertaAsync(id)).Material.Rodape.Alternativa.Should().BeNull();
    }

    [Fact(DisplayName = "A coleção primitiva do complexo aninhado é gravada como o array de códigos declarados, na ordem")]
    public async Task ColecaoPrimitiva_GravadaComoArrayDeCodigos()
    {
        Oferta oferta = OfertaCom(FormatoDeAula.Presencial, FormatoDeAula.Presencial);
        oferta.Material.Rodape.Preferidas = [FormatoDeAula.Remoto, FormatoDeAula.Presencial];
        Guid id = await GravarAsync(oferta);

        (await LerColunaAsync<string[]>(PreferidasDoInterno, id)).Should().Equal("a_distancia", "em_sala");
    }

    [Fact(DisplayName = "As colunas do complexo aninhado têm o nome do caminho, são texto, e a da coleção é `text[]`")]
    public async Task Colunas_TemNomeDoCaminhoETipoDeTexto()
    {
        Dictionary<string, string> tipos = await TiposDasColunasAsync();

        tipos[FormatoDoExterno].Should().Be("text");
        tipos[FormatoDoInterno].Should().Be("text");
        tipos[AlternativaDoInterno].Should().Be("text");
        tipos[PreferidasDoInterno].Should().Be("_text");
    }

    [Fact(DisplayName = "Gravar e ler a entidade devolve os valores do enum nos dois níveis do complexo")]
    public async Task IdaEVolta_DevolveOsValoresDoEnum()
    {
        Oferta oferta = OfertaCom(FormatoDeAula.Remoto, FormatoDeAula.Presencial);
        oferta.Material.Rodape.Alternativa = FormatoDeAula.Remoto;
        oferta.Material.Rodape.Preferidas = [FormatoDeAula.Presencial, FormatoDeAula.Remoto];
        Guid id = await GravarAsync(oferta);

        Oferta lida = await LerOfertaAsync(id);

        lida.Material.Formato.Should().Be(FormatoDeAula.Remoto);
        lida.Material.Rodape.Formato.Should().Be(FormatoDeAula.Presencial);
        lida.Material.Rodape.Alternativa.Should().Be(FormatoDeAula.Remoto);
        lida.Material.Rodape.Preferidas.Should().Equal(FormatoDeAula.Presencial, FormatoDeAula.Remoto);
    }

    [Fact(DisplayName = "Código inválido no enum do complexo externo, escrito direto no banco, faz a leitura falhar")]
    public Task LeituraComCodigoInvalidoNoComplexoExterno_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComFormatoDoExternoInvalido);

    [Fact(DisplayName = "Código inválido no enum do complexo aninhado, escrito direto no banco, faz a leitura falhar")]
    public Task LeituraComCodigoInvalidoNoComplexoAninhado_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComFormatoDoInternoInvalido);

    [Fact(DisplayName = "Código inválido no enum opcional do complexo aninhado, escrito direto no banco, faz a leitura falhar")]
    public Task LeituraComCodigoInvalidoNoOpcionalDoComplexo_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComAlternativaInvalida);

    [Fact(DisplayName = "Código inválido no elemento da coleção do complexo aninhado, escrito direto no banco, faz a leitura falhar")]
    public Task LeituraComCodigoInvalidoNoElementoDaColecao_Falha() => ConferirQueALeituraFalhaPeloCodigoAsync(ComPreferidaInvalida);

    private static Oferta OfertaCom(FormatoDeAula externo, FormatoDeAula interno) =>
        new() { Material = new MaterialDaOferta { Formato = externo, Rodape = new RodapeDoMaterial { Formato = interno } } };

    private static IEnumerable<Exception> CadeiaDe(Exception excecao)
    {
        for (Exception? atual = excecao; atual is not null; atual = atual.InnerException)
        {
            yield return atual;
        }
    }

    private async Task ConferirQueALeituraFalhaPeloCodigoAsync(string insercao)
    {
        Guid id = await InserirPorSqlAsync(insercao);

        Func<Task> leitura = () => LerOfertaAsync(id);

        Exception excecao = (await leitura.Should().ThrowAsync<Exception>()).Which;
        ArgumentException acusacao = CadeiaDe(excecao).OfType<ArgumentException>().Should().ContainSingle(
            erro => erro.Message.Contains(nameof(FormatoDeAula), StringComparison.Ordinal),
            "o EF pode embrulhar a exceção do conversor, que acusa o enum, sem valor substituto").Which;
        acusacao.Message.Should().NotContain("inexistente", "a mensagem cita o enum, e não o código recebido");
    }

    private async Task<Guid> GravarAsync(Oferta oferta)
    {
        oferta.Id = Guid.CreateVersion7();
        await using ContextoDeTurmas contexto = banco.CriarContexto();
        contexto.Ofertas.Add(oferta);
        await contexto.SaveChangesAsync();

        return oferta.Id;
    }

    private async Task<Oferta> LerOfertaAsync(Guid id)
    {
        await using ContextoDeTurmas contexto = banco.CriarContexto();

        return await contexto.Ofertas.AsNoTracking().SingleAsync(oferta => oferta.Id == id);
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

    private async Task<TColuna?> LerColunaAsync<TColuna>(string coluna, Guid id)
    {
        await using NpgsqlConnection conexao = await banco.AbrirConexaoAsync();
        await using NpgsqlCommand comando = new($"SELECT \"{coluna}\" FROM {Tabela} WHERE id = @id", conexao);
        comando.Parameters.Add(new NpgsqlParameter<Guid>("id", id));
        object? valor = await comando.ExecuteScalarAsync();

        return valor is DBNull ? default : (TColuna?)valor;
    }

    private async Task<Dictionary<string, string>> TiposDasColunasAsync()
    {
        await using NpgsqlConnection conexao = await banco.AbrirConexaoAsync();
        await using NpgsqlCommand comando = new(
            $"SELECT column_name, udt_name FROM information_schema.columns WHERE table_name = '{Tabela}'", conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Dictionary<string, string> tipos = [];

        while (await leitor.ReadAsync())
        {
            tipos[leitor.GetString(0)] = leitor.GetString(1);
        }

        return tipos;
    }
}
