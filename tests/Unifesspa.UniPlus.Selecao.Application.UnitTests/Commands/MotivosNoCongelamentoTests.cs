namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;

/// <summary>
/// Os motivos escolhidos nas operações que congelam a versão (UNI-REQ-0122): o motivo desativado no cadastro depois de escolhido
/// recusa a versão que o leva; o texto publicado é o da cópia feita na escolha, e a conferência não a refaz do cadastro.
/// </summary>
public sealed class MotivosNoCongelamentoTests
{
    [Fact(DisplayName = "A publicação recusa o motivo escolhido que foi desativado no cadastro, citando a fase e o código")]
    public async Task Publicar_MotivoDesativadoDepoisDeEscolhido_Recusa()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        (MotivoDeDecisao origem, MotivoDoProcesso escolhido) = EscolherNaPrimeiraFase(cenario);
        origem.Desativar();
        LerEscolha(cenario, new MotivoEscolhidoComOrigem(escolhido, origem));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        FieldError recusa = resposta.Errors.Should().ContainSingle().Subject;
        recusa.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
        recusa.Field.Should().Be("/motivos");
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    [Fact(DisplayName = "A publicação não refaz a cópia da descrição: o texto publicado é o da escolha, mesmo que o cadastro mude depois")]
    public async Task Publicar_DescricaoAlteradaNoCadastro_NaoMudaOTextoDaEscolha()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        (MotivoDeDecisao origem, MotivoDoProcesso escolhido) = EscolherNaPrimeiraFase(cenario);
        string descricaoEscolhida = escolhido.Descricao;
        origem.Atualizar("Redação nova, posterior à escolha.").IsSuccess.Should().BeTrue();
        LerEscolha(cenario, new MotivoEscolhidoComOrigem(escolhido, origem));

        (Result resposta, _) = await cenario.ExecutarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        escolhido.Descricao.Should().Be(descricaoEscolhida);
    }

    // A fase declara o tipo de decisão do motivo, que a conferência exige; a escolha é de um lugar que o processo tem.
    private static (MotivoDeDecisao Origem, MotivoDoProcesso Escolhido) EscolherNaPrimeiraFase(CenarioDeRecusaNoCongelamento cenario)
    {
        ProcessoSeletivo processo = cenario.Processo;
        typeof(FaseCronograma).GetField("_tiposDeDecisao", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(processo.CronogramaFases.First()).Should().BeAssignableTo<List<string>>().Subject.Add(TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa);
        MotivoDeDecisao origem = Motivo();

        return (origem, Escolhido(processo, LugarDaPrimeiraFase(processo), origem));
    }

    private static void LerEscolha(CenarioDeRecusaNoCongelamento cenario, params MotivoEscolhidoComOrigem[] escolha) =>
        cenario.Servicos.MotivosEscolhidos.ListarEscolhaAtualComOrigemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(escolha);
}
