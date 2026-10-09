namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura do <see cref="DefinirDocumentosExigidosCommandHandler"/> (Story #554): a
/// resolução do snapshot-copy de <c>TipoDocumento</c> (Configuração, ADR-0056), do
/// vocabulário de fatos estendido pelo domínio dinâmico da oferta do processo (PR #896), e
/// os erros nomeados que uma resolução malsucedida produz.
/// </summary>
public sealed class DefinirDocumentosExigidosCommandHandlerTests
{
    /// <summary>FormatosPermitidos agora obrigatório (Story #918) — QUALQUER é o valor neutro dos testes que não são sobre formato.</summary>
    private static readonly JsonElement Qualquer = JsonSerializer.SerializeToElement("QUALQUER");

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        ITipoDocumentoReader TipoDocumentoReader,
        IFatoCandidatoReader FatoCandidatoReader,
        IModeloDeDocumentoRepository Modelos,
        ISelecaoUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);

        return new Mocks(
            repository,
            Substitute.For<ITipoDocumentoReader>(),
            Substitute.For<IFatoCandidatoReader>(),
            Substitute.For<IModeloDeDocumentoRepository>(),
            Substitute.For<ISelecaoUnitOfWork>());
    }

    private static Task<Result<MutacaoAceita>> HandleAsync(Mocks mocks, DefinirDocumentosExigidosCommand command) =>
        DefinirDocumentosExigidosCommandHandler.Handle(
            command,
            mocks.Repository,
            mocks.TipoDocumentoReader,
            mocks.FatoCandidatoReader,
            mocks.Modelos,
            mocks.UnitOfWork,
            TimeProvider.System,
            CancellationToken.None);

    private static TipoDocumentoView TipoDocumentoResultado(Guid id) =>
        new(id, "IDENTIDADE", "Documento de identidade", "PESSOAL");

    private static FaseCronograma FaseQualquer() => FaseCronograma.Criar(
        1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    private static FatoCandidatoView FatoSexo() => new(
        Guid.CreateVersion7(), "SEXO", "Sexo", null, "CATEGORICO", "DECLARADO", "ESCALAR",
        ["MASCULINO", "FEMININO", "INTERSEXO"], "INSCRICAO", "CAMPO_FORMULARIO:SEXO", null, "GLOBAL", Ativo: true);

    private static FatoCandidatoView FatoSexoComPontoResolucao(string pontoResolucao) => new(
        Guid.CreateVersion7(), "SEXO", "Sexo", null, "CATEGORICO", "DECLARADO", "ESCALAR",
        ["MASCULINO", "FEMININO", "INTERSEXO"], pontoResolucao, "CAMPO_FORMULARIO:SEXO", null, "GLOBAL", Ativo: true);

    private static FaseCronograma FaseComOrdemECodigo(int ordem, string codigo) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, inicio: null, fim: null,
        produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    private static FatoCandidatoView FatoModalidade() => new(
        Guid.CreateVersion7(), "MODALIDADE", "Modalidade de concorrência", null, "CATEGORICO", "DERIVADO", "MULTIVALORADO", null,
        "INSCRICAO", "REGRA_DERIVACAO:MODALIDADE", null, "MODALIDADE", Ativo: true);

    private static FatoCandidatoView FatoTipoDeficiencia() => new(
        Guid.CreateVersion7(), "TIPO_DEFICIENCIA", "Tipo de deficiência", null, "CATEGORICO", "DECLARADO", "ESCALAR", null,
        "INSCRICAO", "CAMPO_FORMULARIO:TIPO_DEFICIENCIA", null, "PROCESSO", Ativo: true);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
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
                .Should().Be(new ModeloDaExigencia(modelo.Id, "Autodeclaração.odt", FormatoDeModelo.Odt, new string('a', 64)));
        }
    }

    [Fact(DisplayName = "Handle com item válido (sem gatilho) define os documentos exigidos e persiste")]
    public async Task Handle_ItemValido_DefineDocumentosExigidosEPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
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

    [Fact(DisplayName = "Handle com gatilho válido (fato escalar) resolve o vocabulário e persiste a condição")]
    public async Task Handle_GatilhoValidoEscalar_PersisteCondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexo()]));

        ColetarFato(processo, "SEXO", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "SEXO");
    }

    /// <summary>
    /// Um gatilho que cita fato que o processo não resolve nunca se aplica a candidato nenhum:
    /// o fato fica indeterminado, e a exigência, pendente para sempre. Recusar é melhor que
    /// aceitar uma configuração que parece funcionar e não funciona.
    /// </summary>
    [Fact(DisplayName = "Gatilho que cita fato não resolvido pelo processo é recusado")]
    public async Task Handle_GatilhoFatoNaoResolvidoPeloProcesso_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexo()]));

        // O fato existe no catálogo, mas este processo não o coleta.
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    /// <summary>
    /// A faixa etária é calculada pelo sistema da data de nascimento (dependência declarada pelo
    /// mecanismo do derivado): o gatilho que a cita só é aceito quando o processo coleta a data de
    /// nascimento — sem ela, a faixa etária nunca se resolve.
    /// </summary>
    [Theory(DisplayName = "Gatilho por faixa etária é aceito só quando o processo coleta a data de nascimento")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_GatilhoPorFaixaEtaria_DependeDaDataDeNascimento(bool coletaDataDeNascimento)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        if (coletaDataDeNascimento)
        {
            processo.DefinirItens(
                [FatoColetado.Criar("DATA_NASCIMENTO", 0, "Data de nascimento", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null).Value!])
                .IsSuccess.Should().BeTrue();
        }

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoFaixaEtaria()]));

        CondicaoGatilhoInput condicao = new(0, "FAIXA_ETARIA", "MAIOR_IGUAL", "18");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().Be(coletaDataDeNascimento, resultado.Error?.Message);
    }

    /// <summary>A faixa etária como o catálogo a publica: derivada, por atributo do candidato.</summary>
    private static FatoCandidatoView FatoFaixaEtaria() => new(
        Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", null, "NUMERICO", "DERIVADO", "ESCALAR",
        null, "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true);

    /// <summary>
    /// Declara a derivação da modalidade. É o que torna um gatilho por modalidade resolvível:
    /// a modalidade de um candidato é calculada das respostas dele, não perguntada.
    /// </summary>
    private static void DeclararDerivacaoDeModalidade(ProcessoSeletivo processo)
    {
        // Uma regra âncora incondicional basta: o que este helper precisa é que o processo
        // DECLARE a derivação, não que ela cubra o quadro de vagas inteiro.
        RegraDerivacaoConfigurada regra = RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!;
        ConfiguracaoDerivacaoFato derivacao = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [regra]).Value!;
        processo.DefinirRegrasDerivacao([derivacao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Faz o processo coletar o fato, que é o que torna um gatilho sobre ele resolvível.
    ///
    /// Sem isto, a exigência condicionada nunca se aplicaria a candidato nenhum — o fato
    /// ficaria indeterminado para sempre, e a exigência pendente. O comando recusa por isso.
    /// </summary>
    private static void ColetarFato(ProcessoSeletivo processo, string codigo, TipoRenderizacao renderizacao = TipoRenderizacao.SelecaoUnica)
    {
        FatoColetado fato = FatoColetado.Criar(codigo, 0, codigo, renderizacao, Obrigatoriedade.Sempre, null).Value!;
        processo.DefinirItens([fato], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Handle com gatilho de fato desconhecido retorna PredicadoDnf.FatoDesconhecido")]
    public async Task Handle_GatilhoFatoDesconhecido_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[]));

        CondicaoGatilhoInput condicao = new(0, "FATO_INEXISTENTE", "IGUAL", "\"X\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "CA-03: gatilho por MODALIDADE não ofertada pelo processo é recusado (integridade referencial)")]
    public async Task Handle_GatilhoModalidadeNaoOfertada_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Processo NÃO oferece nenhuma modalidade — DistribuicaoVagas vazia.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoModalidade()]));

        // A modalidade é fato derivado POR REGRA: sem regra declarada, o processo não a resolve
        // e a recusa seria outra. Declarar a derivação é o que deixa este teste chegar à
        // conferência que ele de fato mede — o valor contra o domínio ofertado.
        DeclararDerivacaoDeModalidade(processo);
        CondicaoGatilhoInput condicao = new(0, "MODALIDADE", "IGUAL", "\"LB_PPI\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    // ── Repetição por entidade pelo grupo repetível do formulário (ADR-0138) ──────────────

    /// <summary>Processo com o grupo da composição familiar no formulário, e o catálogo com o campo de membro.</summary>
    private static (ProcessoSeletivo Processo, FaseCronograma Fase, Mocks Mocks, Guid TipoDocumentoId) ProcessoComComposicaoFamiliar()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        GrupoColetado composicao = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", 1, 10, null, Obrigatoriedade.Sempre,
            [FatoColetado.Criar("MAIOR_IDADE", 0, "Maior de idade", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!]).Value!;
        processo.DefinirItens([], grupos: [composicao]).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)
        [
            new FatoCandidatoView(Guid.CreateVersion7(), "MAIOR_IDADE", "Maior de idade", null, "BOOLEANO", "DECLARADO", "ESCALAR",
                null, "INSCRICAO", "CAMPO_FORMULARIO:MAIOR_IDADE", null, null, Ativo: true, Escopo: "MEMBRO_GRUPO"),
        ]));
        return (processo, fase, mocks, tipoDocumentoId);
    }

    [Theory(DisplayName = "O gatilho da folha repetida cita o campo do grupo, marcada na folha ou herdada do grupo ancestral")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_GatilhoPorCampoDoGrupo_AceitoNaSubarvoreRepetida(bool herdadaDoAncestral)
    {
        (ProcessoSeletivo processo, FaseCronograma fase, Mocks mocks, Guid tipoDocumentoId) = ProcessoComComposicaoFamiliar();
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "CONDICIONAL", false, "ELIMINA", [new CondicaoGatilhoInput(0, "MAIOR_IDADE", "IGUAL", "true")], [], null, Qualquer, null);
        NoExigenciaInput raiz = herdadaDoAncestral
            ? new("E", null, null, null, null, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], RepetePorEntidade: "COMPOSICAO_FAMILIAR")
            : new("FOLHA", item, null, null, null, null, RepetePorEntidade: "COMPOSICAO_FAMILIAR");

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(processo.Id, [raiz], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Condicoes.Any(c => c.Fato == "MAIOR_IDADE"));
    }

    [Fact(DisplayName = "Fora da subárvore repetida, o campo do grupo não é citável")]
    public async Task Handle_CampoDoGrupoForaDaRepeticao_Recusa()
    {
        (ProcessoSeletivo processo, FaseCronograma fase, Mocks mocks, Guid tipoDocumentoId) = ProcessoComComposicaoFamiliar();
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "CONDICIONAL", false, "ELIMINA", [new CondicaoGatilhoInput(0, "MAIOR_IDADE", "IGUAL", "true")], [], null, Qualquer, null);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(
            processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    [Fact(DisplayName = "Repetir por grupo inexistente é recusado pelo grupo, antes do gatilho que cita o campo dele")]
    public async Task Handle_RepeticaoPorGrupoInexistente_RecusaPeloGrupo()
    {
        (ProcessoSeletivo processo, FaseCronograma fase, Mocks mocks, Guid tipoDocumentoId) = ProcessoComComposicaoFamiliar();
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "CONDICIONAL", false, "ELIMINA", [new CondicaoGatilhoInput(0, "MAIOR_IDADE", "IGUAL", "true")], [], null, Qualquer, null);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(
            processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null, RepetePorEntidade: "PESSOAS_JURIDICAS")], PrecondicaoIfMatch.Ausente));

        resultado.Error!.Code.Should().Be("NoExigencia.TipoEntidadeInvalido");
    }

    [Fact(DisplayName = "Story #917: TIPO_DEFICIENCIA participa do domínio dinâmico igual a MODALIDADE/CONDICAO_ATENDIMENTO")]
    public async Task Handle_GatilhoTipoDeficienciaOfertado_PersisteCondicao()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaTipoDeficiencia tipoTea = OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "TEA", "TEA");
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar([condicaoPcd], [], [tipoTea]).Value!;
        processo.DefinirOfertaAtendimento(oferta, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        ColetarFato(processo, "TIPO_DEFICIENCIA", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.Condicoes.Should().ContainSingle(c => c.Fato == "TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA usa o código do tipo, não o nome de exibição")]
    public async Task Handle_GatilhoTipoDeficiencia_UsaCodigoNaoNome()
    {
        // O domínio dinâmico de TIPO_DEFICIENCIA era montado a partir do NOME do tipo
        // ofertado, enquanto o irmão CONDICAO_ATENDIMENTO já usava o código. A
        // consequência: uma condição escrita com o código canônico era recusada como
        // valor fora do domínio, e renomear o rótulo invalidava condições existentes.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Código e nome deliberadamente diferentes: é o que separa os dois regimes.
        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaTipoDeficiencia tipo = OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual");
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar([condicaoPcd], [], [tipo]).Value!;
        processo.DefinirOfertaAtendimento(oferta, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        ColetarFato(processo, "TIPO_DEFICIENCIA", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput porCodigo = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"DEFICIENCIA_VISUAL\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [porCodigo], [], null, Qualquer, null);

        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue(
            "o código do tipo ofertado é o valor de domínio — antes só o nome era aceito");
    }

    [Fact(DisplayName = "Gatilho por TIPO_DEFICIENCIA com o nome de exibição é recusado")]
    public async Task Handle_GatilhoTipoDeficienciaPeloNome_Recusa()
    {
        // A contraprova: se o domínio ainda viesse do nome, este cenário passaria.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaTipoDeficiencia tipo = OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "DEFICIENCIA_VISUAL", "Deficiência visual");
        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar([condicaoPcd], [], [tipo]).Value!;
        processo.DefinirOfertaAtendimento(oferta, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        CondicaoGatilhoInput porNome = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"Deficiência visual\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [porNome], [], null, Qualquer, null);

        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente));

        resultado.IsFailure.Should().BeTrue("o nome é rótulo de exibição, não valor de domínio");
    }

    [Fact(DisplayName = "Story #917/CA-03: gatilho por TIPO_DEFICIENCIA não ofertado pelo processo é recusado (integridade referencial)")]
    public async Task Handle_GatilhoTipoDeficienciaNaoOfertado_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // Processo NÃO oferece atendimento especializado — OfertaAtendimento nulo.

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoTipoDeficiencia()]));

        ColetarFato(processo, "TIPO_DEFICIENCIA", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"");
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    [Fact(DisplayName = "CA-07 (Story #554, PR #898): mesma referência documental em exigências distintas preserva bases legais distintas, correlacionadas por identidade")]
    public async Task Handle_MesmaReferenciaDocumental_BasesDistintasPorExigencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
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

    [Fact(DisplayName = "ADR-0125: base legal inválida numa fase profunda da árvore recusa ANTES de consultar TipoDocumento/vocabulário de fatos")]
    public async Task Handle_BaseLegalInvalidaEmFolhaProfunda_RecusaSemConsultarReaders()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        // TipoDocumentoReader/FatoCandidatoReader propositalmente NÃO configurados — se a
        // travessia pura perdesse para o I/O, o handler tentaria resolvê-los e devolveria
        // um erro de I/O em vez do erro de forma da base legal.
        Guid tipoDocumentoId = Guid.CreateVersion7();

        BaseLegalInput baseInvalida = new("", "FEDERAL", "RESOLVIDO", null);
        ItemDocumentoExigidoInput folha = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [baseInvalida], null, Qualquer, null);
        NoExigenciaInput noFolha = new("FOLHA", folha, null, null, null, null);
        // O grupo (nó raiz) é visitado ANTES da folha (pré-ordem) — mas não tem bases
        // legais próprias, então a travessia recursa até achar a violação na folha.
        NoExigenciaInput grupo = new("OU", null, 1, "ELIMINA", [], [noFolha]);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [grupo], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigidoBaseLegal.ReferenciaObrigatoria"]);
        await mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "ADR-0125: aplicabilidade Nenhuma numa fase profunda da árvore recusa ANTES de consultar TipoDocumento/vocabulário de fatos")]
    public async Task Handle_AplicabilidadeInvalidaEmFolhaProfunda_RecusaSemConsultarReaders()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        // TipoDocumentoReader/FatoCandidatoReader propositalmente NÃO configurados.
        Guid tipoDocumentoId = Guid.CreateVersion7();

        ItemDocumentoExigidoInput folha = new(fase.Id, tipoDocumentoId, "INVALIDA", true, null, [], [], null, Qualquer, null);
        NoExigenciaInput noFolha = new("FOLHA", folha, null, null, null, null);
        NoExigenciaInput grupo = new("OU", null, 1, "ELIMINA", [], [noFolha]);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [grupo], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigido.AplicabilidadeObrigatoria"]);
        await mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "O formulário do documento fora do vocabulário das finalidades é recusado no campo dele, antes de consultar TipoDocumento")]
    public async Task Handle_FinalidadeForaDoVocabulario_RecusaNoCampo()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        Mocks mocks = NovosMocks(processo, processo.Id);

        ItemDocumentoExigidoInput folha = new(fase.Id, Guid.CreateVersion7(), "GERAL", true, null, [], [], null, Qualquer, null, Finalidade: "MATRICULA");
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", folha, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo([("finalidade", EstruturaFormularioErrorCodes.FinalidadeInvalida)]);
        await mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "ADR-0125: duas bases legais inválidas na mesma folha acumulam, com o índice prefixado ao field")]
    public async Task Handle_DuasBasesLegaisInvalidasNaMesmaFolha_AcumulaComIndicePrefixado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        BaseLegalInput baseSemReferencia = new("", "FEDERAL", "RESOLVIDO", null);
        BaseLegalInput baseSemAbrangencia = new("Lei X", "", "RESOLVIDO", null);
        ItemDocumentoExigidoInput exigencia = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [baseSemReferencia, baseSemAbrangencia], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(
            processo.Id, [new NoExigenciaInput("FOLHA", exigencia, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["basesLegais[0].referencia", "basesLegais[1].abrangencia"]);
    }

    [Fact(DisplayName = "Story #918: reenviar o PUT com FormatosPermitidos/TamanhoMaximoBytes diferentes substitui integralmente (não faz merge)")]
    public async Task Handle_ReenviarComFormatoETamanhoDiferentes_SubstituiIntegralmente()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement listaPdf = JsonSerializer.SerializeToElement(new[] { "PDF" });
        ItemDocumentoExigidoInput primeiro = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, listaPdf, 5_000_000);
        (await HandleAsync(mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", primeiro, null, null, null, null)], PrecondicaoIfMatch.Ausente)))
            .IsSuccess.Should().BeTrue();
        processo.DocumentosExigidos.Single().FormatosPermitidos.Qualquer.Should().BeFalse();
        processo.DocumentosExigidos.Single().FormatosPermitidos.Lista!.Single().Formato.Should().Be(FormatoPermitido.Pdf);
        processo.DocumentosExigidos.Single().TamanhoMaximoBytes.Should().Be(5_000_000);

        JsonElement listaJpeg = JsonSerializer.SerializeToElement(new[] { "JPEG" });
        ItemDocumentoExigidoInput segundo = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, listaJpeg, 2_000_000);
        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, new DefinirDocumentosExigidosCommand(processo.Id, [new NoExigenciaInput("FOLHA", segundo, null, null, null, null)], PrecondicaoIfMatch.Curinga));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.FormatosPermitidos.Lista!.Single().Formato.Should().Be(
            FormatoPermitido.Jpeg, "substituição integral, sem vestígio do valor anterior");
        exigencia.TamanhoMaximoBytes.Should().Be(2_000_000);
    }

    // ── Story #918 — wire polimórfico de FormatosPermitidos ──

    [Fact(DisplayName = "FormatosPermitidos ausente (JsonElement? null) retorna FormatosPermitidos.Obrigatorio")]
    public async Task Handle_FormatosPermitidosAusente_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, null, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Theory(DisplayName = "FormatosPermitidos em forma inválida (ValueKind inesperado ou string diferente de QUALQUER) retorna FormatosPermitidos.FormaInvalida")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"PDF\"")]
    [InlineData("{}")]
    public async Task Handle_FormatosPermitidosFormaInvalida_RetornaErroNomeado(string valorJson)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement valor = JsonDocument.Parse(valorJson).RootElement;
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, valor, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormaInvalida");
    }

    [Fact(DisplayName = "FormatosPermitidos = QUALQUER é aceito")]
    public async Task Handle_FormatosPermitidosQualquer_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
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
        processo.DocumentosExigidos.Single().FormatosPermitidos.Qualquer.Should().BeTrue();
    }

    [Fact(DisplayName = "FormatosPermitidos com lista {PDF,JPEG,PNG} (itens-texto simples) congela os 3 formatos, sem teto por formato")]
    public async Task Handle_FormatosPermitidosListaDeStrings_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement lista = JsonSerializer.SerializeToElement(new[] { "PDF", "JPEG", "PNG" });
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatosPermitidos formatosPermitidos = processo.DocumentosExigidos.Single().FormatosPermitidos;
        formatosPermitidos.Qualquer.Should().BeFalse();
        formatosPermitidos.Lista!.Select(e => e.Formato).Should().BeEquivalentTo(
            [FormatoPermitido.Pdf, FormatoPermitido.Jpeg, FormatoPermitido.Png]);
        formatosPermitidos.Lista!.Should().AllSatisfy(e => e.TamanhoMaximoBytesMax.Should().BeNull());
    }

    [Fact(DisplayName = "FormatosPermitidos com item {formato, tamanhoMaximoBytesMax} congela o teto por formato")]
    public async Task Handle_FormatosPermitidosComTetoPorFormato_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement lista = JsonSerializer.SerializeToElement(new[]
        {
            new { formato = "PDF", tamanhoMaximoBytesMax = 5_000_000 },
        });
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatoPermitidoEntry entrada = processo.DocumentosExigidos.Single().FormatosPermitidos.Lista!.Single();
        entrada.Formato.Should().Be(FormatoPermitido.Pdf);
        entrada.TamanhoMaximoBytesMax.Should().Be(5_000_000);
    }

    [Theory(DisplayName = "tamanhoMaximoBytesMax presente e malformado é recusado com erro nomeado, nunca 500")]
    [InlineData("\"5MB\"")]
    [InlineData("true")]
    [InlineData("5000000.5")]
    [InlineData("99999999999999999999")]
    public async Task Handle_FormatosPermitidosTamanhoMaximoBytesMaxMalformado_RetornaErroNomeado(string tamanhoJson)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        JsonElement lista = JsonDocument.Parse($$"""[{"formato": "PDF", "tamanhoMaximoBytesMax": {{tamanhoJson}}}]""").RootElement;
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormaInvalida");
    }

    [Fact(DisplayName = "Um array contendo a string \"QUALQUER\" NÃO é o token especial — é lista com um formato desconhecido")]
    public async Task Handle_FormatosPermitidosQualquerComLista_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        // "QUALQUER" só é reconhecido como o token especial quando a forma é STRING — um
        // array contendo a string "QUALQUER" é só mais um item de lista (um formato
        // desconhecido, na prática), não o token. O cenário de exclusividade mútua
        // (FormatosPermitidos.QualquerComFormatosEspecificos) é coberto diretamente no VO
        // (FormatosPermitidosTests) — o handler não tem como produzir os dois braços ao
        // mesmo tempo a partir de um único JsonElement.
        JsonElement lista = JsonSerializer.SerializeToElement(new[] { "QUALQUER" });
        ItemDocumentoExigidoInput item = new(fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], null, lista, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormatoInvalido");
    }

    [Fact(DisplayName = "CA-08: âncora de fase de IdadeMaximaEmissao que não pertence ao processo é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseDeOutroProcesso_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, Guid.CreateVersion7());
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], idade, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "CA-08: âncora de fase de IdadeMaximaEmissao sem o extremo definido é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseSemExtremo_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseQualquer();
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));

        // FaseQualquer() não tem Inicio/Fim definidos — FIM_FASE não tem extremo a apontar.
        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, fase.Id);
        ItemDocumentoExigidoInput item = new(
            fase.Id, tipoDocumentoId, "GERAL", true, null, [], [], idade, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    // ── Story #916 — gate de fase ──

    [Fact(DisplayName = "Gate de fase: condição sobre fato cujo PontoResolucao é uma fase POSTERIOR à da exigência é recusada")]
    public async Task Handle_GatilhoFatoDeFasePosterior_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma inscricao = FaseComOrdemECodigo(1, "INSCRICAO");
        FaseCronograma homologacao = FaseComOrdemECodigo(2, "HOMOLOGACAO");
        processo.DefinirCronogramaFases([inscricao, homologacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        // SEXO só é conhecido na fase HOMOLOGACAO (ordem 2), mas o documento é exigido na
        // fase INSCRICAO (ordem 1) — anterior. O gatilho nunca teria como já ter sido
        // resolvido para o candidato.
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexoComPontoResolucao("HOMOLOGACAO")]));

        ColetarFato(processo, "SEXO", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(inscricao.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.FatoResolvidoEmFasePosterior");
    }

    [Theory(DisplayName = "Gate de fase: condição sobre fato cujo PontoResolucao é a mesma fase ou uma fase ANTERIOR à da exigência é aceita")]
    [InlineData("INSCRICAO")]
    [InlineData("HOMOLOGACAO")]
    public async Task Handle_GatilhoFatoDeFaseIgualOuAnterior_Aceita(string pontoResolucao)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma inscricao = FaseComOrdemECodigo(1, "INSCRICAO");
        FaseCronograma homologacao = FaseComOrdemECodigo(2, "HOMOLOGACAO");
        processo.DefinirCronogramaFases([inscricao, homologacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexoComPontoResolucao(pontoResolucao)]));

        // O documento é exigido na fase HOMOLOGACAO (ordem 2) — SEXO conhecido na própria
        // fase ou numa fase anterior (INSCRICAO, ordem 1) satisfaz o gate.
        ColetarFato(processo, "SEXO", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(homologacao.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Gate de fase: PontoResolucao do fato citado não pertence ao cronograma deste processo é recusado")]
    public async Task Handle_GatilhoComPontoResolucaoForaDoCronograma_RetornaErroNomeado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Handler", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma inscricao = FaseComOrdemECodigo(1, "INSCRICAO");
        processo.DefinirCronogramaFases([inscricao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        // SEXO resolve numa fase ("HOMOLOGACAO") que não existe no cronograma deste processo.
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoSexoComPontoResolucao("HOMOLOGACAO")]));

        ColetarFato(processo, "SEXO", TipoRenderizacao.SelecaoUnica);
        CondicaoGatilhoInput condicao = new(0, "SEXO", "IGUAL", "\"MASCULINO\"");
        ItemDocumentoExigidoInput item = new(inscricao.Id, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null);
        DefinirDocumentosExigidosCommand command = new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.PontoResolucaoForaDoCronograma");
    }

    // ── Grupo da convocação (UNI-REQ-0144): os documentos da habilitação seguem o grupo em que o
    // candidato foi convocado, e não todas as modalidades a que concorreu ──

    [Fact(DisplayName = "Exigência da habilitação cita o grupo em que o candidato foi convocado")]
    public async Task Handle_GatilhoPeloGrupoDaConvocacaoNaHabilitacao_Aceita()
    {
        (ProcessoSeletivo processo, Mocks mocks, Guid tipoDocumentoId) = ProcessoComAmplaConcorrencia(
            FaseComOrdemECodigo(1, "RESULTADO_FINAL"), FaseComOrdemECodigo(2, "HABILITACAO"));
        Guid habilitacao = processo.CronogramaFases.Single(f => f.Codigo == "HABILITACAO").Id;

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, ExigenciaPeloGrupoDaConvocacao(processo, habilitacao, tipoDocumentoId));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Sem a fase do resultado final no cronograma, o grupo da convocação não é citado")]
    public async Task Handle_GatilhoPeloGrupoDaConvocacaoSemResultadoFinal_Recusa()
    {
        (ProcessoSeletivo processo, Mocks mocks, Guid tipoDocumentoId) = ProcessoComAmplaConcorrencia(FaseComOrdemECodigo(1, "HABILITACAO"));

        Result<MutacaoAceita> resultado = await HandleAsync(
            mocks, ExigenciaPeloGrupoDaConvocacao(processo, processo.CronogramaFases.Single().Id, tipoDocumentoId));

        resultado.Error!.Code.Should().Be("DocumentoExigido.PontoResolucaoForaDoCronograma");
    }

    private static (ProcessoSeletivo Processo, Mocks Mocks, Guid TipoDocumentoId) ProcessoComAmplaConcorrencia(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Convocação", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirDistribuicaoVagas([ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Mocks mocks = NovosMocks(processo, processo.Id);
        Guid tipoDocumentoId = Guid.CreateVersion7();
        mocks.TipoDocumentoReader.ObterPorIdAsync(tipoDocumentoId, Arg.Any<CancellationToken>())
            .Returns(TipoDocumentoResultado(tipoDocumentoId));
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com(CadastrosVivos.FatosDeModalidade()));
        return (processo, mocks, tipoDocumentoId);
    }

    private static DefinirDocumentosExigidosCommand ExigenciaPeloGrupoDaConvocacao(ProcessoSeletivo processo, Guid faseId, Guid tipoDocumentoId)
    {
        CondicaoGatilhoInput condicao = new(0, "MODALIDADE_CONVOCACAO", "IGUAL", "\"AC\"");
        ItemDocumentoExigidoInput item = new(faseId, tipoDocumentoId, "CONDICIONAL", true, null, [condicao], [], null, Qualquer, null, Finalidade: "HABILITACAO");
        return new(processo.Id, [new NoExigenciaInput("FOLHA", item, null, null, null, null)], PrecondicaoIfMatch.Ausente);
    }
}
