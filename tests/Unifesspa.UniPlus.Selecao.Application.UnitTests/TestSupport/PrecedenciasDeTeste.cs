namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>O cadastro de precedência entre fases como o teste precisa vê-lo: sem relação nenhuma ou com as que o cenário declara.</summary>
internal static class PrecedenciasDeTeste
{
    public static IPrecedenciaFaseReader SemArestas() => Com();

    public static IPrecedenciaFaseReader Com(params (string Antecessora, string Sucessora, bool PermiteSobreposicao)[] arestas)
    {
        IPrecedenciaFaseReader leitor = Substitute.For<IPrecedenciaFaseReader>();
        IReadOnlyList<PrecedenciaFaseView> vivas =
            [.. arestas.Select(static a => new PrecedenciaFaseView(Guid.CreateVersion7(), a.Antecessora, a.Sucessora, a.PermiteSobreposicao))];
        leitor.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns(vivas);

        return leitor;
    }
}
