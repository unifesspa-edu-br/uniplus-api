namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDoFormularioRenderizavel;

/// <summary>O bloco de comprovação documental do formulário renderizável: as exigências da fase e da finalidade, o modelo editável e a recusa de versão com exigência malformada.</summary>
public sealed class ObterFormularioRenderizavelComprovacaoDocumentalTests
{
    /// <summary>Repositório cuja linha de divulgação existe e cujo ato ainda espera a cópia ao acervo.</summary>
    private static ICertameDivulgadoRepository RepositorioComDivulgacaoAindaNaoCopiada(Guid processoId) =>
        RepositorioDe(Divulgado(processoId));

    [Fact(DisplayName = "O bloco de comprovação documental lista as exigências da fase do formulário, na forma pública e com o modelo editável; sem o bloco, nada")]
    public async Task Handle_BlocoDeComprovacao_ListaAsExigenciasDaFaseDoFormulario()
    {
        Guid faseHabilitacao = Guid.Parse("0199a000-0000-7000-8000-0000000000f2");
        Guid faseInscricao = Guid.Parse("0199a000-0000-7000-8000-0000000000f1");
        string envelope = ExigenciasDasFases(faseHabilitacao, faseInscricao);
        EnvelopeReidratado reidratado = EnvelopeCom(
            Formulario(FinalidadeFormulario.Habilitacao, faseHabilitacao, comComprovacao: true),
            Formulario(FinalidadeFormulario.Inscricao, faseInscricao, comComprovacao: false));
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> habilitacao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.Habilitacao);
        Result<FormularioRenderizavelDto> inscricao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.Inscricao);

        ConferirComprovacaoDaHabilitacao(habilitacao.Value!, processoId);
        inscricao.Value!.ComprovacaoDocumental.Should().BeNull("o formulário de inscrição não tem o bloco");
    }

    private static void ConferirComprovacaoDaHabilitacao(FormularioRenderizavelDto habilitacao, Guid processoId)
    {
        habilitacao.ComprovacaoDocumental!.Select(static e => (e.Rotulo, e.Aplicabilidade.Codigo)).Should().Equal(
            [("Documento de identidade", "GERAL"), ("Autodeclaração étnico-racial", "CONDICIONAL")],
            "só as exigências da fase da habilitação, na ordem congelada");
        habilitacao.ComprovacaoDocumental!.Select(static e => e.Modelo).Should().BeEquivalentTo(
            [
                null,
                ModeloNoAcervo(processoId),
            ],
            "a exigência com modelo o traz com o endereço no acervo do ato da divulgação, sem o id do cadastro como campo; a sem modelo, nulo");
    }

    private static ModeloDocumentalCertameDto ModeloNoAcervo(Guid processoId) => new(
        "Autodeclaração.odt", "ODT", new string('a', 64),
        new Uri($"https://acervo.teste/selecao/processos-seletivos/{processoId:D}/atos/{IdDoAtoDaDivulgacao:D}"
            + $"/modelos-de-documento/0199a000-0000-7000-8000-0000000000aa/{new string('a', 64)}.odt"));

    private static string ExigenciasDasFases(Guid faseHabilitacao, Guid faseInscricao) => $$"""
            {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
              {{Exigencia("Documento de identidade", "GERAL", faseHabilitacao, "HABILITACAO")}},
              {{Exigencia("Comprovante de inscrição", "GERAL", faseInscricao, "INSCRICAO")}},
              {{Exigencia("Autodeclaração étnico-racial", "CONDICIONAL", faseHabilitacao, "HABILITACAO", Modelo)}}
            ] } }
            """;

    [Fact(DisplayName = "Antes de a cópia ao acervo registrar o endereço do ato, o modelo da comprovação não traz endereço")]
    public async Task Handle_AntesDaCopiaAoAcervo_ModeloSemEndereco()
    {
        Guid fase = Guid.Parse("0199a000-0000-7000-8000-0000000000f2");
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> habilitacao = await HandleAsync(
            processoId, ExigenciaComModeloNaFase(fase), EnvelopeCom(Formulario(FinalidadeFormulario.Habilitacao, fase, comComprovacao: true)),
            FinalidadeFormulario.Habilitacao, RepositorioComDivulgacaoAindaNaoCopiada(processoId));

        ModeloDocumentalCertameDto modelo = habilitacao.Value!.ComprovacaoDocumental!.Single().Modelo!;
        modelo.NomeArquivo.Should().Be("Autodeclaração.odt");
        modelo.UrlDownload.Should().BeNull("o arquivo pode ainda não estar no acervo, e o endereço só é exposto depois da cópia");
    }

    private static string ExigenciaComModeloNaFase(Guid fase) => $$"""
        {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
          {{Exigencia("Autodeclaração étnico-racial", "CONDICIONAL", fase, "HABILITACAO", Modelo)}}
        ] } }
        """;

    /// <summary>
    /// Inscrição e isenção respondidas na mesma fase, as duas com o bloco de comprovação: a fase não
    /// separa os documentos, e cada formulário mostra só os da sua finalidade. O documento fora de
    /// formulário não aparece em nenhum.
    /// </summary>
    [Fact(DisplayName = "Na fase que divide inscrição e isenção, o bloco de comprovação de cada formulário lista só as exigências da sua finalidade")]
    public async Task Handle_BlocoDeComprovacaoNaFaseCompartilhada_ListaSoAsExigenciasDaFinalidade()
    {
        Guid fase = Guid.Parse("0199a000-0000-7000-8000-0000000000f1");
        string envelope = ExigenciasDaFaseCompartilhada(fase);
        EnvelopeReidratado reidratado = EnvelopeCom(
            Formulario(FinalidadeFormulario.Inscricao, fase, comComprovacao: true),
            Formulario(FinalidadeFormulario.IsencaoTaxa, fase, comComprovacao: true));
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> inscricao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.Inscricao);
        Result<FormularioRenderizavelDto> isencao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.IsencaoTaxa);

        inscricao.Value!.ComprovacaoDocumental!.Select(static e => e.Rotulo).Should().Equal("Documento de identidade");
        isencao.Value!.ComprovacaoDocumental!.Select(static e => e.Rotulo).Should().Equal("Comprovante de renda");
    }

    private static string ExigenciasDaFaseCompartilhada(Guid fase) => $$"""
            {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
              {{Exigencia("Documento de identidade", "GERAL", fase, "INSCRICAO")}},
              {{Exigencia("Comprovante de renda", "GERAL", fase, "ISENCAO_TAXA")}},
              {{Exigencia("Laudo da banca", "GERAL", fase, null)}}
            ] } }
            """;

    [Theory(DisplayName = "Com o bloco de comprovação, fase ou finalidade ausente ou malformada recusa a versão em vez de omitir documento")]
    [InlineData(false, "\"0199a000-0000-7000-8000-0000000000f2\"", "\"finalidade\": \"HABILITACAO\",")]
    [InlineData(true, "null", "\"finalidade\": \"HABILITACAO\",")]
    [InlineData(true, "\"nao-e-guid\"", "\"finalidade\": \"HABILITACAO\",")]
    [InlineData(true, "\"0199a000-0000-7000-8000-0000000000f2\"", "")]
    [InlineData(true, "\"0199a000-0000-7000-8000-0000000000f2\"", "\"finalidade\": \"MATRICULA\",")]
    public async Task Handle_BlocoDeComprovacaoComFaseOuFinalidadeAusente_Recusa(bool formularioComFase, string faseDaExigencia, string finalidadeDaExigencia)
    {
        string envelope = $$"""
            {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
              {"tipoDocumentoNome": "Documento de identidade", "aplicabilidade": "GERAL", "obrigatorio": true, "exigidoNaFaseId": {{faseDaExigencia}},
               {{finalidadeDaExigencia}} "formatosPermitidos": {"lista": null, "qualquer": true}, "prazoDeEmissao": null, "validade": null, "modelo": null }
            ] } }
            """;
        Guid? faseDoFormulario = formularioComFase ? Guid.Parse("0199a000-0000-7000-8000-0000000000f2") : null;

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            Guid.CreateVersion7(), envelope,
            EnvelopeCom(Formulario(FinalidadeFormulario.Habilitacao, faseDoFormulario, comComprovacao: true)), FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("CertamePublicado.EnvelopeInesperado");
    }

    [Fact(DisplayName = "A comprovação traz o prazo de emissão com a data resolvida nas fases e no fuso congelados da versão")]
    public async Task Handle_PrazoNaComprovacao_ResolveADataNoCronogramaEFusoDaVersao()
    {
        Guid faseHabilitacao = Guid.Parse("0199a000-0000-7000-8000-0000000000f2");
        EnvelopeReidratado reidratado = EnvelopeCom(Formulario(FinalidadeFormulario.Habilitacao, faseHabilitacao, comComprovacao: true));
        Guid faseDoCronograma = reidratado.Configuracao.Cronograma.CronogramaFases[0].Id;
        string envelope = $$"""
            {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
              {{ExigenciaComPrazo(faseHabilitacao, faseDoCronograma)}}
            ] } }
            """;

        Result<FormularioRenderizavelDto> habilitacao = await HandleAsync(Guid.CreateVersion7(), envelope, reidratado, FinalidadeFormulario.Habilitacao);

        PrazoDeEmissaoCertameDto prazo = habilitacao.Value!.ComprovacaoDocumental!.Single().PrazoDeEmissao!;
        prazo.Referencia.FaseId.Should().Be(faseDoCronograma);
        prazo.Referencia.Data.Should().Be(new DateOnly(2025, 12, 31), "o início da fase, 1º de janeiro à meia-noite em UTC, é 31 de dezembro no fuso da versão");
    }

    private static string ExigenciaComPrazo(Guid fase, Guid faseDeReferencia) => $$"""
        {"tipoDocumentoNome": "Comprovante de residência", "aplicabilidade": "GERAL", "obrigatorio": true, "exigidoNaFaseId": "{{fase}}",
         "finalidade": "HABILITACAO", "formatosPermitidos": {"lista": null, "qualquer": true}, "modelo": null, "validade": null,
         "prazoDeEmissao": {"escolha": "COM_PRAZO", "valor": 3, "unidade": "MESES", "efeitoForaDoPrazo": "ACEITAR_COM_AVISO",
           "referencia": {"tipo": "INICIO_DA_FASE", "data": null, "faseId": "{{faseDeReferencia}}" } } }
        """;

    private const string Modelo = """
        {"modeloId": "0199a000-0000-7000-8000-0000000000aa", "nomeArquivo": "Autodeclaração.odt", "formato": "ODT", "hashSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}
        """;

    private static string Exigencia(string nome, string aplicabilidade, Guid fase, string? finalidade, string modelo = "null") => $$"""
        {"tipoDocumentoNome": "{{nome}}", "aplicabilidade": "{{aplicabilidade}}", "obrigatorio": true, "exigidoNaFaseId": "{{fase}}",
         "finalidade": {{(finalidade is null ? "null" : $"\"{finalidade}\"")}}, "formatosPermitidos": {"lista": null, "qualquer": true}, "prazoDeEmissao": null, "validade": null, "modelo": {{modelo}} }
        """;
}
