namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do <see cref="DefinirDocumentosExigidosCommandHandler"/> (Story #554): a
/// resolução do snapshot-copy de <c>TipoDocumento</c> (Configuração, ADR-0056), do
/// vocabulário de fatos estendido pelo domínio dinâmico da oferta do processo (PR #896), e
/// os erros nomeados que uma resolução malsucedida produz.
/// </summary>
public sealed class DefinirDocumentosExigidosCommandHandlerTests
{
    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        ITipoDocumentoReader TipoDocumentoReader,
        IFatoCandidatoReader FatoCandidatoReader,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        return new Mocks(
            repository,
            Substitute.For<ITipoDocumentoReader>(),
            Substitute.For<IFatoCandidatoReader>(),
            Substitute.For<ISelecaoUnitOfWork>());
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirDocumentosExigidosCommand command) =>
        DefinirDocumentosExigidosCommandHandler.Handle(
            command,
            mocks.Repository,
            mocks.TipoDocumentoReader,
            mocks.FatoCandidatoReader,
            mocks.UnitOfWork,
            TimeProvider.System,
            CancellationToken.None);

    private static TipoDocumentoView TipoDocumentoResultado(Guid id) =>
        new(id, "IDENTIDADE", "Documento de identidade", "PESSOAL");

    private static FaseCronograma FaseQualquer() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, produzResultado: false,
        resultadoDefinitivo: false, coletaInscricao: false, inicio: null, fim: null,
        atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    private static FatoCandidatoView FatoSexo() => new(
        Guid.CreateVersion7(), "SEXO", "Sexo", null, "CATEGORICO", "DECLARADO", "ESCALAR",
        ["MASCULINO", "FEMININO", "INTERSEXO"], "INSCRICAO", "CAMPO_INSCRICAO:SEXO", null);

    private static FatoCandidatoView FatoModalidade() => new(
        Guid.CreateVersion7(), "MODALIDADE", "Modalidade", null, "CATEGORICO", "DECLARADO", "MULTIVALORADO", null,
        "INSCRICAO", "CAMPO_INSCRICAO:MODALIDADE", null);

    private static FatoCandidatoView FatoTipoDeficiencia() => new(
        Guid.CreateVersion7(), "TIPO_DEFICIENCIA", "Tipo de deficiência", null, "CATEGORICO", "DECLARADO", "ESCALAR", null,
        "INSCRICAO", "CAMPO_INSCRICAO:TIPO_DEFICIENCIA", null);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns((TipoDocumentoView?)null);

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.TipoDocumentoNaoEncontrado");
    }

    [Fact(DisplayName = "Handle com item válido (sem gatilho) define os documentos exigidos e persiste")]
    public async Task Handle_ItemValido_DefineDocumentosExigidosEPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.TipoDocumentoCodigo == "IDENTIDADE");
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await mocks.FatoCandidatoReader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com gatilho válido (fato escalar) resolve o vocabulário e persiste a condição")]
    public async Task Handle_GatilhoValidoEscalar_PersisteCondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[FatoSexo()]);

        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, null, [condicao], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO");
    }

    [Fact(DisplayName = "Handle com gatilho de fato desconhecido retorna PredicadoDnf.FatoDesconhecido")]
    public async Task Handle_GatilhoFatoDesconhecido_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[]);

        CondicaoGatilhoInput condicao = new(0, "FATO_INEXISTENTE", "IGUAL", "\"X\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, null, [condicao], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "CA-03: gatilho por MODALIDADE não ofertada pelo processo é recusado (integridade referencial)")]
    public async Task Handle_GatilhoModalidadeNaoOfertada_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Processo NÃO oferece nenhuma modalidade — DistribuicaoVagas vazia.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[FatoModalidade()]);

        CondicaoGatilhoInput condicao = new(0, "MODALIDADE", "IGUAL", "\"LB_PPI\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, null, [condicao], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    [Fact(DisplayName = "Story #917: TIPO_DEFICIENCIA participa do domínio dinâmico igual a MODALIDADE/CONDICAO_ATENDIMENTO")]
    public async Task Handle_GatilhoTipoDeficienciaOfertado_PersisteCondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaTipoDeficiencia tipoTea = OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "TEA");
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar([condicaoPcd], [], [tipoTea]).Value!;
        processo.DefinirOfertaAtendimento(oferta, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]);

        CondicaoGatilhoInput condicao = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, null, [condicao], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Story #917/CA-03: gatilho por TIPO_DEFICIENCIA não ofertado pelo processo é recusado (integridade referencial)")]
    public async Task Handle_GatilhoTipoDeficienciaNaoOfertado_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Processo NÃO oferece atendimento especializado — OfertaAtendimento nulo.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]);

        CondicaoGatilhoInput condicao = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, null, [condicao], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    [Fact(DisplayName = "CA-07 (Story #554, PR #898): mesma referência documental em exigências distintas preserva bases legais distintas, correlacionadas por identidade")]
    public async Task Handle_MesmaReferenciaDocumental_BasesDistintasPorExigencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        BaseLegalInput baseFederal = new("Lei Federal X", "FEDERAL", "RESOLVIDO", null);
        BaseLegalInput baseEdital = new("Cláusula do edital", "INTERNA_EDITAL", "RESOLVIDO", null);
        ItemDocumentoExigidoInput primeiraExigencia = new(fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [baseFederal], null, null, null);
        ItemDocumentoExigidoInput segundaExigencia = new(fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [baseEdital], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [primeiraExigencia, segundaExigencia], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(2);
        processo.DocumentosExigidos.Should().Contain(d => d.BasesLegais.Single().Referencia == "Lei Federal X");
        processo.DocumentosExigidos.Should().Contain(d => d.BasesLegais.Single().Referencia == "Cláusula do edital");
        processo.DocumentosExigidos.Select(d => d.Id).Should().OnlyHaveUniqueItems(
            "a correlação é pela identidade da própria exigência (ADR-0072), não pelo tipo de documento");
    }

    [Fact(DisplayName = "Story #554/issue #893 (PR #900): reenviar o PUT com FormatoPermitido/TamanhoMaximoBytes diferentes substitui integralmente (não faz merge)")]
    public async Task Handle_ReenviarComFormatoETamanhoDiferentes_SubstituiIntegralmente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput primeiro = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [], null, "PDF", 5_000_000);
        (await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(processo.Id, [primeiro], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Single().FormatoPermitido.Should().Be(FormatoPermitido.Pdf);
        processo.DocumentosExigidos.Single().TamanhoMaximoBytes.Should().Be(5_000_000);

        ItemDocumentoExigidoInput segundo = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [], null, "JPEG", 2_000_000);
        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [segundo], PrecondicaoIfMatch.Curinga));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.FormatoPermitido.Should().Be(FormatoPermitido.Jpeg, "substituição integral, sem vestígio do valor anterior");
        exigencia.TamanhoMaximoBytes.Should().Be(2_000_000);
    }

    [Fact(DisplayName = "CA-08: âncora de fase de IdadeMaximaEmissao que não pertence ao processo é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseDeOutroProcesso_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, Guid.CreateVersion7());
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [], idade, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "CA-08: âncora de fase de IdadeMaximaEmissao sem o extremo definido é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseSemExtremo_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        // FaseQualquer() não tem Inicio/Fim definidos — FIM_FASE não tem extremo a apontar.
        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, fase.Id);
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, null, [], [], idade, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [item], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }
}
