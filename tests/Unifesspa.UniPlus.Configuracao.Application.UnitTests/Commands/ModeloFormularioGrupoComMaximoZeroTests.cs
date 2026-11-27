namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>O grupo com máximo zero é recusado uma vez só: a leitura e a conferência do domínio não repetem a recusa.</summary>
public sealed class ModeloFormularioGrupoComMaximoZeroTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "O grupo lido com máximo zero tem uma recusa só")]
    public async Task Criar_GrupoLidoComMaximoZero_RecusaUmaVez()
    {
        ConteudoDoModeloInput conteudo = Conteudo(Item("CERTIFICADO", 0)) with
        {
            Grupos = [EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").NaSecao("DADOS")
                .ComOcorrencias(0, 0).ComObrigatoriedade("SEMPRE").ComCampos(Campo("MAIOR_IDADE", 0)).Construir()],
        };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("conteudo.grupos[0].ocorrencias.maximo", GrupoFormularioErrorCodes.ContagemIncoerente));
    }
}
