namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

// DisableParallelization: a fixture escreve as connection strings em variáveis de ambiente do processo.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ColecaoInstantesEmResiduo : ICollectionFixture<InstantesEmResiduoFixture>
{
    public const string Name = "instantes-em-residuo";
}
