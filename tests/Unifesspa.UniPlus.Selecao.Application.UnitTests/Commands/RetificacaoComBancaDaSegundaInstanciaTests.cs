namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A retificação que congela versão nova passa pela mesma conferência da publicação, sem código à parte: a banca de 2ª instância
/// prevista onde nenhum recurso admite instância superior é recusada com o código e o campo da publicação.
/// </summary>
public sealed class RetificacaoComBancaDaSegundaInstanciaTests
{
    [Fact(DisplayName = "Retificar recusa a banca de 2ª instância prevista na etapa sem recurso que admita instância superior, e não congela versão nova")]
    public async Task Retificar_BancaDeSegundaInstanciaSemRecurso_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        EtapaProcesso etapa = cenario.Processo.Etapas.Single();
        etapa.DefinirBancas([BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), BancasDeTeste.Regra())]);

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.IsFailure.Should().BeTrue();
        FieldError recusa = resposta.Errors.Should().ContainSingle(static erro => erro.Error.Code == "Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar").Subject;
        recusa.Field.Should().Be("/etapas/0/bancas/0");
        eventos.Should().BeEmpty();
    }
}
