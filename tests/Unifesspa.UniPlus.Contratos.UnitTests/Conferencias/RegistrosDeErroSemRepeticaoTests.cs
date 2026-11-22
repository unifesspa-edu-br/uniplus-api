namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Dentro de um registro de erros, cada código de domínio aparece uma vez só: a repetição seria um mapeamento que
/// a outra entrada sobrescreve, e passaria despercebida num registro grande. O código de fio pode se repetir de
/// propósito, quando vários erros de domínio respondem com o mesmo código público.
/// </summary>
public sealed class RegistrosDeErroSemRepeticaoTests
{
    [Fact(DisplayName = "Nenhum registro de erros repete código de domínio")]
    public void NenhumRegistroRepeteCodigo()
    {
        IReadOnlyList<IDomainErrorRegistration> registros = RegistrosDeErroDaSolucao.Instanciar();

        string[] repeticoes =
        [
            .. registros.SelectMany(static r => RepeticoesDoRegistro(r)),
        ];

        registros.Should().NotBeEmpty("sem registro descoberto, a conferência passaria vazia");
        repeticoes.Should().BeEmpty("cada código de domínio de um registro tem uma entrada só");
    }

    private static IEnumerable<string> RepeticoesDoRegistro(IDomainErrorRegistration registro) =>
        registro.GetMappings()
            .GroupBy(static m => m.Key, StringComparer.OrdinalIgnoreCase)
            .Where(static g => g.Count() > 1)
            .Select(g => $"{registro.GetType().Name}: código de domínio repetido: {g.Key}");
}
