namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Desempate;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>As recusas do desempate saem por assunto, e dentro de cada assunto na posição do critério na lista.</summary>
public sealed class OrdemDasRecusasDoDesempateTests
{
    [Fact(DisplayName = "A ordem repetida do critério 1 vem antes da etapa inexistente do critério 0: o assunto vem antes da posição")]
    public void ConferirCriteriosDesempate_OrdemRepetidaEEtapaInexistente_SaiPorAssunto()
    {
        ConfiguracaoDeclarada configuracao = ProcessoConformeFactory.Criar().Declaracao();
        Guid etapaInexistente = Guid.CreateVersion7();
        CriterioDesempateInformado[] criterios =
        [
            new(2, new ParametrosDesempateMaiorNotaEtapa(etapaInexistente)),
            new(2, new ParametrosDesempateMaiorIdade()),
        ];

        List<FieldError> recusas = RegrasDoDesempate.ConferirCriteriosDesempate(configuracao, criterios);

        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("criterios[1].ordem", "ProcessoSeletivo.OrdemDesempateDuplicada"),
            ("criterios[0].etapaRef", "ProcessoSeletivo.EtapaRefDesempateInexistente"));
        recusas[1].Error.Message.Should().Be($"O critério de desempate na ordem 2 referencia a etapa {etapaInexistente}, que não existe neste processo.");
    }
}
