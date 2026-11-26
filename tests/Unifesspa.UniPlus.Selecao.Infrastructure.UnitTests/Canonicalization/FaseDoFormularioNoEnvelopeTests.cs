namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>
/// O formulário do documento é conferido contra as fases que o próprio documento congelou: o bloco das modalidades
/// calculadas só é admitido no formulário que serve à fase de inscrição.
/// </summary>
public sealed class FaseDoFormularioNoEnvelopeTests
{
    [Fact(DisplayName = "O formulário numa fase que coleta a inscrição admite o bloco das modalidades calculadas")]
    public void LerFormularios_BlocoDeModalidadesEmFaseQueColetaInscricao_Aceita()
    {
        FaseCronograma fase = FormulariosDoDocumentoDeTeste.Fase(coletaInscricao: true);

        (LeitorEnvelope leitor, IReadOnlyList<FormularioProcesso> formularios) = Ler(fase, [fase]);

        leitor.Falhou.Should().BeFalse();
        formularios.Should().ContainSingle().Which.FaseId.Should().Be(fase.Id);
    }

    [Fact(DisplayName = "O formulário numa fase que não coleta a inscrição recusa o bloco das modalidades calculadas")]
    public void LerFormularios_BlocoDeModalidadesEmFaseQueNaoColetaInscricao_Recusa()
    {
        FaseCronograma fase = FormulariosDoDocumentoDeTeste.Fase(coletaInscricao: false);

        (LeitorEnvelope leitor, IReadOnlyList<FormularioProcesso> formularios) = Ler(fase, [fase]);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Be("O bloco não é admitido pelo formulário.");
        formularios.Should().BeEmpty();
    }

    [Fact(DisplayName = "O formulário sem fase admite o bloco das modalidades calculadas")]
    public void LerFormularios_BlocoDeModalidadesSemFase_Aceita()
    {
        FaseCronograma fase = FormulariosDoDocumentoDeTeste.Fase(coletaInscricao: false);

        (LeitorEnvelope leitor, IReadOnlyList<FormularioProcesso> formularios) = Ler(null, [fase]);

        leitor.Falhou.Should().BeFalse();
        formularios.Should().ContainSingle().Which.FaseId.Should().BeNull();
    }

    private static (LeitorEnvelope Leitor, IReadOnlyList<FormularioProcesso> Formularios) Ler(FaseCronograma? faseDoFormulario, IEnumerable<FaseCronograma> fases)
    {
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["faseId"] = faseDoFormulario is null ? null : faseDoFormulario.Id.ToString();
        formulario["paginas"]!.AsArray().Insert(0, FormularioComTermosNoEnvelope.Pagina("MODALIDADES", 0, "BLOCO", "MODALIDADES_CALCULADAS"));
        formulario["paginas"]![1]!["ordem"] = 1;
        LeitorEnvelope leitor = new();

        (IReadOnlyList<FormularioProcesso> formularios, _) =
            LeituraDosFormularios.LerFormularios(leitor, FormularioComTermosNoEnvelope.Documento(formulario), CronogramaDoProcesso.Do(fases));

        return (leitor, formularios);
    }
}
