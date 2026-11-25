namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// A exigência de documento: o processo e o tipo de documento existem, o modelo confirmado é copiado, a proteção do tipo e os fatos comprovados são congelados e só o vínculo novo não aprovado é recusado.
/// </summary>
public sealed class DefinirDocumentosExigidosExigenciaCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);
        DefinirDocumentosExigidosCommand command = new(processoId, []);

        Result resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com tipo de documento não encontrado retorna DocumentoExigido.TipoDocumentoNaoEncontrado")]
    public async Task Handle_TipoDocumentoNaoEncontrado_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase().ComTipoDeDocumento(_ => null);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.TipoDocumentoNaoEncontrado");
    }

    [Theory(DisplayName = "A exigência copia o modelo confirmado do processo; o modelo pendente e o de outro processo são recusados")]
    [InlineData(true, true, null)]
    [InlineData(false, true, "ModeloDeDocumento.NaoConfirmado")]
    [InlineData(true, false, "DocumentoExigido.ModeloNaoEncontrado")]
    public async Task Handle_ModeloDaExigencia(bool confirmado, bool doProcesso, string? recusa)
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ModeloDeDocumento modelo = ModeloPendenteOuConfirmado(cenario.Processo.Id, confirmado);
        cenario.Mocks.Modelos.ListarDoProcessoAsync(cenario.Processo.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(doProcesso ? [modelo] : []);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComModelo(modelo.Id));

        (resultado.Error?.Code).Should().Be(recusa);
        ConferirModeloCopiadoQuandoAceito(cenario.Processo, modelo, aceito: recusa is null);
    }

    [Fact(DisplayName = "Handle com item válido (sem gatilho) define os documentos exigidos e persiste")]
    public async Task Handle_ItemValido_DefineDocumentosExigidosEPersiste()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.DocumentosExigidos.Should().ContainSingle(d => d.TipoDocumentoCodigo == "IDENTIDADE");
        await cenario.Mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cenario.Mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle congela na exigência a proteção do tipo de documento e os fatos que ela comprova")]
    public async Task Handle_CongelaAProtecaoDoTipoEOsFatosComprovados()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase().ComTipoDeDocumento(LaudoMedico);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComFatosComprovados("TIPO_DEFICIENCIA"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = cenario.Processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        exigencia.FinalidadeTratamento.Should().Be("Comprovação do atendimento especializado.");
        exigencia.HipoteseLegal.Should().Be("TUTELA_DA_SAUDE");
        exigencia.FatosComprovados.Should().Equal("TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Tipo de documento sem a proteção aprovada não entra em exigência nova, mas continua onde o processo já o exige")]
    public async Task Handle_TipoComProtecaoPendente_RecusaVinculoNovoEMantemOExistente()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        TipoDocumentoView aprovado = LaudoMedico(cenario.TipoDocumentoId);
        DefinirDocumentosExigidosCommand command = cenario.Comando(cenario.ItemGeral());

        Result novo = await HandleComTipoAsync(cenario, command, aprovado with { ProtecaoAprovada = false });
        novo.Error!.Code.Should().Be("DocumentoExigido.TipoDocumentoComProtecaoPendente");
        cenario.Processo.DocumentosExigidos.Should().BeEmpty();

        (await HandleComTipoAsync(cenario, command, aprovado)).IsSuccess.Should().BeTrue();
        (await HandleComTipoAsync(cenario, command, aprovado with { ProtecaoAprovada = false })).IsSuccess.Should().BeTrue("o vínculo existente não é vínculo novo");
    }

    /// <summary>
    /// Já vinculado: a mesma entrada, sobre o catálogo que depois perdeu a aprovação, não é vínculo novo.
    /// </summary>
    [Theory(DisplayName = "O fato comprovado novo, pendente na proteção ou desativado, é vínculo novo recusado; o que o processo já comprova continua")]
    [InlineData("pendente", "VinculoCatalogo.FatoComProtecaoPendente")]
    [InlineData("desativado", "VinculoCatalogo.FatoDesativado")]
    public async Task Handle_FatoComprovadoNovoNaoAprovado_RecusaSoOVinculoNovo(string estado, string codigoEsperado)
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase().ComTipoDeDocumento(LaudoMedico);
        FatoCandidatoView fato = FatoTipoDeficiencia() with { ProtecaoAprovada = estado != "pendente", Ativo = estado != "desativado" };
        DefinirDocumentosExigidosCommand command = cenario.Comando(cenario.ItemGeral().ComFatosComprovados("TIPO_DEFICIENCIA"));

        Result novo = await HandleComCatalogoAsync(cenario, command, [fato]);

        novo.Error!.Code.Should().Be(codigoEsperado);
        cenario.Processo.DocumentosExigidos.Should().BeEmpty();

        (await HandleComCatalogoAsync(cenario, command, [FatoTipoDeficiencia()])).IsSuccess.Should().BeTrue();
        (await HandleComCatalogoAsync(cenario, command, [fato])).IsSuccess.Should().BeTrue("o fato que o processo já comprova não é vínculo novo");
    }

    [Fact(DisplayName = "Handle com fatos comprovados repetidos recusa na forma e consulta o tipo de documento")]
    public async Task Handle_FatosComprovadosRepetidos_RecusaEConsultaOTipo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);

        ItemDocumentoExigidoDeTeste item = ItemDocumentoExigidoDeTeste.Geral(fase.Id, Guid.CreateVersion7()).ComFatosComprovados("A", "A");
        DefinirDocumentosExigidosCommand command = new(processo.Id, [RegraDeComprovacaoDeTeste.DeDocumento(item).Construir()]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => e.Error.Code).Should().Equal(DocumentoExigido.FatosComprovadosInvalidos, "DocumentoExigido.TipoDocumentoNaoEncontrado");
        await mocks.TipoDocumentoReader.Received().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-07: mesma referência documental em exigências distintas preserva bases legais distintas, correlacionadas por identidade")]
    public async Task Handle_MesmaReferenciaDocumental_BasesDistintasPorExigencia()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste primeira = cenario.ItemGeral().ComBasesLegais(new BaseLegalInput("Lei Federal X", "FEDERAL", "RESOLVIDO", null));
        ItemDocumentoExigidoDeTeste segunda = cenario.ItemGeral().ComBasesLegais(new BaseLegalInput("Cláusula do edital", "INTERNA_EDITAL", "RESOLVIDO", null));
        DefinirDocumentosExigidosCommand command = new(
            cenario.Processo.Id, [RegraDeComprovacaoDeTeste.DeDocumento(primeira).Construir(), RegraDeComprovacaoDeTeste.DeDocumento(segunda).Construir()]);

        Result resultado = await HandleAsync(cenario.Mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ConferirBasesLegaisDistintasPorExigencia(cenario.Processo);
    }

    [Fact]
    public async Task Handle_DuasExigenciasDoMesmoTipo_FormaChavesDistintas()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemGeral();
        DefinirDocumentosExigidosCommand comando = new(cenario.Processo.Id,
            [RegraDeComprovacaoDeTeste.DeDocumento(item).Construir(), RegraDeComprovacaoDeTeste.DeDocumento(item).Construir()]);

        Result resultado = await HandleAsync(cenario.Mocks, comando);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.DocumentosExigidos.Select(static documento => documento.Chave.Valor)
            .Should().Equal("exigencia_identidade", "exigencia_identidade_2");
    }

    private static TipoDocumentoView LaudoMedico(Guid tipoDocumentoId) =>
        new(tipoDocumentoId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true);

    private static Task<Result> HandleComTipoAsync(CenarioDaExigencia cenario, DefinirDocumentosExigidosCommand command, TipoDocumentoView tipo)
    {
        cenario.Mocks.TipoDocumentoReader.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        return HandleAsync(cenario.Mocks, command);
    }

    private static Task<Result> HandleComCatalogoAsync(
        CenarioDaExigencia cenario, DefinirDocumentosExigidosCommand command, IReadOnlyList<FatoCandidatoView> catalogo)
    {
        cenario.Mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(catalogo);
        return HandleAsync(cenario.Mocks, command);
    }

    private static ModeloDeDocumento ModeloPendenteOuConfirmado(Guid processoId, bool confirmado)
    {
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(processoId, "Autodeclaração", "ODT", TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            modelo.Confirmar(10, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        }

        return modelo;
    }

    private static void ConferirModeloCopiadoQuandoAceito(ProcessoSeletivo processo, ModeloDeDocumento modelo, bool aceito)
    {
        if (!aceito)
        {
            return;
        }

        processo.DocumentosExigidos.Should().ContainSingle().Which.Modelo
            .Should().Be(new ModeloDaExigencia(modelo.Id, "Autodeclaração.odt", FormatoDeDocumento.Odt, new string('a', 64)));
    }

    private static void ConferirBasesLegaisDistintasPorExigencia(ProcessoSeletivo processo)
    {
        processo.DocumentosExigidos.Should().HaveCount(2);
        processo.DocumentosExigidos.Should().Contain(d => d.BasesLegais.Single().Referencia == "Lei Federal X");
        processo.DocumentosExigidos.Should().Contain(d => d.BasesLegais.Single().Referencia == "Cláusula do edital");
        processo.DocumentosExigidos.Select(d => d.Id).Should().OnlyHaveUniqueItems(
            "a correlação é pela identidade da própria exigência (ADR-0072), não pelo tipo de documento");
    }
}
