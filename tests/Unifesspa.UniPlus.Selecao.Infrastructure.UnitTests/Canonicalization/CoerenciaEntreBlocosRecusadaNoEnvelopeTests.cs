namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;

public sealed class CoerenciaEntreBlocosRecusadaNoEnvelopeTests
{
    [Theory]
    [InlineData(false, "INSCRICAO", "finalidade que não tem formulário")]
    [InlineData(true, "EXEMPLO_FATO_FORA_DA_SECAO", "itens não correspondem à estrutura")]
    public void Conferir_ItemForaDoFormulario_NaoRepeteValor(bool temFormulario, string valor, string trecho)
    {
        JsonObject documento = Documento(temFormulario, valor);
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FatoColetado> fatos, Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) = LeituraDosFatos.LerFatosColetados(leitor, documento);
        (IReadOnlyList<FormularioProcesso> formularios, _) = LeituraDosFormularios.LerFormularios(leitor, documento);
        leitor.Falhou.Should().BeFalse();
        documento.ToJsonString().Should().Contain(valor);
        Result<EnvelopeReidratado> resultado = CoerenciaEntreBlocos.Conferir(Blocos(fatos, valores, formularios), null!, documento);
        FieldError causa = resultado.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain(trecho).And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }

    [Fact]
    public void Conferir_CitacaoInvalida_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_CITACAO_AUSENTE";
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        JsonArray exibicao = CitacaoDeDado(valor);
        formulario["paginas"]!.AsArray().Insert(0, FormularioComTermosNoEnvelope.Pagina("PAGINA", 0, "SECAO", null, exibicao));
        formulario["paginas"]![1]!["ordem"] = 1;
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(formulario);
        documento["dadosDosFormularios"] = new JsonArray();
        ConferirRecusa(documento, valor, "uma citação não corresponde aos dados conhecidos pelo formulário");
    }

    [Fact]
    public void Conferir_EdicaoDoEnemSemClassificacaoEnem_NaoRepeteValor()
    {
        const string valor = "ROTULO_FICTICIO_EDICAO";
        JsonObject formulario = FormularioComTermosNoEnvelope.Formulario("INSCRICAO");
        formulario["paginas"]!.AsArray().Insert(0, FormularioComTermosNoEnvelope.Pagina("PAGINA", 0, "SECAO", null));
        formulario["paginas"]![1]!["ordem"] = 1;
        JsonObject fato = ConferenciaDoVocabularioRecusadaNoEnvelopeTests.Fato("EDICAO_ENEM", 0);
        fato["rotulo"] = valor;
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(formulario);
        documento["dadosDosFormularios"] = new JsonArray(fato);
        ConferirRecusa(documento, valor, "a edição do ENEM exige classificação pelo ENEM");
    }

    private static void ConferirRecusa(JsonObject documento, string valor, string mensagem)
    {
        documento.ToJsonString().Should().Contain(valor);
        LeitorEnvelope leitor = new();
        (IReadOnlyList<FatoColetado> fatos, Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) = LeituraDosFatos.LerFatosColetados(leitor, documento);
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) = LeituraDosFormularios.LerFormularios(leitor, documento);
        leitor.Falhou.Should().BeFalse();
        PrepararTestemunhos(documento, fatos, formularios, termos);
        BlocosDoEnvelope blocos = Blocos(fatos, valores, formularios) with { Configuracao = Configuracao() };
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(Guid.NewGuid(), Congelado(blocos.Configuracao.HashDocumento), Guid.NewGuid(), "usuario-ficticio", DateTimeOffset.UnixEpoch);
        Result<EnvelopeReidratado> resultado = CoerenciaEntreBlocos.Conferir(blocos, versao, documento);
        ConferirErroSemExporValor(resultado, valor, mensagem);
    }

    private static JsonArray CitacaoDeDado(string codigoDoDado) => new(new JsonArray(new JsonObject
    {
        ["codigoDoDado"] = codigoDoDado,
        ["operador"] = "IGUAL",
        ["valor"] = true,
    }));

    private static void ConferirErroSemExporValor(Result<EnvelopeReidratado> resultado, string valor, string mensagem)
    {
        FieldError causa = resultado.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain(mensagem).And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }

    private static DocumentoCongelado Congelado(string hashDocumento)
    {
        SnapshotCanonico snapshot = new SnapshotPublicacaoCanonicalizer().Canonicalizar(ExemploDeConfiguracao.Entrada());
        return new(snapshot.Bytes, snapshot.SchemaVersion, snapshot.AlgoritmoHash, hashDocumento);
    }

    private static void PrepararTestemunhos(JsonObject documento, IReadOnlyList<FatoColetado> fatos,
        IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos)
    {
        Result<GrafoDependenciaConjunta> grafo = GrafoDependenciaConjunta.Construir(fatos, [], [], formularios, termos);
        grafo.IsSuccess.Should().BeTrue();
        documento["dependenciasEntreItens"] = SerializacaoDosFatos.SerializarGrafoDependencia(grafo.Value!);
        documento["modalidadesOfertadas"] = SerializacaoDosFatos.SerializarModalidadesOfertadas([]);
    }

    private static BlocosDaConfiguracao Configuracao() => new()
    {
        Dados = null,
        HashDocumento = new string('a', 64),
        MotivosDeDecisao = [],
        Etapas = [],
        Distribuicao = [],
        Atendimento = null,
        Bonus = null,
        Cascata = null,
        Desempate = [],
        Classificacao = null,
        CronogramaFases = [],
    };

    private static JsonObject Documento(bool temFormulario, string valor)
    {
        JsonObject fato = ConferenciaDoVocabularioRecusadaNoEnvelopeTests.Fato(temFormulario ? valor : "EXEMPLO_FATO", 0);
        JsonObject documento = FormularioComTermosNoEnvelope.Documento(temFormulario ? [FormularioComTermosNoEnvelope.Formulario("INSCRICAO")] : []);
        documento["dadosDosFormularios"] = new JsonArray(fato);
        return documento;
    }

    private static BlocosDoEnvelope Blocos(IReadOnlyList<FatoColetado> fatos,
        Dictionary<string, IReadOnlyList<Unifesspa.UniPlus.Selecao.Application.Abstractions.ValorDominioDeclaradoCongelado>?> valores,
        IReadOnlyList<FormularioProcesso> formularios) => new(null!, Documentos(formularios), null!, null, [], Coleta(fatos, valores));

    private static BlocosDosDocumentos Documentos(IReadOnlyList<FormularioProcesso> formularios) => new()
    {
        Conformidade = null,
        DocumentosExigidos = [],
        ReferenciaTemporalFatos = null,
        MetadadosFatosCongelados = null,
        Formularios = formularios,
        TermosExigidos = [],
    };

    private static BlocosDeColeta Coleta(IReadOnlyList<FatoColetado> fatos,
        Dictionary<string, IReadOnlyList<Unifesspa.UniPlus.Selecao.Application.Abstractions.ValorDominioDeclaradoCongelado>?> valores) => new()
        {
            FatosColetados = fatos,
            ValoresSelecionaveis = valores,
            GruposColetados = [],
            AgregadosDosGrupos = [],
            RegrasDerivacao = [],
            VersaoInterpretador = CalculoDosFatosDerivados.VersaoSemantica,
            ApresentacaoDosPressupostos = new Dictionary<string, Unifesspa.UniPlus.Regras.Formularios.ApresentacaoDoFato>(),
            TiposDeAtoCongelados = new Dictionary<string, Unifesspa.UniPlus.Selecao.Application.Abstractions.TipoDeAtoCongelado>(),
            FatosDerivadosCongelados = new Dictionary<string, Unifesspa.UniPlus.Selecao.Application.Abstractions.FatoDerivadoCongelado>(),
        };
}
