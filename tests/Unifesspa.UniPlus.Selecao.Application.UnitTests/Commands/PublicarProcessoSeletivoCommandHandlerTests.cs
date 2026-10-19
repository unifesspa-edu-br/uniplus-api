namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura do congelamento de metadado de fato (Story #919, RN08) em
/// <see cref="PublicarProcessoSeletivoCommandHandler"/>: o resultado da resolução alimenta
/// <see cref="EntradaCanonicalizacao.MetadadosFatosCongelados"/>, e um código de fato que não
/// resolve no catálogo aborta a publicação com um erro nomeado ANTES de canonicalizar.
/// </summary>
/// <remarks>
/// Desde a issue #1059 (D4-bis), <see cref="IFatoCandidatoReader.ListarAsync"/> é consultado
/// UMA vez por congelamento, sempre — mesmo sem condição de gatilho — porque o gate de valor
/// inativo do domínio precisa do catálogo inteiro independente de o processo ter fato coletado
/// ou gatilho. É esse catálogo compartilhado, não mais <c>ObterPorCodigoAsync</c> por código,
/// que <see cref="ResolvedorMetadadosFatosCongelados"/> agora consulta.
/// </remarks>
public sealed class PublicarProcessoSeletivoCommandHandlerTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ProcessoSeletivo NovoProcessoConforme(out Guid faseId) =>
        ProcessoSeletivoConformeBuilder.Criar("PS Metadado de Fato", out faseId);

    private static DocumentoExigido ExigenciaComGatilhoPorFato(Guid faseId, string fato) =>
        DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO",
            tipoDocumentoNome: "Declaração",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!],
            basesLegais: [DocumentoExigidoBaseLegal.Criar(
                "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static FatoCandidatoView FatoModalidade() => new(
        Id: Guid.CreateVersion7(),
        Codigo: "MODALIDADE",
        Nome: "Modalidade de concorrência",
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DERIVADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: "REGRA_DERIVACAO:MODALIDADE",
        ValoresDominioDeclarados: null, FonteValores: "MODALIDADE", Ativo: true, ClassificacaoProtecao: "PESSOAL");

    private sealed record Mocks(
        IProcessoSeletivoRepository ProcessoRepository,
        IDocumentoAPublicarRepository DocumentoRepository,
        ISnapshotPublicacaoCanonicalizer Canonicalizer,
        ITipoAtoPublicadoReader TipoDeAtoReader,
        IVagaDeLinhagemReader VagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository ObrigatoriedadeLegalRepository,
        IFatoCandidatoReader FatoCandidatoReader);

    private static (Mocks Mocks, DocumentoAPublicar Documento) NovosMocks(ProcessoSeletivo processo, Action<EntradaCanonicalizacao>? captura = null)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoAPublicarRepository documentoRepository = Substitute.For<IDocumentoAPublicarRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);

        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.3", "canonical-json/sha256@v1"));

        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>().ComQualquerTipoDeclarado();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura",
                CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));

        // O formulário de inscrição coleta o conjunto básico, que o catálogo conhece por padrão.
        IFatoCandidatoReader fatoCandidatoReader = Substitute.For<IFatoCandidatoReader>();
        fatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Fatos);

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

        return (new Mocks(
            processoRepository,
            documentoRepository,
            canonicalizer,
            tipoDeAtoReader,
            Substitute.For<IVagaDeLinhagemReader>(),
            obrigatoriedadeLegalRepository,
            fatoCandidatoReader), documento);
    }

    /// <summary>
    /// A leitura do calendário vigente é <b>uma por handler</b>: a mesma resposta alimenta o gate
    /// da raiz e o bloco congelado do envelope. Duas leituras abririam a janela em que o dataset
    /// muda entre validar e congelar, e a versão publicada carregaria um calendário que o gate
    /// não aprovou — defeito que nenhuma asserção sobre o resultado pegaria, porque num teste
    /// comum as duas leituras devolveriam o mesmo valor.
    /// </summary>
    [Fact(DisplayName = "O handler lê o calendário vigente uma única vez")]
    public async Task Handle_LeCalendarioUmaUnicaVez()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        var reader = CalendarioVigenteReaderDeTeste.ComVigente();

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento, reader);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        reader.Leituras.Should().Be(1);
    }

    [Theory(DisplayName = "O documento do edital que não é PDF é recusado antes de qualquer escrita")]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task Handle_DocumentoDoEditalNaoEPdf_Recusa(string tipoDeMidia)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, _) = NovosMocks(processo);
        DocumentoAPublicar naoPdf = DocumentoAPublicar.IniciarPendente(
            processo.Id, FinalidadeDoDocumento.AnexoDoEdital, "edital", tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        naoPdf.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        mocks.DocumentoRepository.ObterPorIdAsync(naoPdf.Id, Arg.Any<CancellationToken>()).Returns(naoPdf);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(mocks, processo, naoPdf);

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.DocumentoNaoEnviadoComoEdital");
        eventos.Should().BeEmpty();
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoAPublicar documento,
        ICalendarioVigenteReader? calendarioReader = null,
        ISelecaoUnitOfWork? unitOfWork = null)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("user-sub-1");

        return PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", null, null,
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA")),
            mocks.ProcessoRepository,
            mocks.DocumentoRepository,
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            Substitute.For<IAtoDoProcessoRepository>(),
            mocks.Canonicalizer, new ResolvedorFusoDeTeste(),
            unitOfWork ?? Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            mocks.TipoDeAtoReader,
            mocks.VagaDeLinhagemReader,
            mocks.ObrigatoriedadeLegalRepository,
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            mocks.FatoCandidatoReader,
            calendarioReader ?? CalendarioVigenteReaderDeTeste.SemVigente(),
            TimeProvider.System,
            CancellationToken.None);
    }

    [Fact(DisplayName = "Fato reclassificado de PESSOAL para IDENTIFICADOR no catálogo publica como IDENTIFICADOR e a resposta é cifrada")]
    public async Task Handle_FatoReclassificadoNoCatalogo_PublicaComAClassificacaoViva()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        FatoColetado cpfDoCatalogoAntigo = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF");
        FatoColetado cpf = FatoColetado.Criar(
            "CPF", cpfDoCatalogoAntigo.Ordem, cpfDoCatalogoAntigo.Rotulo, cpfDoCatalogoAntigo.TipoRenderizacao, cpfDoCatalogoAntigo.Obrigatoriedade, null, "PESSOAL",
            cpfDoCatalogoAntigo.OrigemValores, cpfDoCatalogoAntigo.EtapaCodigo, formato: cpfDoCatalogoAntigo.Formato).Value!;
        processo.DefinirItens([.. processo.FatosColetados.Select(f => f.FatoCodigo == "CPF" ? cpf : f)]).IsSuccess.Should().BeTrue();
        DefinicaoDaResposta.De(cpf.TipoRenderizacao, cpf.ClassificacaoProtecao, fatoCodigo: "CPF").Cifrada.Should().BeFalse("a cópia velha não cifra");

        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cpf.ClassificacaoProtecao.Should().Be("IDENTIFICADOR");
        DefinicaoDaResposta.De(cpf.TipoRenderizacao, cpf.ClassificacaoProtecao, fatoCodigo: "CPF").Cifrada.Should().BeTrue();
    }

    private static void CatalogoComCorRacaDoProcesso(Mocks mocks) =>
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([
            .. CatalogoDoConjuntoBasico.Fatos.Where(static f => f.Codigo != "COR_RACA"),
            CatalogoDoConjuntoBasico.Fatos.Single(static f => f.Codigo == "COR_RACA") with { FonteValores = "PROCESSO" },
        ]);

    [Fact(DisplayName = "A cópia recopiada do catálogo é a que os gates avaliam: fonte passada a PROCESSO recusa pelo gate de oferta")]
    public async Task Handle_FonteDeValoresMudaNoCatalogo_OsGatesAvaliamACopiaNova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        CatalogoComCorRacaDoProcesso(mocks);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
        resposta.Error.Message.Should().Contain("não declara nenhuma opção");
        mocks.Canonicalizer.DidNotReceiveWithAnyArgs().Canonicalizar(default!);
    }

    [Fact(DisplayName = "A publicação recusada por gate posterior à recópia descarta o que a recópia mudou")]
    public async Task Handle_RecusaPosteriorARecopia_DescartaAsAlteracoes()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        CatalogoComCorRacaDoProcesso(mocks);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento, unitOfWork: unitOfWork);

        resposta.IsFailure.Should().BeTrue();
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await unitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Booleano que vira categórico no catálogo recusa a publicação do item booleano, nomeando o campo")]
    public async Task Handle_BooleanoQueViraCategorico_RecusaOItem()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        FatoColetado item = processo.FatosColetados.Single(static f => f.FatoCodigo == "DESEJA_NOME_SOCIAL");
        item.TipoRenderizacao.Should().Be(TipoRenderizacao.Booleano);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([
            .. CatalogoDoConjuntoBasico.Fatos.Where(static f => f.Codigo != "DESEJA_NOME_SOCIAL"),
            CatalogoDoConjuntoBasico.Fatos.Single(static f => f.Codigo == "DESEJA_NOME_SOCIAL") with { Dominio = "CATEGORICO" },
        ]);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
        resposta.Error.Message.Should().Contain(item.Rotulo).And.Contain("Regrave o item");
        mocks.Canonicalizer.DidNotReceiveWithAnyArgs().Canonicalizar(default!);
    }

    [Fact(DisplayName = "Sem condição de gatilho, MetadadosFatosCongelados é null mesmo com o catálogo consultado")]
    public async Task Handle_SemCondicaoDeGatilho_MetadadosENulo()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo, e => entradaCapturada = e);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.MetadadosFatosCongelados.Should().BeNull(
            "nenhuma condição de gatilho existe no processo — nada a congelar, mesmo o catálogo tendo sido lido " +
            "para o gate de valor inativo (D4-bis)");
        _ = await mocks.FatoCandidatoReader.Received(1).ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com condição de gatilho resolvida, MetadadosFatosCongelados carrega o metadado do fato citado")]
    public async Task Handle_ComCondicaoDeGatilhoResolvida_ResolveMetadadoDoFato()
    {
        // A modalidade do candidato vem da ampla concorrência que o processo oferta.
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "MODALIDADE"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoModalidade() with { PontoResolucao = "RESULTADO_FINAL" }]));

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.MetadadosFatosCongelados.Should().NotBeNull();
        entradaCapturada.MetadadosFatosCongelados.Should().ContainKey("MODALIDADE");
        MetadadoFatoCongelado metadado = entradaCapturada.MetadadosFatosCongelados!["MODALIDADE"];
        metadado.Dominio.Should().Be("CATEGORICO");
        metadado.Origem.Should().Be("DERIVADO");
        metadado.Cardinalidade.Should().Be("MULTIVALORADO");
        metadado.PontoResolucao.Should().Be("RESULTADO_FINAL");
        metadado.Binding.Should().Be("REGRA_DERIVACAO:MODALIDADE");
        metadado.ValoresDominioDeclarados.Should().BeNull("MODALIDADE é escopo-processo — os valores vêm da oferta do processo, não de FatoValorDominio");
    }

    [Fact(DisplayName = "Gatilho que cita fato que o processo deixou de derivar recusa a publicação")]
    public async Task Handle_GatilhoCitaFatoForaDoProcesso_Recusa()
    {
        const string derivado = "REGIME_DE_VAGA";
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirRegrasDerivacao(
            [ConfiguracaoDerivacaoFato.Criar(derivado, [RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!]).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, derivado), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[
                FatoModalidade() with { Codigo = derivado, Binding = $"REGRA_DERIVACAO:{derivado}", PontoResolucao = "RESULTADO_FINAL" }]));

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
        processo.Status.Should().Be(StatusProcesso.Rascunho);
    }

    [Fact(DisplayName = "Derivado citado por gatilho herda a fase da dependência que o gatilho não cita")]
    public async Task Handle_DerivadoComDependenciaForaDoCronograma_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirItens(
            [FatoColetado.Criar("TEM_RENDA", 0, "Tem renda?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL").Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(
            [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(PredicadoDnf.CriarDeCondicoesAgrupadas(
                [(0, CondicaoDnf.Criar("TEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!)],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "MODALIDADE"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        FatoCandidatoView temRenda = FatoModalidade() with
        {
            Codigo = "TEM_RENDA",
            Dominio = "BOOLEANO",
            Origem = "DECLARADO",
            Cardinalidade = "ESCALAR",
            PontoResolucao = "HABILITACAO",
            Binding = "CAMPO_FORMULARIO:TEM_RENDA",
            FonteValores = null,
        };
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoModalidade() with { PontoResolucao = "RESULTADO_FINAL" }, temRenda]));

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma);
    }

    [Fact(DisplayName = "Código de fato que não resolve no catálogo aborta a publicação ANTES de canonicalizar")]
    public async Task Handle_CodigoDeFatoNaoResolve_AbortaAntesDeCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "FATO_INEXISTENTE"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        // O catálogo (D4-bis) não conhece "FATO_INEXISTENTE": NovosMocks traz só o conjunto básico.
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(mocks, processo, documento);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoCongeladoNaoEncontrado");
        _ = mocks.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }
}
