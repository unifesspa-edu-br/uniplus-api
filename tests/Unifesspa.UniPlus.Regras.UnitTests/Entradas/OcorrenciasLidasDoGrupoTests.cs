namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A leitura das ocorrências do grupo: o mínimo e a inclusão do candidato são declarados sempre.</summary>
public sealed class OcorrenciasLidasDoGrupoTests
{
    [Fact(DisplayName = "As ocorrências ausentes recusam o mínimo e a inclusão do candidato, cada um no próprio caminho")]
    public void Ler_OcorrenciasAusentes_RecusaOsDoisCampos()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(null);

        lidas.Recusas.Select(static r => r.Field).Should().Equal("ocorrencias.minimo", "ocorrencias.incluiCandidato");
        lidas.Recusas.Select(static r => r.Error.Code).Should().AllBe(GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
        lidas.Declaradas.Should().BeNull();
    }

    [Fact(DisplayName = "O mínimo ausente é recusado sozinho")]
    public void Ler_MinimoAusente_RecusaSoOMinimo()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(null, false, 3));

        FieldError recusa = lidas.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("ocorrencias.minimo");
        recusa.Error.Message.Should().Be("O mínimo de ocorrências do grupo é obrigatório.");
        lidas.Declaradas.Should().BeNull();
    }

    [Fact(DisplayName = "A inclusão do candidato ausente é recusada sozinha")]
    public void Ler_InclusaoAusente_RecusaSoAInclusao()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(1, null, 3));

        FieldError recusa = lidas.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("ocorrencias.incluiCandidato");
        recusa.Error.Message.Should().Be("É obrigatório declarar se o candidato é uma das ocorrências do grupo.");
        lidas.Declaradas.Should().BeNull();
    }

    [Fact(DisplayName = "O mínimo negativo com a inclusão ausente é recusado junto com ela, e a forma ainda é conferida")]
    public void Ler_MinimoNegativoComInclusaoAusente_AsDuasRecusasSaemNoMesmoLote()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(-1, null));

        lidas.Recusas.Select(static r => r.Field).Should().Equal("ocorrencias.incluiCandidato");
        lidas.ParaAConferenciaDaForma.Conferir().Select(static r => r.Error.Code).Should().Equal(GrupoFormularioErrorCodes.ContagemIncoerente);
    }

    [Fact(DisplayName = "O máximo ausente é o grupo sem limite")]
    public void Ler_MaximoAusente_SemLimite()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(0, false));

        lidas.Recusas.Should().BeEmpty();
        lidas.Declaradas.Should().Be(new OcorrenciasDoGrupo(0, null, IncluiCandidato: false));
    }

    [Fact(DisplayName = "As ocorrências completas chegam como declaradas")]
    public void Ler_Completas_Declaradas()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(1, true, 5));

        lidas.Recusas.Should().BeEmpty();
        lidas.Declaradas.Should().Be(new OcorrenciasDoGrupo(1, 5, IncluiCandidato: true));
    }
}
