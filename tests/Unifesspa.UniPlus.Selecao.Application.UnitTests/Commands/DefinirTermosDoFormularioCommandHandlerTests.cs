namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Termos exigidos pelo formulário (UNI-REQ-0086): a versão vem do catálogo e o conteúdo dela é
/// congelado; as condições citam só o que o processo coleta ou deriva; a obrigatoriedade tem
/// predicado se, e só se, é <c>QUANDO</c>. Os casos usam o formulário de habilitação, que não tem a
/// seção do conjunto básico e coleta só o que o caso declara.
/// </summary>
public sealed class DefinirTermosDoFormularioCommandHandlerTests
{
    private static readonly Guid TermoId = Guid.CreateVersion7();
    private static readonly Guid VersaoId = Guid.CreateVersion7();

    private sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IFatoCandidatoReader FatoCandidatoReader,
        ITermoConsentimentoReader TermoReader,
        IUnitOfWork UnitOfWork);

    private static Mocks NovosMocks(ProcessoSeletivo processo)
    {
        Mocks mocks = new(
            Substitute.For<IProcessoSeletivoRepository>(),
            Substitute.For<IFatoCandidatoReader>(),
            Substitute.For<ITermoConsentimentoReader>(),
            Substitute.For<IUnitOfWork>());
        mocks.Repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(FatosDoCatalogo()));
        mocks.TermoReader.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(
        [
            new VersaoTermoConsentimentoView(TermoId, VersaoId, "AUTORIZACAO_DE_CONSULTA", 1, "Autorização de consulta", "Autorizo a consulta.", "Lei 12.711/2012",
                FormaAceite.RegistroComEnderecoIp, new string('b', 64)),
        ]);
        return mocks;
    }

    private static FatoCandidatoView[] FatosDoCatalogo() =>
    [
        new(Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["BRANCA", "PRETA", "PARDA"], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "BAIXA_RENDA", "Baixa renda", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "CAMPO_FORMULARIO:BAIXA_RENDA", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_ETARIA", "Faixa etária", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Numerico), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            null, "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new(Guid.CreateVersion7(), "FAIXA_DE_RENDA", "Faixa de renda", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
            ["MENOR_DE_18", "DE_18_A_59"], "INSCRICAO", "ATRIBUTO_CANDIDATO:FAIXA_DE_RENDA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Catalogo), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ];

    private static Task<Result> HandleAsync(Mocks mocks, ProcessoSeletivo processo, params TermoExigidoInput[] termos) =>
        DefinirTermosDoFormularioCommandHandler.Handle(
            new DefinirTermosDoFormularioCommand(processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), termos),
            mocks.Repository, mocks.FatoCandidatoReader, mocks.TermoReader, mocks.UnitOfWork, CancellationToken.None);

    private static ProcessoSeletivo ProcessoQueColetaCorRaca() => ProcessoQueColeta(ItemCorRaca());

    private static ProcessoSeletivo ProcessoQueColeta(params FatoColetado[] itens)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Termos", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirItens(itens, PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FatoColetado ItemCorRaca() =>
        FatoColetadoDeTeste.Coletando("COR_RACA", 0)
            .ComRotulo("Cor ou raça")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();

    private static FatoColetado ItemDataDeNascimento() =>
        FatoColetadoDeTeste.Coletando("DATA_NASCIMENTO", 1)
            .ComRotulo("Data de nascimento")
            .DoTipo(TipoDeCampo.Data)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();

    [Theory(DisplayName = "Condição do termo cita a faixa etária quando o processo coleta a data de nascimento")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_CondicaoCitaFaixaEtaria_PelaDataDeNascimento(bool coletaDataDeNascimento)
    {
        ProcessoSeletivo processo = coletaDataDeNascimento ? ProcessoQueColeta(ItemCorRaca(), ItemDataDeNascimento()) : ProcessoQueColetaCorRaca();

        Result resultado = await HandleAsync(
            NovosMocks(processo), processo, Termo("CONSULTA", TermoId, VersaoId).ExibidoQuando(Quando("FAIXA_ETARIA", 18)).Construir());

        resultado.IsSuccess.Should().Be(coletaDataDeNascimento, resultado.Error?.Message);
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    [Fact(DisplayName = "Termo condicional é aceito e congela o conteúdo da versão escolhida")]
    public async Task Handle_TermoCondicional_CongelaVersao()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("CONSULTA", TermoId, VersaoId).ExibidoQuando(Quando("COR_RACA", "PRETA")).ComObrigatoriedade("QUANDO", Quando("COR_RACA", "PRETA")).Construir());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        TermoExigidoFormulario termo = processo.TermosExigidos.Should().ContainSingle().Which;
        termo.Should().BeEquivalentTo(new { Texto = "Autorizo a consulta.", FormaAceite = FormaAceite.RegistroComEnderecoIp, HashVersao = new string('b', 64) });
        termo.Obrigatoriedade.Tipo.Should().Be(TipoObrigatoriedade.Quando);
        termo.Exibicao!.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "Versão que pertence a outro termo é recusada no índice do termo")]
    public async Task Handle_VersaoDeOutroTermo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("CONSULTA", Guid.CreateVersion7(), VersaoId).Construir());

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "termos[0].termo.versaoId",
            Error = new { Code = TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada },
        });
    }

    [Fact(DisplayName = "Condição de termo que cita fato que o processo não coleta nem deriva é recusada")]
    public async Task Handle_CondicaoCitaFatoNaoColetado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("CONSULTA", TermoId, VersaoId).ExibidoQuando(Quando("BAIXA_RENDA", true)).Construir());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("termos[0].regras.exibicao");
        processo.TermosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Condição que cita fato calculado de atributos do candidato sem dependências declaradas é recusada pelo nome, sem esconder a outra condição")]
    public async Task Handle_CondicaoCitaAtributoDoCandidato_RecusaNomeadaEAcumula()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("CONSULTA", TermoId, VersaoId).ExibidoQuando(Quando("FAIXA_DE_RENDA", "DE_18_A_59"))
                .ComObrigatoriedade("QUANDO", Quando("BAIXA_RENDA", true)).Construir());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("termos[0].regras.exibicao", GrafoFormularioErrorCodes.CitaAtributoDoCandidato),
            ("termos[0].regras.obrigatoriedade.predicado", "PredicadoDnf.FatoNaoColetadoPeloProcesso"),
        ]);
    }

    [Fact(DisplayName = "Versão inexistente não esconde a condição inválida do mesmo termo: as duas recusas saem juntas")]
    public async Task Handle_VersaoInexistenteECondicaoInvalida_AcumulaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("CONSULTA", TermoId, Guid.CreateVersion7()).ExibidoQuando(Quando("BAIXA_RENDA", true)).Construir());

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["termos[0].regras.exibicao", "termos[0].termo.versaoId"]);
    }

    [Theory(DisplayName = "Obrigatoriedade tem predicado se, e só se, é QUANDO")]
    [InlineData("QUANDO", false)]
    [InlineData("SEMPRE", true)]
    [InlineData("OBRIGATORIO", false)]
    public async Task Handle_ObrigatoriedadeIncoerente_Recusa(string tipo, bool comPredicado)
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("CONSULTA", TermoId, VersaoId).ComObrigatoriedade(tipo, comPredicado ? Quando("COR_RACA", "PRETA") : null).Construir());

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(TermoExigidoFormularioErrorCodes.ObrigatoriedadeInvalida);
    }

    [Fact(DisplayName = "Termo sem as regras é recusado na obrigatoriedade, no caminho das regras do termo")]
    public async Task Handle_TermoSemRegras_RecusaAObrigatoriedadeAusente()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();

        Result resultado = await HandleAsync(NovosMocks(processo), processo, Termo("CONSULTA", TermoId, VersaoId).SemRegras().Construir());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("termos[0].regras.obrigatoriedade", TermoExigidoFormularioErrorCodes.ObrigatoriedadeInvalida));
    }

    [Fact(DisplayName = "Termo sem a versão do catálogo é recusado como versão não encontrada, no caminho do termo")]
    public async Task Handle_TermoSemVersao_RecusaVersaoNaoEncontrada()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();

        Result resultado = await HandleAsync(NovosMocks(processo), processo, EntradaDoTermoExigidoDeTeste.Exigindo("CONSULTA").Construir());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("termos[0].termo.versaoId", TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada));
    }

    private static EntradaDoTermoExigidoDeTeste Termo(string codigo, Guid termoId, Guid versaoId, int ordem = 0) =>
        EntradaDoTermoExigidoDeTeste.Exigindo(codigo, ordem).DaVersao(termoId, versaoId);

    [Fact(DisplayName = "Forma, versão e unicidade saem na mesma resposta, entre termos diferentes")]
    public async Task Handle_RecusasDeFasesDiferentes_SaemJuntas()
    {
        ProcessoSeletivo processo = ProcessoQueColetaCorRaca();
        Mocks mocks = NovosMocks(processo);

        Result resultado = await HandleAsync(mocks, processo,
            Termo("", TermoId, VersaoId).Construir(),
            Termo("CONSULTA", TermoId, Guid.CreateVersion7(), ordem: 1).Construir(),
            Termo("CONSULTA", TermoId, VersaoId, ordem: 2).Construir());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("termos[0].codigo", TermoFormularioErrorCodes.CodigoObrigatorio),
            ("termos[1].termo.versaoId", TermoExigidoFormularioErrorCodes.VersaoNaoEncontrada),
            ("termos[2].codigo", TermoFormularioErrorCodes.CodigoDuplicado),
        ]);
    }
}
