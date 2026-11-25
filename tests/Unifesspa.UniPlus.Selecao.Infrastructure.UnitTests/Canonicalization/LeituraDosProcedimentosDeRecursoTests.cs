namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

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

    [Fact]
    public void Ler_ComAmbosOsAlvos_RestauraOsProcedimentos()
    {
        ProcedimentosDeRecurso original = ColunaDosProcedimentosDeRecursoTests.Procedimentos();
        JsonObject pai = new() { ["procedimentos"] = SerializacaoDosProcedimentosDeRecurso.Serializar(original) };
        LeitorEnvelope leitor = new();

        ProcedimentosDeRecurso? lidos = LeituraDosProcedimentosDeRecurso.Ler(leitor, pai, "fase", CodigosDoProcedimentoDeRecurso.DaFase);

        leitor.Falhou.Should().BeFalse();
        lidos!.MesmoConteudo(original).Should().BeTrue();
    }

    [Fact]
    public void AlvoComTipoDesconhecido_RecusaEnvelopeMalformado()
    {
        JsonObject pai = PaiComUmProcedimentoDoTipo(TipoMotivoDeDecisao.Classificacao.Codigo());
        pai["procedimentos"]![0]!["alvo"] = new JsonObject { ["tipo"] = "desconhecido", ["valor"] = "x" };
        LeitorEnvelope leitor = new();

        LeituraDosProcedimentosDeRecurso.Ler(leitor, pai, "fase", CodigosDoProcedimentoDeRecurso.DaFase);

        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Ler_ProcedimentosRepetidos_PropagaTodasAsIncoerencias(int quantidade)
    {
        JsonObject pai = PaiComUmProcedimentoDoTipo(TipoMotivoDeDecisao.Classificacao.Codigo());
        JsonArray procedimentos = (JsonArray)pai["procedimentos"]!;
        for (int indice = 1; indice < quantidade; indice++)
        {
            procedimentos.Add(procedimentos[0]!.DeepClone());
        }
        LeitorEnvelope leitor = new();
        LeituraDosProcedimentosDeRecurso.Ler(leitor, pai, "fase", CodigosDoProcedimentoDeRecurso.DaFase);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(quantidade - 1);
        ConferirDuplicados(new JsonObject { ["fase"] = pai }, causas);
    }

    private static void ConferirDuplicados(JsonObject documento, IReadOnlyList<FieldError> causas)
    {
        for (int indice = 0; indice < causas.Count; indice++)
        {
            string caminho = $"fase.procedimentos[{indice + 1}]";
            causas[indice].Should().Be(new FieldError(caminho, new DomainError(
                CodigosDoProcedimentoDeRecurso.DaFase.Duplicado,
                $"A matéria do tipo '{TipoMotivoDeDecisao.Classificacao.Codigo()}' já tem procedimento declarado; cada matéria tem um único procedimento.")));
            CaminhoNoDocumento.Existe(documento, caminho).Should().BeTrue();
        }
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
                    ["codigo"] = RegraDeProcedimentoDoRecurso.Recalculo.Codigo(),
                    ["versao"] = "v1",
                    ["hash"] = new string('a', 64),
                },
            }),
        };
}
