namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
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

public sealed partial class PublicarProcessoSeletivoCommandHandlerTests
{
    [Fact(DisplayName = "Fato reclassificado de PESSOAL para IDENTIFICADOR no catálogo publica como IDENTIFICADOR e a resposta é cifrada")]
    public async Task Handle_FatoReclassificadoNoCatalogo_PublicaComAClassificacaoViva()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        FatoColetado cpf = ReclassificarCpfComoPessoal(processo);

        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cpf.ClassificacaoProtecao.Should().Be("IDENTIFICADOR");
        DefinicaoDaResposta.De(cpf.TipoRenderizacao, cpf.ClassificacaoProtecao, fatoCodigo: "CPF").Cifrada.Should().BeTrue();
    }

    private static FatoColetado ReclassificarCpfComoPessoal(ProcessoSeletivo processo)
    {
        FatoColetado cpfDoCatalogoAntigo = processo.FatosColetados.Single(static f => f.FatoCodigo == "CPF");
        FatoColetado cpf = FatoColetado.Criar(
            "CPF", cpfDoCatalogoAntigo.Ordem, cpfDoCatalogoAntigo.Rotulo, cpfDoCatalogoAntigo.TipoRenderizacao, cpfDoCatalogoAntigo.Obrigatoriedade, null, "PESSOAL", ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, ProtecaoDoFatoDeTeste.Descricao,
            cpfDoCatalogoAntigo.OrigemValores, cpfDoCatalogoAntigo.EtapaCodigo, formato: cpfDoCatalogoAntigo.Formato).Value!;
        processo.DefinirItens([.. processo.FatosColetados.Select(f => f.FatoCodigo == "CPF" ? cpf : f)]).IsSuccess.Should().BeTrue();
        DefinicaoDaResposta.De(cpf.TipoRenderizacao, cpf.ClassificacaoProtecao, fatoCodigo: "CPF").Cifrada.Should().BeFalse("a cópia velha não cifra");
        return cpf;
    }

    private static void CatalogoComCorRacaDoProcesso(Mocks mocks) =>
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([
            .. CatalogoDoConjuntoBasico.Fatos.Where(static f => f.Codigo != "COR_RACA"),
            CatalogoDoConjuntoBasico.Fatos.Single(static f => f.Codigo == "COR_RACA") with { FonteValores = "PROCESSO" },
        ]);

    [Fact(DisplayName = "A cópia recopiada do catálogo é a que as verificações avaliam: fonte passada a PROCESSO recusa pela verificação de oferta")]
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

    [Fact(DisplayName = "A publicação recusada por verificação posterior à recópia descarta o que a recópia mudou")]
    public async Task Handle_RecusaPosteriorARecopia_DescartaAsAlteracoes()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        CatalogoComCorRacaDoProcesso(mocks);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

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
        CatalogoComNomeSocialCategorico(mocks);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
        resposta.Error.Message.Should().Contain(item.Rotulo).And.Contain("Regrave o item");
        mocks.Canonicalizer.DidNotReceiveWithAnyArgs().Canonicalizar(default!);
    }

    private static void CatalogoComNomeSocialCategorico(Mocks mocks)
    {
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns([
            .. CatalogoDoConjuntoBasico.Fatos.Where(static f => f.Codigo != "DESEJA_NOME_SOCIAL"),
            CatalogoDoConjuntoBasico.Fatos.Single(static f => f.Codigo == "DESEJA_NOME_SOCIAL") with { Dominio = "CATEGORICO" },
        ]);
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
        entradaCapturada!.DadosDeCatalogo.MetadadosFatosCongelados.Should().BeNull(
            "nenhuma condição de gatilho existe no processo — nada a congelar, mesmo o catálogo tendo sido lido " +
            "para a verificação de valor inativo");
        _ = await mocks.FatoCandidatoReader.Received(1).ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com condição de gatilho resolvida, MetadadosFatosCongelados carrega o metadado do fato citado")]
    public async Task Handle_ComCondicaoDeGatilhoResolvida_ResolveMetadadoDoFato()
    {
        // A modalidade do candidato vem da ampla concorrência que o processo oferta.
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "MODALIDADE"), 0).Value!])
            .IsSuccess.Should().BeTrue();

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoModalidade() with { PontoResolucao = "RESULTADO_FINAL" }]));

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        ConferirMetadadosDaModalidade(entradaCapturada);
    }

    private static void ConferirMetadadosDaModalidade(EntradaCanonicalizacao? entradaCapturada)
    {
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.DadosDeCatalogo.MetadadosFatosCongelados.Should().NotBeNull();
        entradaCapturada.DadosDeCatalogo.MetadadosFatosCongelados.Should().ContainKey("MODALIDADE");
        ConferirMetadado(entradaCapturada.DadosDeCatalogo.MetadadosFatosCongelados!["MODALIDADE"]);
    }

    private static void ConferirMetadado(MetadadoFatoCongelado metadado)
    {
        metadado.Dominio.Should().Be("CATEGORICO");
        metadado.Origem.Should().Be("DERIVADO");
        metadado.Cardinalidade.Should().Be("MULTIVALORADO");
        metadado.PontoResolucao.Should().Be("RESULTADO_FINAL");
        metadado.Binding.Should().Be("REGRA_DERIVACAO:MODALIDADE");
        metadado.ValoresDominioDeclarados.Should().BeNull("MODALIDADE é escopo-processo — os valores vêm da oferta do processo, não de FatoValorDominio");
    }

    [Fact(DisplayName = "A publicação é recusada quando o fato do gatilho ou o tipo da exigência estão sem a proteção aprovada")]
    public async Task Handle_ItemSemProtecaoAprovada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "MODALIDADE"), 0).Value!])
            .IsSuccess.Should().BeTrue();
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        ITipoDocumentoReader tipos = ProtecoesPendentes(mocks);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(mocks, processo, documento, tipos: tipos);

        resposta.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            "ProcessoSeletivo.TipoDocumentoComProtecaoPendente", "ProcessoSeletivo.FatoComProtecaoPendente");
        eventos.Should().BeEmpty();
        mocks.Canonicalizer.DidNotReceiveWithAnyArgs().Canonicalizar(default!);
    }

    private static ITipoDocumentoReader ProtecoesPendentes(Mocks mocks)
    {
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[FatoModalidade() with { PontoResolucao = "RESULTADO_FINAL", ProtecaoAprovada = false }]));
        ITipoDocumentoReader tipos = Substitute.For<ITipoDocumentoReader>();
        tipos.ListarVivosAsync(Arg.Any<CancellationToken>()).Returns([CadastrosVivos.TipoDocumento("LAUDO_MEDICO") with { ProtecaoAprovada = false }]);
        return tipos;
    }

    [Fact(DisplayName = "Gatilho que cita fato que o processo deixou de derivar recusa a publicação")]
    public async Task Handle_GatilhoCitaFatoForaDoProcesso_Recusa()
    {
        const string derivado = "REGIME_DE_VAGA";
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        RemoverDerivacaoReferenciada(processo, faseId, derivado);

        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[
                FatoModalidade() with { Codigo = derivado, Binding = $"REGRA_DERIVACAO:{derivado}", PontoResolucao = "RESULTADO_FINAL" }]));

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
        processo.Status.Should().Be(StatusProcesso.Rascunho);
    }

    private static void RemoverDerivacaoReferenciada(ProcessoSeletivo processo, Guid faseId, string derivado)
    {
        processo.DefinirRegrasDerivacao(
            [ConfiguracaoDerivacaoFato.Criar(derivado, [RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!]).Value!]).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, derivado), 0).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Derivado citado por gatilho herda a fase da dependência que o gatilho não cita")]
    public async Task Handle_DerivadoComDependenciaForaDoCronograma_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        ConfigurarDependenciaDaModalidade(processo, faseId);

        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        CatalogoComDependenciaForaDoCronograma(mocks);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.Error!.Code.Should().Be(DocumentoExigidoErrorCodes.PontoResolucaoForaDoCronograma);
    }

    private static void CatalogoComDependenciaForaDoCronograma(Mocks mocks)
    {
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
    }

    private static void ConfigurarDependenciaDaModalidade(ProcessoSeletivo processo, Guid faseId)
    {
        processo.DefinirItens(
            [FatoColetado.Criar("TEM_RENDA", 0, "Tem renda?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!]).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(
            [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(PredicadoDnf.CriarDeCondicoesAgrupadas(
                [(0, CondicaoDnf.Criar("TEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!)],
            FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "MODALIDADE"), 0).Value!])
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Código de fato que não resolve no catálogo aborta a publicação ANTES de canonicalizar")]
    public async Task Handle_CodigoDeFatoNaoResolve_AbortaAntesDeCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "FATO_INEXISTENTE"), 0).Value!])
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
