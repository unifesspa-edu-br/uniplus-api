using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

namespace Unifesspa.UniPlus.Contratos.UnitTests;

/// <summary>Confere os mapeamentos publicados dos códigos de erro do domínio.</summary>
public sealed class MapeamentoDeDomainErrorTests
{
    [Theory(DisplayName = "Guard rail da cadeia de versões: corrida responde 409, recusa de corpo responde 422")]
    [InlineData("VersaoConfiguracao.NumeroDuplicado", 409)]
    [InlineData("VersaoConfiguracao.CadeiaQuebrada", 422)]
    [InlineData("VersaoConfiguracao.VigenciaRegressiva", 422)]
    [InlineData("VersaoConfiguracao.AtoCriadorJaCriouVersao", 422)]
    [InlineData("VersaoConfiguracao.NumeracaoComBuraco", 422)]
    public void GuardRailDaCadeiaDeVersoes_TemOStatusDaSuaEspecie(string code, int statusEsperado)
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = LerMapeamentosRegistrados(HostsDoProdutoIniciados.UniPlus);

        mapeamentos.Should().ContainKey(code);
        mapeamentos[code].Status.Should().Be(statusEsperado);
    }

    /// <summary>
    /// O <c>code</c> permanece específico em todas as sete, inclusive na que passou a 409.
    /// O status responde "dá para tentar de novo?"; o code responde "por quê?". Colapsá-las no
    /// código genérico de conflito apagaria a segunda resposta sem melhorar a primeira, e as
    /// tornaria indistinguíveis do conflito de concorrência otimista, que é outro mecanismo e
    /// tem outro tratamento.
    /// </summary>
    [Fact(DisplayName = "As recusas da cadeia de versões mantêm code próprio, sem colapsar no conflito genérico")]
    public void GuardRailDaCadeiaDeVersoes_NaoColapsaNoCodeGenericoDeConflito()
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = LerMapeamentosRegistrados(HostsDoProdutoIniciados.UniPlus);

        IEnumerable<KeyValuePair<string, DomainErrorMapping>> daCadeia = mapeamentos
            .Where(m => m.Key.StartsWith("VersaoConfiguracao.", StringComparison.Ordinal));

        daCadeia.Should().NotBeEmpty();
        daCadeia.Should().OnlyContain(
            m => m.Value.Code != "uniplus.concorrencia.conflito",
            "o code identifica a causa e alimenta o catálogo público de erros; o conflito "
                + "genérico é do middleware de concorrência otimista, e confundir os dois "
                + "esconde qual invariante da cadeia de versões foi violada");
    }

    /// <summary>
    /// A marca de conflito retentável só faz sentido sobre um conflito. O envelope a publica
    /// para qualquer status, mas é um sinal ao cliente: numa recusa de conteúdo ela
    /// prometeria uma repetição que não muda o desfecho.
    /// </summary>
    /// <remarks>
    /// Uma declaração fora de lugar é o primeiro sintoma de que o critério foi aplicado sem o
    /// cenário de corrida na mão.
    /// </remarks>
    [Theory(DisplayName = "só um conflito pode declarar-se retentável")]
    [InlineData(HostsDoProdutoIniciados.UniPlus)]
    [InlineData(HostsDoProdutoIniciados.Portal)]
    public void RetryableConflict_SoEhDeclaradoEmStatusDeConflito(string modulo)
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = LerMapeamentosRegistrados(modulo);

        IEnumerable<KeyValuePair<string, DomainErrorMapping>> retentaveis = mapeamentos
            .Where(m => m.Value.RetryableConflict);

        retentaveis.Should().OnlyContain(
            m => m.Value.Status == 409,
            "o campo `retryable` do envelope responde \"repetir a mesma requisição pode agora dar "
                + "certo?\", e só o 409 nomeia a corrida que o torna verdadeiro — num 422 ou num 404 "
                + "ele promete ao cliente uma saída que a repetição não oferece");
    }

    private static Dictionary<string, DomainErrorMapping> LerMapeamentosRegistrados(string executavel) =>
        HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos.GetServices<IDomainErrorRegistration>()
            .SelectMany(registro => registro.GetMappings())
            .GroupBy(mapeamento => mapeamento.Key, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Last().Value, StringComparer.Ordinal);
}
