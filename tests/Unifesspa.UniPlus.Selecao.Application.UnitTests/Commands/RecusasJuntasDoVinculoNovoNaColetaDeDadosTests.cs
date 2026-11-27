namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>
/// O vínculo novo ao catálogo recusado na mesma resposta das demais recusas da definição dos itens e do formulário
/// (ADR-0125): só o vínculo novo é recusado, e nada é gravado quando há qualquer recusa.
/// </summary>
public sealed class RecusasJuntasDoVinculoNovoNaColetaDeDadosTests
{
    private static Mocks MocksComCatalogo(ProcessoSeletivo processo, Func<FatoCandidatoView, FatoCandidatoView> ajuste)
    {
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com([.. VocabularioSeed().Select(ajuste)]));
        return mocks;
    }

    private static FatoCandidatoView BaixaRendaDesativada(FatoCandidatoView dado) => dado.Codigo == "BAIXA_RENDA" ? dado with { Ativo = false } : dado;

    private static Task<Result> DefinirNaHabilitacaoAsync(Mocks mocks, ProcessoSeletivo processo, params FatoColetadoInput[] itens) =>
        HandleAsync(mocks, new DefinirFatosColetadosCommand(processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), itens));

    private static FatoColetadoInput BaixaRenda(string? rotulo = "Baixa renda") =>
        EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo(rotulo).Construir();

    [Fact(DisplayName = "Item recusado na leitura com dado desativado acusa a recusa da leitura e o dado desativado")]
    public async Task Handle_ItemRecusadoComDadoDesativado_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(mocks, processo, BaixaRenda(rotulo: ""));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.FatoDesativado).And.HaveCountGreaterThan(1);
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Condição de item aceito que cita dado desativado é recusada junto da recusa de outro item")]
    public async Task Handle_CondicaoComDadoDesativadoEOutraRecusa_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(
            mocks,
            processo,
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ExibidoQuando([[Condicao("BAIXA_RENDA", "IGUAL", true)]]).Construir(),
            EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL", 1).ComRotulo("Nome social").DoTipo("DATA").Construir());

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain([VinculoCatalogoErrorCodes.FatoDesativado, "ItemFormulario.TipoDeCampoIncoerenteComDominio"]);
    }

    [Fact(DisplayName = "Valor desativado citado na condição de item aceito é recusado junto da recusa de outro item")]
    public async Task Handle_ValorDesativadoNaCondicaoEOutraRecusa_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, static f => f.Codigo == "COR_RACA"
            ? f with { ValoresDominioDeclarados = [new("BRANCA", "Branca", 0, true), new("PRETA", "Preta", 1, false)] }
            : f);

        Result resultado = await DefinirNaHabilitacaoAsync(
            mocks,
            processo,
            EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").ExibidoQuando([[Condicao("COR_RACA", "IGUAL", "PRETA")]]).Construir(),
            EntradaDoFatoColetadoDeTeste.Coletando("NOME_SOCIAL", 1).ComRotulo("Nome social").DoTipo("DATA").Construir());

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain([VinculoCatalogoErrorCodes.ValorDesativado, "ItemFormulario.TipoDeCampoIncoerenteComDominio"]);
    }

    [Fact(DisplayName = "Obrigatoriedade de item recusado que cita dado com proteção pendente é recusada junto da leitura")]
    public async Task Handle_ObrigatoriedadeDeItemRecusadoComProtecaoPendente_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, static f => f.Codigo == "BAIXA_RENDA" ? f with { ProtecaoAprovada = false } : f);

        Result resultado = await DefinirNaHabilitacaoAsync(
            mocks,
            processo,
            EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("").DoTipo("ESCOLHA_UNICA")
                .ComObrigatoriedade("QUANDO", [[Condicao("BAIXA_RENDA", "IGUAL", true)]]).Construir());

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.FatoComProtecaoPendente).And.HaveCountGreaterThan(1);
    }

    [Fact(DisplayName = "Campo de grupo recusado com dado desativado acusa a leitura e o dado desativado")]
    public async Task Handle_CampoDeGrupoRecusadoComDadoDesativado_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, static f => f.Codigo == "PARENTESCO" ? f with { Ativo = false } : f);
        GrupoColetadoInput grupo = Composicao() with
        {
            Subitens = [EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").DoTipo("TIPO_QUE_NAO_EXISTE").Construir()],
        };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.FatoDesativado);
        resultado.Errors.Select(static e => e.Field).Should().Contain("grupos[0].subitens[0].apresentacao.tipoDeCampo");
    }

    [Fact(DisplayName = "Item já vinculado ao processo com dado desativado continua aceito, mesmo havendo outra recusa no pedido")]
    public async Task Handle_VinculoExistenteDesativado_NaoEhRecusado()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("BAIXA_RENDA", 0).ComRotulo("Baixa renda").Construir()], PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(
            mocks, processo, BaixaRenda(), EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA", 1).ComRotulo("").Construir());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => e.Error.Code).Should().NotContain(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Só o vínculo novo recusado: a recusa sai e nada é salvo")]
    public async Task Handle_SoVinculoNovo_RecusaSemSalvar()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(mocks, processo, BaixaRenda());

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        processo.FatosColetados.Should().BeEmpty();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O vínculo novo sai depois das recusas dos campos")]
    public async Task Handle_VinculoNovoComRecusaDeCampo_VinculoPorUltimo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(
            mocks, processo, BaixaRenda(), EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA", 1).ComRotulo("").Construir());

        resultado.Errors.Count.Should().BeGreaterThan(1);
        resultado.Errors[^1].Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Código em branco em item recusado fica fora do vínculo, sem lançar")]
    public async Task Handle_CodigoEmBranco_FicaForaDoVinculo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(mocks, processo, EntradaDoFatoColetadoDeTeste.Coletando(" ").ComRotulo("Campo").Construir());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => e.Error.Code).Should().NotContain(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Acima do teto só a recusa da quantidade sai, sem ler o catálogo")]
    public async Task Handle_AcimaDoTeto_SoARecusaDaQuantidade()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = MocksComCatalogo(processo, BaixaRendaDesativada);

        Result resultado = await DefinirNaHabilitacaoAsync(mocks, processo, [.. ItensAcimaDoTeto()]);

        resultado.Errors.Should().ContainSingle();
        await ConferirQueNadaFoiLidoNemGravadoAsync(mocks);
    }

    private static Task<Result> DefinirFormularioAsync(
        ProcessoSeletivo processo, Guid faseId, IReadOnlyList<PaginaDoFormularioInput> paginas, IReadOnlyList<FatoCandidatoView> catalogo, IFatoCandidatoReader? reader = null, IUnitOfWork? unitOfWork = null)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        reader ??= Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(catalogo);
        return DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(processo.Id, PapelDoFormularioDeTeste.Inscricao.Codigo(), faseId, null, true, paginas),
            repository, reader, unitOfWork ?? Substitute.For<IUnitOfWork>(), CancellationToken.None);
    }

    private static PaginaDoFormularioInput SecaoQueCitaBaixaRenda(string titulo = "Dados") =>
        EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo(titulo).ExibidaQuando([[Condicao("BAIXA_RENDA", "IGUAL", true)]]).Construir();

    private static IReadOnlyList<FatoCandidatoView> CatalogoComBaixaRendaDesativada() =>
        CatalogoDoConjuntoBasico.Com([.. VocabularioSeed().Select(BaixaRendaDesativada)]);

    [Fact(DisplayName = "Página recusada na leitura com exibição que cita dado desativado acusa as duas recusas")]
    public async Task Formulario_PaginaRecusadaComExibicaoDesativada_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid fase);
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("")
            .ExibidaQuando([[Condicao("BAIXA_RENDA", "IGUAL", true)]]).Construir();

        Result resultado = await DefinirFormularioAsync(processo, fase, [pagina], CatalogoComBaixaRendaDesativada());

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.FatoDesativado);
        resultado.Errors.Select(static e => e.Field).Should().Contain("paginas[0].apresentacao.titulo");
    }

    [Fact(DisplayName = "Dado com proteção pendente na exibição de uma página é recusado junto da página com tipo inexistente")]
    public async Task Formulario_ProtecaoPendenteEPaginaRecusada_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid fase);
        PaginaDoFormularioInput comTipoInexistente = EntradaDaPaginaDoFormularioDeTeste.Secao("OUTRA", 1).ComTitulo("Outra").Construir() with { Tipo = "TIPO_QUE_NAO_EXISTE" };
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "BAIXA_RENDA" ? f with { ProtecaoAprovada = false } : f)]);

        Result resultado = await DefinirFormularioAsync(processo, fase, [SecaoQueCitaBaixaRenda(), comTipoInexistente], catalogo);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.FatoComProtecaoPendente);
        resultado.Errors.Select(static e => e.Field).Should().Contain("paginas[1].tipo");
    }

    [Fact(DisplayName = "Página com tipo inexistente e exibição que cita valor desativado acusa as duas recusas")]
    public async Task Formulario_PaginaComTipoInexistenteEValorDesativado_AcusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid fase);
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados")
            .ExibidaQuando([[Condicao("COR_RACA", "IGUAL", "PRETA")]]).Construir() with
        { Tipo = "TIPO_QUE_NAO_EXISTE" };
        IReadOnlyList<FatoCandidatoView> catalogo = CatalogoDoConjuntoBasico.Com([.. VocabularioSeed().Select(static f => f.Codigo == "COR_RACA"
            ? f with { ValoresDominioDeclarados = [new("BRANCA", "Branca", 0, true), new("PRETA", "Preta", 1, false)] }
            : f)]);

        Result resultado = await DefinirFormularioAsync(processo, fase, [pagina], catalogo);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.ValorDesativado);
        resultado.Errors.Select(static e => e.Field).Should().Contain("paginas[0].tipo");
    }

    [Fact(DisplayName = "Só o vínculo novo na exibição: a recusa sai com as da estrutura e nada é gravado")]
    public async Task Formulario_SoVinculoNovo_RecusaComAEstruturaSemGravar()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid fase);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result resultado = await DefinirFormularioAsync(processo, fase, [SecaoQueCitaBaixaRenda()], CatalogoComBaixaRendaDesativada(), unitOfWork: unitOfWork);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(VinculoCatalogoErrorCodes.FatoDesativado).And.HaveCountGreaterThan(1);
        resultado.Errors[^1].Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Na criação do formulário de inscrição os itens do conjunto básico entram no vínculo")]
    public async Task Formulario_CriacaoDaInscricao_ItensDoConjuntoBasicoEntramNoVinculo()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid fase);
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Inscricao).IsSuccess.Should().BeTrue();
        string desativado = ConjuntoBasicoDaInscricao.Fatos.Order(StringComparer.Ordinal).First();
        IReadOnlyList<FatoCandidatoView> catalogo = [.. CatalogoDoConjuntoBasico.Fatos.Select(f => f.Codigo == desativado ? f with { Ativo = false } : f)];
        PaginaDoFormularioInput[] paginas =
        [
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
            EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão e aceite").Construir(),
        ];

        Result resultado = await DefinirFormularioAsync(processo, fase, paginas, catalogo);

        resultado.Errors.Should().Contain(e => e.Error.Code == VinculoCatalogoErrorCodes.FatoDesativado && e.Origem == desativado);
    }

    [Fact(DisplayName = "Sem exibição e sem criação o catálogo não é lido")]
    public async Task Formulario_SemExibicaoESemCriacao_NaoLeOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid fase);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();

        await DefinirFormularioAsync(processo, fase, [EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir()], [], reader);

        await reader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
    }
}
