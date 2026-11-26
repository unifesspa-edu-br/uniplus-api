namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ConsequenciaDaFaltaDeEnvioNoEnvelopeTests
{
    private const string CodigoDoFormularioDeTeste = "HABILITACAO";

    [Fact]
    public void Formulario_ConsequenciaDeclarada_GravaCodigoERotuloELeOCodigo()
    {
        JsonObject documento = DocumentoComConsequencia(ConsequenciaDoDescumprimento.RetiraAModalidade);
        JsonObject formulario = documento["formularios"]![0]!.AsObject();
        formulario["consequenciaDaFaltaDeEnvio"]!.GetValue<string>().Should().Be("RETIRA_A_MODALIDADE");
        formulario["contexto"]!["consequenciaDaFaltaDeEnvioRotulo"]!.GetValue<string>().Should().Be("Retirada da modalidade");
        formulario["contexto"]!["consequenciaDaFaltaDeEnvioRotulo"] = "Rótulo alterado";
        LeitorEnvelope leitor = new();

        (IReadOnlyList<FormularioProcesso> lidos, _) = LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeFalse();
        lidos.Single(static f => f.Codigo.Valor == CodigoDoFormularioDeTeste).ConsequenciaDaFaltaDeEnvio.Should().Be(ConsequenciaDoDescumprimento.RetiraAModalidade);
    }

    [Fact]
    public void Formulario_SemConsequencia_GravaNuloELeAusencia()
    {
        JsonObject documento = DocumentoComConsequencia(null);
        JsonObject formulario = documento["formularios"]![0]!.AsObject();
        formulario["consequenciaDaFaltaDeEnvio"].Should().BeNull();
        formulario["contexto"]?["consequenciaDaFaltaDeEnvioRotulo"].Should().BeNull();
        LeitorEnvelope leitor = new();

        (IReadOnlyList<FormularioProcesso> lidos, _) = LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeFalse();
        lidos.Single(static f => f.Codigo.Valor == CodigoDoFormularioDeTeste).ConsequenciaDaFaltaDeEnvio.Should().BeNull();
    }

    [Fact]
    public void Formulario_CodigoForaDoVocabulario_RecusaALeitura()
    {
        JsonObject documento = DocumentoComConsequencia(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        documento["formularios"]![0]!["consequenciaDaFaltaDeEnvio"] = "ABRE_PRAZO_PARA_REENVIO";
        LeitorEnvelope leitor = new();

        LeituraDosFormularios.LerFormularios(leitor, documento, FormulariosDoDocumentoDeTeste.SemFases);

        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void Resumo_MesmoCodigoComRotuloDiferente_MudaOResumo()
    {
        JsonObject documento = DocumentoComConsequencia(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        JsonObject outro = documento.DeepClone().AsObject();
        outro["formularios"]![0]!["contexto"]!["consequenciaDaFaltaDeEnvioRotulo"] = "Outro texto";
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;

        perfil.HashHex(perfil.Serializar(outro)).Should().NotBe(perfil.HashHex(perfil.Serializar(documento)));
    }

    [Fact]
    public void ComparacaoDaRetificacao_SemOContexto_IgnoraORotuloDaConsequencia()
    {
        JsonObject documento = DocumentoComConsequencia(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        JsonObject outro = documento.DeepClone().AsObject();
        outro["formularios"]![0]!["contexto"]!["consequenciaDaFaltaDeEnvioRotulo"] = "Outro texto";

        JsonNode.DeepEquals(ContextoDosRotulos.SemContexto(documento), ContextoDosRotulos.SemContexto(outro)).Should().BeTrue();
    }

    private static PaginaDoFormulario[] Paginas() =>
    [
        PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
        PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão", null, null).Value!,
    ];

    private static JsonObject DocumentoComConsequencia(ConsequenciaDoDescumprimento? consequencia)
    {
        ProcessoSeletivo processo = ExemploDeConfiguracao.Entrada().Processo;
        processo.DefinirFormulario(new FormularioDeclarado(CodigoDoFormulario.Criar(CodigoDoFormularioDeTeste).Value!, null, true), null, Paginas(), null, consequencia)
            .IsSuccess.Should().BeTrue();
        JsonObject documento = new() { ["formularios"] = SerializacaoDosFormularios.SerializarFormularios(processo) };
        JsonObject copia = JsonNode.Parse(PerfilCanonicoV1.Instancia.Serializar(documento))!.AsObject();
        JsonArray formularios = copia["formularios"]!.AsArray();
        JsonNode habilitacao = formularios.Single(static f => f!["codigo"]!.GetValue<string>() == CodigoDoFormularioDeTeste)!;
        formularios.Clear();
        formularios.Add(habilitacao);
        return copia;
    }
}
