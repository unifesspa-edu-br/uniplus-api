namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ImportacaoForaDoCronogramaNoEnvelopeTests
{
    private static DeclaracaoDaImportacaoDoResultado Declaracao(Guid? fase) =>
        DeclaracaoDaImportacaoDoResultado.Declarar([new("INEP_RETORNO_ENEM", "v1")], fase).Value!;

    [Fact]
    public void FaseDoCronograma_Aceita()
    {
        FaseCronograma fase = FormulariosDoDocumentoDeTeste.Fase(coletaInscricao: true);

        CoerenciaEntreBlocos.ImportacaoForaDoCronograma(Declaracao(fase.Id), [fase]).Should().BeNull();
    }

    [Fact]
    public void FaseQueOCronogramaNaoTraz_RecusaSemRepetirOIdentificador()
    {
        FaseCronograma fase = FormulariosDoDocumentoDeTeste.Fase(coletaInscricao: true);
        Guid ausente = Guid.CreateVersion7();

        DomainError? recusa = CoerenciaEntreBlocos.ImportacaoForaDoCronograma(Declaracao(ausente), [fase]);

        recusa!.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        recusa.Message.Should().Contain("importacaoDoResultado").And.NotContain(ausente.ToString());
    }

    [Fact]
    public void SemFaseDeclaradaOuSemDeclaracao_Aceita()
    {
        CoerenciaEntreBlocos.ImportacaoForaDoCronograma(Declaracao(null), []).Should().BeNull();
        CoerenciaEntreBlocos.ImportacaoForaDoCronograma(null, []).Should().BeNull();
    }
}
