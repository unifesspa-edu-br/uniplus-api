namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O tipo de processo do modelo é guardado na forma em que o cadastro grava o código — aparado e em
/// NFC —, por qualquer caminho que o grave, porque o processo o compara de forma ordinal.
/// </summary>
public sealed class ModeloFormularioTipoDeProcessoTests
{
    private const string Composto = "PÓS";
    private const string Decomposto = "PÓS";

    private static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    private static ConteudoDoModelo Conteudo() => new(
        "Habilitação",
        [
            new EtapaDoModelo("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null),
            new EtapaDoModelo("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
        ],
        [new ItemDoModelo("QUILOMBOLA", 0, "DADOS", "Quilombola", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false)],
        [],
        [],
        []);

    [Fact(DisplayName = "Criar com o código do tipo decomposto guarda o código em NFC")]
    public void Criar_TipoDecomposto_GuardaNormalizado()
    {
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            "HABILITACAO", "Habilitação", null, FinalidadeFormulario.Habilitacao, $" {Decomposto} ", Conteudo(), SemDerivacoes);

        modelo.Value!.TipoProcessoCodigo.Should().Be(Composto);
    }

    [Fact(DisplayName = "Atualizar com o código do tipo decomposto guarda o código em NFC")]
    public void Atualizar_TipoDecomposto_GuardaNormalizado()
    {
        ModeloFormulario modelo = ModeloFormulario.Criar(
            "HABILITACAO", "Habilitação", null, FinalidadeFormulario.Habilitacao, null, Conteudo(), SemDerivacoes).Value!;

        Result atualizado = modelo.Atualizar("Habilitação", null, Decomposto, Conteudo(), SemDerivacoes);

        atualizado.IsSuccess.Should().BeTrue(atualizado.Error?.Message);
        modelo.TipoProcessoCodigo.Should().Be(Composto);
    }
}
