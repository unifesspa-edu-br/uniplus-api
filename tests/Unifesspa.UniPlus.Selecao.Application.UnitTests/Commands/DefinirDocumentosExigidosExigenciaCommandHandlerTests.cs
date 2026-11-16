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
        DefinirDocumentosExigidosCommand command = new(processoId, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com tipo de documento não encontrado retorna DocumentoExigido.TipoDocumentoNaoEncontrado")]
    public async Task Handle_TipoDocumentoNaoEncontrado_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns((TipoDocumentoView?)null);

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.TipoDocumentoNaoEncontrado");
    }

    [Theory(DisplayName = "A exigência copia o modelo confirmado do processo; o modelo pendente e o de outro processo são recusados")]
    [InlineData(true, true, null)]
    [InlineData(false, true, "ModeloDeDocumento.NaoConfirmado")]
    [InlineData(true, false, "DocumentoExigido.ModeloNaoEncontrado")]
    public async Task Handle_ModeloDaExigencia(bool confirmado, bool doProcesso, string? recusa)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>()).Returns(TipoDocumentoResultado(tipoDocumentoId));
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(processo.Id, "Autodeclaração", "ODT", TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            modelo.Confirmar(10, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        }

        mocks.Modelos.ListarDoProcessoAsync(processo.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(doProcesso ? [modelo] : []);
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null, ModeloId: modelo.Id);

        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        (resultado.Error?.Code).Should().Be(recusa);
        if (recusa is null)
        {
            processo.DocumentosExigidos.Should().ContainSingle().Which.Modelo
                .Should().Be(new ModeloDaExigencia(modelo.Id, "Autodeclaração.odt", FormatoDeDocumento.Odt, new string('a', 64)));
        }
    }

    [Fact(DisplayName = "Handle com item válido (sem gatilho) define os documentos exigidos e persiste")]
    public async Task Handle_ItemValido_DefineDocumentosExigidosEPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.TipoDocumentoCodigo == "IDENTIDADE");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle congela na exigência a proteção do tipo de documento e os fatos que ela comprova")]
    public async Task Handle_CongelaAProtecaoDoTipoEOsFatosComprovados()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(new TipoDocumentoView(tipoDocumentoId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null, FatosComprovados: ["TIPO_DEFICIENCIA"]);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        exigencia.FinalidadeTratamento.Should().Be("Comprovação do atendimento especializado.");
        exigencia.HipoteseLegal.Should().Be("TUTELA_DA_SAUDE");
        exigencia.FatosComprovados.Should().Equal("TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Tipo de documento sem a proteção aprovada não entra em exigência nova, mas continua onde o processo já o exige")]
    public async Task Handle_TipoComProtecaoPendente_RecusaVinculoNovoEMantemOExistente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        TipoDocumentoView aprovado = new(tipoDocumentoId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true);
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>()).Returns(aprovado with { ProtecaoAprovada = false });
        Result<MutacaoAceita> novo = await HandleAsync(mocks, command);
        novo.Error!.Code.Should().Be("DocumentoExigido.TipoDocumentoComProtecaoPendente");
        processo.DocumentosExigidos.Should().BeEmpty();

        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>()).Returns(aprovado);
        (await HandleAsync(mocks, command)).IsSuccess.Should().BeTrue();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>()).Returns(aprovado with { ProtecaoAprovada = false });
        (await HandleAsync(mocks, command)).IsSuccess.Should().BeTrue("o vínculo existente não é vínculo novo");
    }

    [Theory(DisplayName = "O fato comprovado novo, pendente na proteção ou desativado, é vínculo novo recusado; o que o processo já comprova continua")]
    [InlineData("pendente", "VinculoCatalogo.FatoComProtecaoPendente")]
    [InlineData("desativado", "VinculoCatalogo.FatoDesativado")]
    public async Task Handle_FatoComprovadoNovoNaoAprovado_RecusaSoOVinculoNovo(string estado, string codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(new TipoDocumentoView(tipoDocumentoId, "LAUDO_MEDICO", "Laudo médico", "SAUDE", ClassificacaoDeProtecao.Sensivel, "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE", ProtecaoAprovada: true));
        FatoCandidatoView fato = FatoTipoDeficiencia() with { ProtecaoAprovada = estado != "pendente", Ativo = estado != "desativado" };
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([fato]);
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, Qualquer, null, FatosComprovados: ["TIPO_DEFICIENCIA"]);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> novo = await HandleAsync(mocks, command);

        novo.Error!.Code.Should().Be(codigoEsperado);
        processo.DocumentosExigidos.Should().BeEmpty();

        // Já vinculado: a mesma entrada, sobre o catálogo que depois perdeu a aprovação, não é vínculo novo.
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([FatoTipoDeficiencia()]);
        (await HandleAsync(mocks, command)).IsSuccess.Should().BeTrue();
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([fato]);
        (await HandleAsync(mocks, command)).IsSuccess.Should().BeTrue("o fato que o processo já comprova não é vínculo novo");
    }

    [Fact(DisplayName = "Handle com fatos comprovados repetidos recusa na forma, antes de ler o tipo de documento")]
    public async Task Handle_FatosComprovadosRepetidos_RecusaAntesDeLerOTipo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);

        ItemDocumentoExigidoInput item = new(fase.Id, Guid.CreateVersion7(), "GERAL", true, null, [], [], null, Qualquer, null, FatosComprovados: ["A", "A"]);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(DocumentoExigido.FatosComprovadosInvalidos);
        await mocks.TipoDocumentoReader.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CA-07 (Story #554, PR #898): mesma referência documental em exigências distintas preserva bases legais distintas, correlacionadas por identidade")]
    public async Task Handle_MesmaReferenciaDocumental_BasesDistintasPorExigencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        BaseLegalInput baseFederal = new("Lei Federal X", "FEDERAL", "RESOLVIDO", null);
        BaseLegalInput baseEdital = new("Cláusula do edital", "INTERNA_EDITAL", "RESOLVIDO", null);
        ItemDocumentoExigidoInput primeiraExigencia = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [baseFederal], null, Qualquer, null);
        ItemDocumentoExigidoInput segundaExigencia = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [baseEdital], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", primeiraExigencia, null, null, null, null), new NoExigenciaInput("FOLHA", segundaExigencia, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(2);
        processo.DocumentosExigidos.Should().Contain(d => d.BasesLegais.Single().Referencia == "Lei Federal X");
        processo.DocumentosExigidos.Should().Contain(d => d.BasesLegais.Single().Referencia == "Cláusula do edital");
        processo.DocumentosExigidos.Select(d => d.Id).Should().OnlyHaveUniqueItems(
            "a correlação é pela identidade da própria exigência (ADR-0072), não pelo tipo de documento");
    }
}
