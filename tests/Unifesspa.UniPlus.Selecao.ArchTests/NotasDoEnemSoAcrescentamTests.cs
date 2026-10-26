namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Nenhum caminho altera ou apaga um lote, uma geração ou um lançamento de nota (ADR-0149): as
/// portas só acrescentam e consultam. Corrigir uma nota é importar outro lote.
/// </summary>
public sealed class NotasDoEnemSoAcrescentamTests
{
    private static readonly string[] Prefixos = ["Atualizar", "Remover", "Excluir", "Apagar", "Alterar", "Editar"];

    [Theory(DisplayName = "as portas das notas do ENEM não expõem operação que altere ou apague")]
    [InlineData(typeof(ILoteDeNotasDoEnemRepository))]
    [InlineData(typeof(IGeracaoDeEnvioAoInepRepository))]
    [InlineData(typeof(ILancamentosDeNotaReader))]
    public void Portas_NaoExpoemMutacao(Type porta)
    {
        ArgumentNullException.ThrowIfNull(porta);

        string[] proibidos = [.. porta.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(static m => m.Name)
            .Where(static nome => Prefixos.Any(p => nome.StartsWith(p, StringComparison.Ordinal)))];

        proibidos.Should().BeEmpty($"{porta.Name} é evidência append-only: só acrescenta e consulta");
    }
}
