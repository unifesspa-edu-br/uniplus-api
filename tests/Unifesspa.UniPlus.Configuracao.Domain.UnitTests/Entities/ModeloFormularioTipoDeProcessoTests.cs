namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O tipo de processo é guardado como recebido em criação e atualização.
/// </summary>
public sealed class ModeloFormularioTipoDeProcessoTests
{
    private const string Codigo = "POS";

    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    private static ConteudoDoModelo Conteudo() => new(
        "Habilitação",
        [
            new PaginaDoModelo("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null),
            new PaginaDoModelo("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
        ],
        [new ItemDoModelo("QUILOMBOLA", 0, "DADOS", new ApresentacaoDoCampo("Quilombola", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))],
        [],
        [],
        []);

    [Fact(DisplayName = "Criar com código válido do tipo guarda o código recebido")]
    public void Criar_TipoValido_GuardaCodigoRecebido()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO", "Habilitação", null, FinalidadeFormulario.Habilitacao, Codigo, Conteudo(), SemDerivacoes);

        modelo.Value!.TipoProcessoCodigo.Should().Be(Codigo);
    }

    [Fact(DisplayName = "Atualizar com código válido do tipo guarda o código recebido")]
    public void Atualizar_TipoValido_GuardaCodigoRecebido()
    {
        ModeloFormulario modelo = ModeloFormulario.Criar(
            "HABILITACAO", "Habilitação", null, FinalidadeFormulario.Habilitacao, null, Conteudo(), SemDerivacoes).Value!;

        Result atualizado = modelo.Atualizar("Habilitação", null, Codigo, Conteudo(), SemDerivacoes);

        atualizado.IsSuccess.Should().BeTrue(atualizado.Error?.Message);
        modelo.TipoProcessoCodigo.Should().Be(Codigo);
    }
}
