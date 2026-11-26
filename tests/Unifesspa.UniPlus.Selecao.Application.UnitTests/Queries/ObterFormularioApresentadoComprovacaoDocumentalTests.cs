namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDoFormularioApresentado;

/// <summary>O bloco de comprovação documental do formulário apresentado: as exigências da fase e do código do formulário, o modelo editável e a recusa de versão com exigência malformada.</summary>
public sealed class ObterFormularioApresentadoComprovacaoDocumentalTests
{
    private const string CodigoDaInscricao = "INSCRICAO";

    private const string CodigoDaIsencao = "ISENCAO_TAXA";

    private const string CodigoDaHabilitacao = "HABILITACAO";

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
            Formulario(CodigoDaHabilitacao, faseHabilitacao, comComprovacao: true),
            Formulario(CodigoDaInscricao, faseInscricao, comComprovacao: false));
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioApresentadoDoProcessoDto> habilitacao = await HandleAsync(processoId, envelope, reidratado, CodigoDaHabilitacao);
        Result<FormularioApresentadoDoProcessoDto> inscricao = await HandleAsync(processoId, envelope, reidratado, CodigoDaInscricao);

        ConferirComprovacaoDaHabilitacao(habilitacao.Value!, processoId);
        inscricao.Value!.ComprovacaoDocumental.Should().BeNull("o formulário de inscrição não tem o bloco");
    }

    private static void ConferirComprovacaoDaHabilitacao(FormularioApresentadoDoProcessoDto habilitacao, Guid processoId)
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

        Result<FormularioApresentadoDoProcessoDto> habilitacao = await HandleAsync(
            processoId, ExigenciaComModeloNaFase(fase), EnvelopeCom(Formulario(CodigoDaHabilitacao, fase, comComprovacao: true)),
            CodigoDaHabilitacao, RepositorioComDivulgacaoAindaNaoCopiada(processoId));

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
    /// separa os documentos, e cada formulário mostra só os do seu código. O documento fora de
    /// formulário não aparece em nenhum.
    /// </summary>
    [Fact(DisplayName = "Na fase que divide inscrição e isenção, o bloco de comprovação de cada formulário lista só as exigências do seu código")]
    public async Task Handle_BlocoDeComprovacaoNaFaseCompartilhada_ListaSoAsExigenciasDoCodigo()
    {
        Guid fase = Guid.Parse("0199a000-0000-7000-8000-0000000000f1");
        string envelope = ExigenciasDaFaseCompartilhada(fase);
        EnvelopeReidratado reidratado = EnvelopeCom(
            Formulario(CodigoDaInscricao, fase, comComprovacao: true),
            Formulario(CodigoDaIsencao, fase, comComprovacao: true));
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioApresentadoDoProcessoDto> inscricao = await HandleAsync(processoId, envelope, reidratado, CodigoDaInscricao);
        Result<FormularioApresentadoDoProcessoDto> isencao = await HandleAsync(processoId, envelope, reidratado, CodigoDaIsencao);

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

    [Theory(DisplayName = "Com o bloco de comprovação, fase ou código do formulário ausente ou malformado recusa a versão em vez de omitir documento")]
    [InlineData(false, "\"0199a000-0000-7000-8000-0000000000f2\"", "\"formularioCodigo\": \"HABILITACAO\",")]
    [InlineData(true, "null", "\"formularioCodigo\": \"HABILITACAO\",")]
    [InlineData(true, "\"nao-e-guid\"", "\"formularioCodigo\": \"HABILITACAO\",")]
    [InlineData(true, "\"0199a000-0000-7000-8000-0000000000f2\"", "")]
    [InlineData(true, "\"0199a000-0000-7000-8000-0000000000f2\"", "\"formularioCodigo\": \"matricula 1\",")]
    public async Task Handle_BlocoDeComprovacaoComFaseOuCodigoAusente_Recusa(bool formularioComFase, string faseDaExigencia, string codigoDaExigencia)
    {
        string envelope = $$"""
            {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
              {"tipoDocumentoNome": "Documento de identidade", "aplicabilidade": "GERAL", "obrigatorio": true, "exigidoNaFaseId": {{faseDaExigencia}},
               {{codigoDaExigencia}} "formatosPermitidos": {"lista": null, "qualquer": true}, "prazoDeEmissao": null, "validade": null, "modelo": null }
            ] } }
            """;
        Guid? faseDoFormulario = formularioComFase ? Guid.Parse("0199a000-0000-7000-8000-0000000000f2") : null;

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(
            Guid.CreateVersion7(), envelope,
            EnvelopeCom(Formulario(CodigoDaHabilitacao, faseDoFormulario, comComprovacao: true)), CodigoDaHabilitacao);

        resultado.Error!.Code.Should().Be("CertamePublicado.EnvelopeInesperado");
    }

    [Fact(DisplayName = "A comprovação traz o prazo de emissão com a data resolvida nas fases e no fuso congelados da versão")]
    public async Task Handle_PrazoNaComprovacao_ResolveADataNoCronogramaEFusoDaVersao()
    {
        Guid faseHabilitacao = Guid.Parse("0199a000-0000-7000-8000-0000000000f2");
        EnvelopeReidratado reidratado = EnvelopeCom(Formulario(CodigoDaHabilitacao, faseHabilitacao, comComprovacao: true));
        Guid faseDoCronograma = reidratado.Configuracao.Cronograma.CronogramaFases[0].Id;
        string envelope = $$"""
            {"documentosExigidos": {"dataDeReferenciaDosDados": null, "exigencias": [
              {{ExigenciaComPrazo(faseHabilitacao, faseDoCronograma)}}
            ] } }
            """;

        Result<FormularioApresentadoDoProcessoDto> habilitacao = await HandleAsync(Guid.CreateVersion7(), envelope, reidratado, CodigoDaHabilitacao);

        PrazoDeEmissaoCertameDto prazo = habilitacao.Value!.ComprovacaoDocumental!.Single().PrazoDeEmissao!;
        prazo.Referencia.FaseId.Should().Be(faseDoCronograma);
        prazo.Referencia.Data.Should().Be(new DateOnly(2025, 12, 31), "o início da fase, 1º de janeiro à meia-noite em UTC, é 31 de dezembro no fuso da versão");
    }

    private static string ExigenciaComPrazo(Guid fase, Guid faseDeReferencia) => $$"""
        {"tipoDocumentoNome": "Comprovante de residência", "aplicabilidade": "GERAL", "obrigatorio": true, "exigidoNaFaseId": "{{fase}}",
         "formularioCodigo": "HABILITACAO", "formatosPermitidos": {"lista": null, "qualquer": true}, "modelo": null, "validade": null,
         "prazoDeEmissao": {"escolha": "COM_PRAZO", "valor": 3, "unidade": "MESES", "efeitoForaDoPrazo": "ACEITAR_COM_AVISO",
           "referencia": {"tipo": "INICIO_DA_FASE", "data": null, "faseId": "{{faseDeReferencia}}" } } }
        """;

    private const string Modelo = """
        {"modeloId": "0199a000-0000-7000-8000-0000000000aa", "nomeArquivo": "Autodeclaração.odt", "formato": "ODT", "hashSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}
        """;

    private static string Exigencia(string nome, string aplicabilidade, Guid fase, string? formularioCodigo, string modelo = "null") => $$"""
        {"tipoDocumentoNome": "{{nome}}", "aplicabilidade": "{{aplicabilidade}}", "obrigatorio": true, "exigidoNaFaseId": "{{fase}}",
         "formularioCodigo": {{(formularioCodigo is null ? "null" : $"\"{formularioCodigo}\"")}}, "formatosPermitidos": {"lista": null, "qualquer": true}, "prazoDeEmissao": null, "validade": null, "modelo": {{modelo}} }
        """;
}
