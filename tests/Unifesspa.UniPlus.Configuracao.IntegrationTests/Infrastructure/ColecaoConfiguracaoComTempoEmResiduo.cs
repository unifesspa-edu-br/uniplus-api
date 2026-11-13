namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

// DisableParallelization protege as variáveis de ambiente de connection string, process-wide, contra outras coleções.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ColecaoConfiguracaoComTempoEmResiduo : ICollectionFixture<ConfiguracaoComTempoEmResiduoFixture>
{
    public const string Name = "configuracao-com-tempo-em-residuo";
}
