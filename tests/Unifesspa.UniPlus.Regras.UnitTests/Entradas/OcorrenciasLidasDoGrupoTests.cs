namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A leitura das ocorrências do grupo: o mínimo e a inclusão do candidato são declarados sempre.</summary>
public sealed class OcorrenciasLidasDoGrupoTests
{
    private static readonly (string FatoCodigo, bool TemExibicao, Obrigatoriedade Obrigatoriedade, IReadOnlyList<RestricaoValor> Restricoes) Parentesco =
        (CandidatoComoMembro.FatoParentesco, false, Obrigatoriedade.Sempre, []);

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
        lidas.ParaAConferenciaDaForma.Conferir().Should().BeEmpty();
        lidas.ConferirCandidatoComoMembro([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O mínimo ausente com a inclusão do candidato é recusado uma vez, sem a recusa de zero ocorrência")]
    public void Ler_MinimoAusenteComInclusao_NaoConfereOMinimoNoCandidato()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(null, true, 10));

        FieldError recusa = lidas.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("ocorrencias.minimo");
        recusa.Error.Code.Should().Be(GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
        lidas.ConferirCandidatoComoMembro([Parentesco]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O mínimo ausente com a inclusão do candidato ainda confere o parentesco")]
    public void ConferirCandidatoComoMembro_MinimoAusenteSemParentesco_RecusaSoOParentesco()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(null, true, 10));

        FieldError recusa = lidas.ConferirCandidatoComoMembro([]).Should().ContainSingle().Subject;
        recusa.Field.Should().Be("subitens");
        recusa.Error.Code.Should().Be(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto);
    }

    [Fact(DisplayName = "O mínimo ausente com o máximo zero recusa o mínimo, e a contagem ainda recusa o máximo")]
    public void Ler_MinimoAusenteComMaximoZero_AsDuasRecusasSaemPorFontesDistintas()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(null, false, 0));

        lidas.Recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(("ocorrencias.minimo", GrupoFormularioErrorCodes.OcorrenciasObrigatorias));
        lidas.ParaAConferenciaDaForma.Conferir().Select(static r => (r.Field, r.Error.Code)).Should().Equal(("maximo", GrupoFormularioErrorCodes.ContagemIncoerente));
    }

    [Fact(DisplayName = "A inclusão do candidato não declarada não confere o candidato como membro")]
    public void ConferirCandidatoComoMembro_InclusaoNaoDeclarada_Vazia()
    {
        OcorrenciasDoGrupoInput entrada = new(1, null, 10);

        OcorrenciasLidasDoGrupo.Ler(entrada).ConferirCandidatoComoMembro([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O mínimo zero declarado com a inclusão do candidato continua recusado")]
    public void ConferirCandidatoComoMembro_MinimoZeroDeclarado_Recusa()
    {
        OcorrenciasLidasDoGrupo lidas = OcorrenciasLidasDoGrupo.Ler(new OcorrenciasDoGrupoInput(0, true, 10));

        FieldError recusa = lidas.ConferirCandidatoComoMembro([Parentesco]).Should().ContainSingle().Subject;
        recusa.Field.Should().Be("ocorrencias.minimo");
        recusa.Error.Code.Should().Be(GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto);
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
