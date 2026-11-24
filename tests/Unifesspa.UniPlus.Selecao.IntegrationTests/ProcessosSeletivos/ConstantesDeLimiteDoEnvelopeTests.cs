namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.TestesCompartilhados;

using Xunit;

public sealed class ConstantesDeLimiteDoEnvelopeTests
{
    public static TheoryData<string, string?> LimitesSemColuna
    {
        get
        {
            TheoryData<string, string?> dados = [];
            foreach (EntradaDeConfrontoDoLimite entrada in QuadroDeConfrontoDosLimites.Entradas.Where(entrada => entrada.Classe == ClasseDeConfrontoDoLimite.SemColuna))
            {
                dados.Add(entrada.Nome, entrada.Motivo);
            }
            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(LimitesSemColuna))]
    public void LimiteSemColuna_TemMotivo(string nome, string? motivo) => motivo.Should().NotBeNullOrWhiteSpace(nome);
}
