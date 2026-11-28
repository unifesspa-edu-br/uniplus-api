namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os dados do pedido de criação do processo: nome e origem dos candidatos. Cada causa sai no campo do
/// pedido, todas juntas e na ordem do pedido, sem repetir o valor recebido.
/// </summary>
public sealed class CamposDoNovoProcessoTests
{
    private static CriarProcessoSeletivoCommand Valido() => new CriacaoDoProcessoSeletivoBuilder(Guid.NewGuid()).Construir();

    private static (string? Campo, string Codigo)[] Pares(CriarProcessoSeletivoCommand comando) =>
        [.. CamposDoNovoProcesso.Conferir(comando).Select(static recusa => (recusa.Field, recusa.Error.Code))];

    [Fact(DisplayName = "O pedido com nome e origem válidos não tem recusa")]
    public void PedidoValido_SemRecusa() => CamposDoNovoProcesso.Conferir(Valido()).Should().BeEmpty();

    [Theory(DisplayName = "O nome nulo, vazio ou só de espaços é obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NomeAusente_Obrigatorio(string? nome) =>
        Pares(Valido() with { Nome = nome! }).Should().Equal(("nome", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "O nome acima do limite recusa pelo tamanho, e o do limite é aceito")]
    public void NomeAcimaDoLimite_Tamanho()
    {
        Pares(Valido() with { Nome = new string('a', DocumentoDoRascunho.NomeMaxLength + 1) }).Should().Equal(("nome", CodigosDeValidacao.Tamanho));
        CamposDoNovoProcesso.Conferir(Valido() with { Nome = new string('a', DocumentoDoRascunho.NomeMaxLength) }).Should().BeEmpty();
    }

    [Theory(DisplayName = "A origem dos candidatos ausente é obrigatória e a desconhecida é inválida")]
    [InlineData(null, CodigosDeValidacao.Obrigatorio)]
    [InlineData("", CodigosDeValidacao.Obrigatorio)]
    [InlineData("   ", CodigosDeValidacao.Obrigatorio)]
    [InlineData("origemQueNaoExiste", CodigosDeValidacao.Regra)]
    public void Origem_AusenteOuDesconhecida(string? origem, string codigo) =>
        Pares(Valido() with { OrigemCandidatos = origem }).Should().Equal(("origemCandidatos", codigo));

    [Fact(DisplayName = "Todas as causas saem juntas, na ordem do pedido, e a mensagem não repete o valor recebido")]
    public void TodasAsCausas_SaemJuntas()
    {
        CriarProcessoSeletivoCommand comando = Valido() with { Nome = string.Empty, OrigemCandidatos = "valorEnviado" };

        IReadOnlyList<FieldError> recusas = CamposDoNovoProcesso.Conferir(comando);

        recusas.Select(static recusa => recusa.Field).Should().Equal("nome", "origemCandidatos");
        recusas.Should().OnlyContain(static recusa => !recusa.Error.Message.Contains("valorEnviado", StringComparison.Ordinal));
    }
}
