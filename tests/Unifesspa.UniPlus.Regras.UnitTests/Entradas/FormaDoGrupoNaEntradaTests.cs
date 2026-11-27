namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using CampoDoMembro = (string FatoCodigo, bool TemExibicao, Unifesspa.UniPlus.Regras.Formularios.Obrigatoriedade Obrigatoriedade, System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Formularios.RestricaoValor> Restricoes);

/// <summary>A forma do grupo lida da entrada: a forma do grupo e, quando inclui o candidato, o parentesco, mesmo com partes não lidas.</summary>
public sealed class FormaDoGrupoNaEntradaTests
{
    private static readonly CampoDoMembro Parentesco = (CandidatoComoMembro.FatoParentesco, false, Obrigatoriedade.Sempre, []);

    [Fact(DisplayName = "O grupo conforme não tem recusa")]
    public void Conferir_GrupoConforme_SemRecusa()
    {
        GrupoColetadoInput entrada = Grupo(new OcorrenciasDoGrupoInput(1, true, 5), Campo(CandidatoComoMembro.FatoParentesco));

        FormaDoGrupoNaEntrada.Conferir(entrada, OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias), null, Obrigatoriedade.Sempre, [Parentesco])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "O máximo zero sem mínimo declarado recusa o máximo, e o mínimo fica com a leitura")]
    public void Conferir_MinimoAusenteEMaximoZero_RecusaSoOMaximo()
    {
        GrupoColetadoInput entrada = Grupo(new OcorrenciasDoGrupoInput(null, false, 0), Campo("MAIOR_IDADE"));

        List<FieldError> recusas =
            FormaDoGrupoNaEntrada.Conferir(entrada, OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias), null, Obrigatoriedade.Nunca, [null]);

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(("ocorrencias.maximo", GrupoFormularioErrorCodes.ContagemIncoerente));
    }

    [Fact(DisplayName = "O candidato incluído sem parentesco recusa o parentesco ausente")]
    public void Conferir_CandidatoIncluidoSemParentesco_RecusaOParentescoAusente()
    {
        GrupoColetadoInput entrada = Grupo(new OcorrenciasDoGrupoInput(1, true, 5), Campo("MAIOR_IDADE"));

        List<FieldError> recusas = FormaDoGrupoNaEntrada.Conferir(
            entrada, OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias), null, Obrigatoriedade.Sempre, [("MAIOR_IDADE", false, Obrigatoriedade.Sempre, [])]);

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(("subitens", GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto));
    }

    [Fact(DisplayName = "O campo que não se leu conta como conforme e não vira parentesco ausente")]
    public void Conferir_ParentescoNaoLido_NaoRecusaOParentescoAusente()
    {
        GrupoColetadoInput entrada = Grupo(new OcorrenciasDoGrupoInput(1, true, 5), Campo(CandidatoComoMembro.FatoParentesco));

        FormaDoGrupoNaEntrada.Conferir(entrada, OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias), null, Obrigatoriedade.Sempre, [null])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A obrigatoriedade que não se leu vale nunca, e a exibição que cita o grupo é recusada")]
    public void Conferir_ObrigatoriedadeNaoLidaEExibicaoAutorreferente_RecusaAAutorreferencia()
    {
        GrupoColetadoInput entrada = Grupo(new OcorrenciasDoGrupoInput(0, false, 5), Campo("MAIOR_IDADE"));
        PredicadoDnf exibicao = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("COMPOSICAO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

        List<FieldError> recusas =
            FormaDoGrupoNaEntrada.Conferir(entrada, OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias), exibicao, null, [null]);

        recusas.Select(static r => r.Error.Code).Should().Equal(GrupoFormularioErrorCodes.RegraAutorreferente);
    }

    [Fact(DisplayName = "O grupo sem campos na entrada é conferido sem falhar")]
    public void Conferir_SubitensAusentes_ConfereOGrupoSemFalhar()
    {
        GrupoColetadoInput entrada = Grupo(new OcorrenciasDoGrupoInput(0, false, 5)) with { Subitens = null! };

        FormaDoGrupoNaEntrada.Conferir(entrada, OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias), null, Obrigatoriedade.Nunca, [])
            .Select(static r => r.Error.Code).Should().Equal(GrupoFormularioErrorCodes.SubitensForaDoLimite);
    }

    private static GrupoColetadoInput Grupo(OcorrenciasDoGrupoInput ocorrencias, params FatoColetadoInput[] campos) =>
        EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO").ComRotulo("Composição").ComCampos(campos).Construir() with { Ocorrencias = ocorrencias };

    private static FatoColetadoInput Campo(string fato) => EntradaDoFatoColetadoDeTeste.Coletando(fato).ComRotulo(fato).DoTipo("SIM_OU_NAO").Construir();
}
