namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O nome social é dado pessoal: a restrição de classificação do catálogo de fatos recusa a linha dele como pública.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class NomeSocialPessoalNoCatalogoTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public NomeSocialPessoalNoCatalogoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A restrição do catálogo recusa o nome social público")]
    public async Task NomeSocialPublico_ERecusadoPelaRestricao()
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand tornarPublico = new(
            "UPDATE configuracao.rol_de_fatos_candidato SET classificacao_protecao = 'PUBLICO' WHERE codigo = @codigo",
            conexao);
        tornarPublico.Parameters.AddWithValue("codigo", FatoCandidato.CodigoDoNomeSocial);

        Func<Task> executar = async () => await tornarPublico.ExecuteNonQueryAsync();

        (await executar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
    }
}
