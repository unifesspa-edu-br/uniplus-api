namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O registro das regras expõe a tabela esperada e a recusa de enquadramento da homologação, para que
/// a prova não dependa do próprio registro nem do catálogo publicado, e não expõe os códigos que
/// nenhuma resposta HTTP alcança.
/// </summary>
public sealed class RegrasDomainErrorRegistrationTests
{
    private const int QuantidadeDeMapeamentos = 96;
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";
    private const string CodigoDoErroNaoMapeado = "uniplus.erro_nao_mapeado";

    private static readonly JsonSerializerOptions OpcoesDeLeitura = new(JsonSerializerDefaults.Web);

    public static readonly TheoryData<string> ChavesQueNaoAlcancamAResposta = new()
    {
        "PredicadoDnf.DominioDinamicoNaoFornecido",
        RestricaoValorErrorCodes.FormaJsonInvalida,
        DerivacaoDoFatoErrorCodes.SemRegras,
        DerivacaoDoFatoErrorCodes.DependenciasIncoerentes,
        "DescritorFatoCandidato.DominioIncoerente",
    };

    [Fact(DisplayName = "O registro mapeia exatamente a tabela esperada, sem perder nem acrescentar código")]
    public void GetMappings_RegistroDasRegras_PreservaATabelaEsperada()
    {
        MapeamentoEsperado[] esperado =
        [
            .. LerTabelaEsperada(),
            new("PredicadoDnf.FormaJsonInvalida", 422, "uniplus.predicado_dnf.forma_json_invalida", "O predicado ou a obrigatoriedade não tem a forma esperada"),
        ];

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

    [Theory(DisplayName = "O registro não contém os códigos que nenhuma resposta HTTP alcança")]
    [MemberData(nameof(ChavesQueNaoAlcancamAResposta))]
    public void GetMappings_RegistroDasRegras_NaoContemAChaveQueNaoAlcancaAResposta(string chave)
    {
        MapeamentosDoRegistro().Select(static par => par.Key).Should().NotContain(chave);
    }

    [Theory(DisplayName = "O código que nenhuma resposta alcança, se chegasse ao tradutor, sai como erro de domínio não mapeado")]
    [MemberData(nameof(ChavesQueNaoAlcancamAResposta))]
    public void Resolve_ChaveQueNaoAlcancaAResposta_SaiComoErroNaoMapeado(string chave)
    {
        DomainErrorMappingRegistry mapeador = new(
            [new RegrasDomainErrorRegistration()],
            new ProblemTypeUriFactory(Options.Create(new ProblemTypeOptions { BaseUri = BaseUriDoCatalogo })));

        (int Status, string Type, string Title, string Code, bool RetryableConflict) resolvido =
            DomainErrorProblemDetailsFactory.Resolve(new DomainError(chave, "mensagem de teste"), mapeador);

        resolvido.Should().Be((400, BaseUriDoCatalogo + CodigoDoErroNaoMapeado, "Erro de domínio", CodigoDoErroNaoMapeado, false));
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
