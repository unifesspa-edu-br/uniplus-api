namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// A leitura dos procedimentos de recurso do envelope recusa o tipo de decisão fora do vocabulário sem repetir o texto
/// escrito no envelope, e diz quais tipos são aceitos.
/// </summary>
public sealed class LeituraDosProcedimentosDeRecursoTests
{
    private const string TipoEscrito = "TIPO-ESCRITO-FICTICIO";

    [Fact(DisplayName = "O tipo de decisão fora do vocabulário é recusado como envelope malformado, lista os tipos aceitos e não repete o texto escrito")]
    public void TipoDeDecisaoForaDoVocabulario_RecusaListandoOsAceitosSemRepetirOTexto()
    {
        LeitorEnvelope leitor = new();

        ProcedimentosDeRecurso? lidos = LeituraDosProcedimentosDeRecurso.Ler(
            leitor, PaiComUmProcedimentoDoTipo(TipoEscrito), "fase", CodigosDoProcedimentoDeRecurso.DaFase);

        lidos.Should().BeNull();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should()
            .Contain("fase.procedimentos[0].tipoDeDecisao")
            .And.Contain(TipoDoMotivoInformado.RotulosAceitosEmTexto)
            .And.NotContain(TipoEscrito);
    }

    private static JsonObject PaiComUmProcedimentoDoTipo(string tipoDeDecisao) =>
        new()
        {
            ["procedimentos"] = new JsonArray(new JsonObject
            {
                ["tipoDeDecisao"] = tipoDeDecisao,
                ["alvo"] = null,
                ["regra"] = new JsonObject
                {
                    ["codigo"] = RegraProcedimentoRecursoCodigo.Recalculo,
                    ["versao"] = "v1",
                    ["hash"] = new string('a', 64),
                },
            }),
        };
}
