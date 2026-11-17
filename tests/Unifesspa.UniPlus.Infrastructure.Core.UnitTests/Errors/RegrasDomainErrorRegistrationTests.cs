namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// O registro das regras expõe exatamente a tabela esperada, mantida em arquivo à parte para que
/// a prova não dependa do próprio registro nem do catálogo publicado.
/// </summary>
public sealed class RegrasDomainErrorRegistrationTests
{
    private const int QuantidadeDeMapeamentos = 99;

    private static readonly JsonSerializerOptions OpcoesDeLeitura = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "O registro mapeia exatamente a tabela esperada, sem perder nem acrescentar código")]
    public void GetMappings_RegistroDasRegras_PreservaATabelaEsperada()
    {
        MapeamentoEsperado[] esperado = LerTabelaEsperada();

        MapeamentoEsperado[] obtido = [.. MapeamentosDoRegistro().Select(static par =>
            new MapeamentoEsperado(par.Key, par.Value.Status, par.Value.Code, par.Value.Title))];

        obtido.Should().HaveCount(QuantidadeDeMapeamentos);
        obtido.Should().BeEquivalentTo(esperado);
    }

    [Fact(DisplayName = "Nenhum mapeamento das regras declara conflito retentável nem recusa após gravação")]
    public void GetMappings_RegistroDasRegras_UsaSoOsPadroesDeRetentativaEGravacao()
    {
        MapeamentosDoRegistro().Should().OnlyContain(static par => !par.Value.RetryableConflict && !par.Value.RecusaAposGravacao);
    }

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> MapeamentosDoRegistro() =>
        new RegrasDomainErrorRegistration().GetMappings();

    private static MapeamentoEsperado[] LerTabelaEsperada()
    {
        string caminho = Path.Join(AppContext.BaseDirectory, "Errors", "RegrasMapeamentosEsperados.json");
        return JsonSerializer.Deserialize<MapeamentoEsperado[]>(File.ReadAllText(caminho), OpcoesDeLeitura)!;
    }

    private sealed record MapeamentoEsperado(string Chave, int Status, string Codigo, string Titulo);
}
