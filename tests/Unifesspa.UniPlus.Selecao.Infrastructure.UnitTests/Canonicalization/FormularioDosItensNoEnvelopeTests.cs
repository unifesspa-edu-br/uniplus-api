namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cada campo, grupo e termo do documento congelado cita o código do formulário que o exige, e a leitura o liga de volta
/// ao formulário lido com esse código.
/// </summary>
public sealed class FormularioDosItensNoEnvelopeTests
{
    [Fact(DisplayName = "O campo serializado cita o código do formulário que o exige")]
    public void SerializarFatosColetados_Campo_CitaOCodigoDoFormulario()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        FatoColetado campo = ItensDoFormularioDeTeste.Campo("FATO", 0, formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaHabilitacao)!);

        JsonArray serializado = SerializacaoDosFatos.SerializarFatosColetados([campo], formularios.CodigosPorIdentificador(), null);

        serializado.Should().ContainSingle().Which!["formularioCodigo"]!.GetValue<string>().Should().Be("HABILITACAO");
    }

    [Fact(DisplayName = "O grupo serializado e os campos dele citam o código do formulário do grupo")]
    public void SerializarGruposColetados_Grupo_CitaOCodigoDoFormularioNoGrupoENosCampos()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        GrupoColetado grupo = ItensDoFormularioDeTeste.Grupo("GRUPO", 0, formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaInscricao)!);

        JsonArray serializado = SerializacaoDosFatos.SerializarGruposColetados([grupo], formularios.CodigosPorIdentificador(), null, []);

        JsonNode item = serializado.Should().ContainSingle().Subject!;
        item["formularioCodigo"]!.GetValue<string>().Should().Be("INSCRICAO");
        item["subitens"]!.AsArray().Should().ContainSingle().Which!["formularioCodigo"]!.GetValue<string>().Should().Be("INSCRICAO");
    }

    [Fact(DisplayName = "O campo que nenhum formulário do processo exige não é serializado")]
    public void SerializarFatosColetados_CampoSemFormulario_Falha()
    {
        FatoColetado semFormulario = FatoColetadoDeTeste.Coletando("FATO", 0).Construir();
        IReadOnlyDictionary<Guid, string> codigos = FormulariosDoDocumentoDeTeste.ComFormularios().CodigosPorIdentificador();

        Action serializar = () => SerializacaoDosFatos.SerializarFatosColetados([semFormulario], codigos, null);

        serializar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "O campo lido fica ligado ao formulário lido com o código citado")]
    public void LerFatosColetados_CodigoDoFormulario_LigaOCampoAoFormulario()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        FormularioProcesso habilitacao = formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaHabilitacao)!;
        JsonObject documento = Documento("dadosDosFormularios", SerializacaoDosFatos.SerializarFatosColetados(
            [ItensDoFormularioDeTeste.Campo("FATO", 0, habilitacao)], formularios.CodigosPorIdentificador(), null));
        LeitorEnvelope leitor = new();

        (IReadOnlyList<FatoColetado> lidos, _) = LeituraDosFatos.LerFatosColetados(leitor, documento, formularios);

        leitor.Falhou.Should().BeFalse();
        lidos.Should().ContainSingle().Which.FormularioId.Should().Be(habilitacao.Id);
    }

    [Fact(DisplayName = "O grupo lido, com os campos dele, fica ligado ao formulário lido com o código citado")]
    public void LerGruposColetados_CodigoDoFormulario_LigaOGrupoEOsCamposAoFormulario()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        FormularioProcesso inscricao = formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaInscricao)!;
        JsonObject documento = Documento("gruposDosFormularios", SerializacaoDosFatos.SerializarGruposColetados(
            [ItensDoFormularioDeTeste.Grupo("GRUPO", 0, inscricao)], formularios.CodigosPorIdentificador(), null, []));
        LeitorEnvelope leitor = new();

        IReadOnlyList<GrupoColetado> lidos = LeituraDosGrupos.LerGruposColetados(
            leitor, documento, new(StringComparer.Ordinal), [], formularios);

        leitor.Falhou.Should().BeFalse();
        GrupoColetado grupo = lidos.Should().ContainSingle().Subject;
        grupo.FormularioId.Should().Be(inscricao.Id);
        grupo.Subitens.Should().OnlyContain(campo => campo.FormularioId == inscricao.Id);
    }

    [Fact(DisplayName = "O grupo que cita formulário que o documento não traz é recusado")]
    public void LerGruposColetados_FormularioDesconhecido_Recusa()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        JsonObject documento = Documento("gruposDosFormularios", SerializacaoDosFatos.SerializarGruposColetados(
            [ItensDoFormularioDeTeste.Grupo("GRUPO", 0, formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaInscricao)!)], formularios.CodigosPorIdentificador(), null, []));
        documento["gruposDosFormularios"]![0]!["formularioCodigo"] = "OUTRO_FORMULARIO";
        LeitorEnvelope leitor = new();

        LeituraDosGrupos.LerGruposColetados(leitor, documento, new(StringComparer.Ordinal), [], formularios);

        leitor.Erro.Message.Should().Be("'gruposDosFormularios[0].formularioCodigo' não é o código de um formulário do documento.");
    }

    [Fact(DisplayName = "O campo sem a chave do formulário é recusado")]
    public void LerFatosColetados_ChaveDoFormularioAusente_Recusa()
    {
        FormulariosDoDocumento formularios = FormulariosDoDocumentoDeTeste.ComFormularios();
        JsonObject documento = Documento("dadosDosFormularios", SerializacaoDosFatos.SerializarFatosColetados(
            [ItensDoFormularioDeTeste.Campo("FATO", 0, formularios.De(FormulariosDoDocumentoDeTeste.CodigoDaInscricao)!)], formularios.CodigosPorIdentificador(), null));
        documento["dadosDosFormularios"]![0]!.AsObject().Remove("formularioCodigo");
        LeitorEnvelope leitor = new();

        LeituraDosFatos.LerFatosColetados(leitor, documento, formularios);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("formularioCodigo");
    }

    [Fact(DisplayName = "Cada formulário serializado traz só os termos que ele exige")]
    public void SerializarFormularios_TermosDeDoisFormularios_CadaUmTrazOsSeus()
    {
        ProcessoSeletivo processo = ExemploDeConfiguracao.Entrada().Processo;
        DefinirFormularioComTermo(processo, "HABILITACAO", obrigatorio: true, "TERMO_DA_HABILITACAO");
        DefinirFormularioComTermo(processo, "ISENCAO_TAXA", obrigatorio: false, "TERMO_DA_ISENCAO");

        JsonArray serializado = SerializacaoDosFormularios.SerializarFormularios(processo);

        TermosPorFormulario(serializado, "HABILITACAO").Should().Equal("TERMO_DA_HABILITACAO");
        TermosPorFormulario(serializado, "ISENCAO_TAXA").Should().Equal("TERMO_DA_ISENCAO");
    }

    private static void DefinirFormularioComTermo(ProcessoSeletivo processo, string codigo, bool obrigatorio, string codigoDoTermo)
    {
        CodigoDoFormulario formulario = CodigoDoFormulario.Criar(codigo).Value!;
        processo.DefinirFormulario(new FormularioDeclarado(formulario, null, obrigatorio), null, FormulariosDoDocumentoDeTeste.Paginas())
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(formulario, [Termo(codigoDoTermo, 1)]).IsSuccess.Should().BeTrue();
    }

    private static string[] TermosPorFormulario(JsonArray formularios, string codigo) =>
        [.. formularios.Single(f => f!["codigo"]!.GetValue<string>() == codigo)!["termos"]!.AsArray()
            .Select(static t => t!["codigo"]!.GetValue<string>())];

    private static TermoExigidoFormulario Termo(string codigo, int ordem) =>
        TermoExigidoFormulario.Criar(
            codigo,
            ordem,
            new VersaoTermoEscolhida(Guid.NewGuid(), Guid.NewGuid(), "CATALOGO", 1, "Termo fictício", "Texto fictício", "Base fictícia", FormaAceite.RegistroSemEnderecoIp, "hash fictício"),
            null,
            Obrigatoriedade.Sempre).Value!;

    private static JsonObject Documento(string bloco, JsonArray itens) =>
        JsonNode.Parse(PerfilCanonicoV1.Instancia.Serializar(new JsonObject { [bloco] = itens }))!.AsObject();
}
