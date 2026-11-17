namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;


using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>
/// Cobertura do <see cref="DefinirFatosColetadosCommandHandler"/> (Story #984): a coletabilidade (só fato declarado com binding de campo
/// de inscrição), a validação semântica das pré-condições contra o vocabulário fechado e a obrigatoriedade dos itens.
/// Os casos usam o formulário de habilitação, livre da seção reservada do conjunto básico, que a inscrição monta e confere à parte.
/// </summary>
public sealed class DefinirFatosColetadosCommandHandlerTests
{
    private static (ProcessoSeletivo Processo, FatoColetado BaixaRenda) ProcessoComInscricaoGravadaSemOsTresDados(int ordemDaBaixaRenda, int ordemDoGrupo)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();
        FatoColetado baixaRenda = BaixaRendaNaSecao(ordemDaBaixaRenda);
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, [.. FormularioDeTeste.DadosBasicosDeAntesDosDadosDeGenero(), baixaRenda], PrecondicaoIfMatch.Ausente, [ComposicaoFamiliar(ordemDoGrupo)])
            .IsSuccess.Should().BeTrue();
        processo.FatosColetados.Where(static f => f.FatoCodigo is "SEXO_CIVIL" or "IDENTIDADE_GENERO" or "ORIENTACAO_SEXUAL")
            .Should().BeEmpty("pré-condição: o formulário foi gravado antes dos três dados");

        return (processo, baixaRenda);
    }

    private static FatoColetado BaixaRendaNaSecao(int ordem) => FatoColetado.Criar(
            "BAIXA_RENDA", ordem, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, "PESSOAL",
            etapaCodigo: FormularioDeTeste.Secao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static GrupoColetado ComposicaoFamiliar(int ordem) => GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", ordem, FormularioDeTeste.Secao, "Composição familiar", 0, 10, null, Obrigatoriedade.Nunca,
            [FatoColetado.Criar("PARENTESCO", 0, "Parentesco", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            FinalidadeFormulario.Inscricao).Value!;

    private static Task<Result<MutacaoAceita>> RegravarSoOsItensAsync(ProcessoSeletivo processo, FatoColetado baixaRenda) =>
        DefinirNaInscricaoAsync(
            NovosMocks(processo, processo.Id), processo,
            [new FatoColetadoInput("BAIXA_RENDA", baixaRenda.Ordem, "Baixa renda", "BOOLEANO", "NUNCA", null) { EtapaCodigo = FormularioDeTeste.Secao }]);

    [Fact(DisplayName = "O formulário de inscrição gravado antes dos dados de sexo e gênero recebe os três na gravação, sem colidir com a ordem dos itens")]
    public async Task Handle_InscricaoGravadaSemOsTresDados_ReporOsItensSemColidirAOrdem()
    {
        (ProcessoSeletivo processo, FatoColetado baixaRenda) = ProcessoComInscricaoGravadaSemOsTresDados(
            ConjuntoBasicoDaInscricao.Itens.Count - 3, ConjuntoBasicoDaInscricao.Itens.Count - 2);
        CodigosDaConferencia(processo).Should().Contain(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);

        Result<MutacaoAceita> resultado = await RegravarSoOsItensAsync(processo, baixaRenda);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        List<FatoColetado> daInscricao = [.. processo.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)];
        daInscricao.Select(static f => f.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        CodigosDaConferencia(processo).Should().NotContain(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico, "a regravação repõe o conjunto básico");
        daInscricao.Select(static f => f.Ordem).Concat(processo.GruposColetados.Select(static g => g.Ordem)).Should().OnlyHaveUniqueItems();
        processo.GruposColetados.Single().Ordem.Should().Be(baixaRenda.Ordem + 1 + 3, "o grupo omitido sobe com os itens, para a ordem seguir única");
        daInscricao.Where(static f => ConjuntoBasicoDaInscricao.Fatos.Contains(f.FatoCodigo))
            .Should().OnlyContain(static f => f.EtapaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao);
    }

    [Fact(DisplayName = "Com um buraco depois da seção, regravar só os itens sobe item e grupo omitido juntos, sem repetir ordem")]
    public async Task Handle_InscricaoGravadaComBuracoDepoisDaSecao_ItemEGrupoSobemJuntos()
    {
        int fimDaSecao = ConjuntoBasicoDaInscricao.Itens.Count - 3;
        (ProcessoSeletivo processo, FatoColetado baixaRenda) = ProcessoComInscricaoGravadaSemOsTresDados(fimDaSecao + 3, fimDaSecao);

        Result<MutacaoAceita> resultado = await RegravarSoOsItensAsync(processo, baixaRenda);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao).Select(static f => f.Ordem)
            .Concat(processo.GruposColetados.Select(static g => g.Ordem)).Should().OnlyHaveUniqueItems();
        processo.GruposColetados.Single().Ordem.Should().Be(fimDaSecao + 3);
        processo.FatosColetados.Single(static f => f.FatoCodigo == "BAIXA_RENDA").Ordem.Should().Be(baixaRenda.Ordem + 3);
    }

    [Fact(DisplayName = "Processo inexistente retorna ProcessoSeletivo.NaoEncontrado sem tocar no reader")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        Mocks mocks = NovosMocks(processo: null, processoId);
        DefinirFatosColetadosCommand command = new(processoId, FinalidadeFormulario.Habilitacao, [], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    private static FatoColetadoInput[] ItensDaColetaValida() =>
    [
        new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
        new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA", "IGUAL", "PRETA")]]),
    ];

    [Fact(DisplayName = "Coleta com pré-condição válida é aceita, persistida, e devolve 204 sem ETag em rascunho")]
    public async Task Handle_ColetaValida_DefineEPersiste()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, new DefinirFatosColetadosCommand(
            processo.Id, FinalidadeFormulario.Habilitacao, ItensDaColetaValida(), PrecondicaoIfMatch.Ausente));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ETag.Should().BeNull("em rascunho não há sessão editorial nem ETag");
        processo.FatosColetados.Select(f => f.FatoCodigo).Should().BeEquivalentTo(["COR_RACA", "BAIXA_RENDA"]);
        await mocks.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Dois itens fora das seções do formulário saem juntos, cada um no seu campo")]
    public async Task Handle_ItensForaDeSecao_AcumulaAsRecusas()
    {
        Result<MutacaoAceita> resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null, EtapaCodigo: "INEXISTENTE"),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", null, EtapaCodigo: "OUTRA"),
        ]);

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["itens[0].etapaCodigo", "itens[1].etapaCodigo"]);
    }

    [Fact(DisplayName = "Coletar fato desativado no catálogo é recusado quando o processo ainda não o coletava; já coletado, continua")]
    public async Task Handle_FatoDesativado_RecusaSoVinculoNovo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "BAIXA_RENDA" ? f with { Ativo = false } : f)]));
        DefinirFatosColetadosCommand coletaBaixaRenda = new(
            processo.Id, FinalidadeFormulario.Habilitacao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        (await HandleAsync(mocks, coletaBaixaRenda)).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);

        processo.DefinirItens(
            [FatoColetado.Criar("BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
            PrecondicaoIfMatch.Ausente, FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        (await HandleAsync(mocks, coletaBaixaRenda)).IsSuccess.Should().BeTrue("o fato já era coletado pelo processo");
    }

    private static DocumentoExigido ExigenciaQueComprovaBaixaRenda(Guid faseId) => DocumentoExigido.Criar(
            faseId, CadastrosVivos.IdentidadeDe("LAUDO_MEDICO"), "LAUDO_MEDICO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null,
            finalidade: FinalidadeFormulario.Inscricao, fatosComprovados: ["BAIXA_RENDA"]).Value!;

    [Fact(DisplayName = "O fato pendente que uma exigência apenas comprova continua vínculo novo: coletá-lo num campo novo do formulário é recusado")]
    public async Task Handle_FatoPendenteSoComprovadoPorExigencia_ColetaNovaERecusada()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS comprovado", out Guid faseId);
        FormularioDeTeste.GarantirFormulario(processo, FinalidadeFormulario.Habilitacao, PrecondicaoIfMatch.Ausente);
        DocumentoExigido exigencia = ExigenciaQueComprovaBaixaRenda(faseId);
        Result definida = processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "BAIXA_RENDA" ? f with { ProtecaoAprovada = false } : f)]));
        DefinirFatosColetadosCommand coletaBaixaRenda = new(
            processo.Id, FinalidadeFormulario.Habilitacao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        (await HandleAsync(mocks, coletaBaixaRenda)).Error!.Code.Should().Be(VinculoCatalogoErrorCodes.FatoComProtecaoPendente);
    }

    [Fact(DisplayName = "Coletar um fato derivado (MODALIDADE) é recusado como não coletável")]
    public async Task Handle_FatoDerivado_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao, [new FatoColetadoInput("MODALIDADE", 0, "Modalidade", "SELECAO_MULTIPLA", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoNaoColetavel");
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato de membro de grupo não é coletado como item do formulário")]
    public async Task Handle_FatoDeMembroDeGrupo_RetornaNaoColetavel()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com(
            [.. VocabularioSeed().Select(static f => f.Codigo == "BAIXA_RENDA" ? f with { Escopo = "MEMBRO_GRUPO" } : f)]));
        DefinirFatosColetadosCommand command = new(
            processo.Id, FinalidadeFormulario.Habilitacao, [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", null)], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.FatoNaoColetavel);
    }

    [Fact(DisplayName = "Coletar um fato computado de atributo (RENDA_PER_CAPITA) é recusado como não coletável")]
    public async Task Handle_FatoComputado_RetornaNaoColetavel()
    {
        Result<MutacaoAceita> resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [new FatoColetadoInput("RENDA_PER_CAPITA", 0, "Renda per capita", "NUMERO", "NUNCA", null)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoNaoColetavel");
    }

    [Fact(DisplayName = "Fato fora do vocabulário é recusado como desconhecido, sem tradução")]
    public async Task Handle_FatoForaDoVocabulario_RetornaDesconhecido()
    {
        Result<MutacaoAceita> resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [new FatoColetadoInput("V", 0, "V", "SELECAO_UNICA", "NUNCA", null)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ItemFormulario.FatoDesconhecido");
    }

    [Fact(DisplayName = "Pré-condição com operador incompatível com o domínio do fato citado é recusada")]
    public async Task Handle_OperadorIncompativel_RetornaErroSemantico()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // COR_RACA é categórico: MAIOR_IGUAL não se aplica.
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
        [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA", "MAIOR_IGUAL", "PRETA")]]),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }

    [Fact(DisplayName = "Pré-condição que cita fato posterior na ordem é recusada pela estrutura do grafo (domínio)")]
    public async Task Handle_CitaFatoPosterior_RetornaErroDoDominio()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        // BAIXA_RENDA na ordem 0 cita COR_RACA (ordem 1, posterior).
        DefinirFatosColetadosCommand command = new(processo.Id, FinalidadeFormulario.Habilitacao,
        [
            new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "BOOLEANO", "NUNCA", [[Condicao("COR_RACA", "IGUAL", "PRETA")]]),
            new FatoColetadoInput("COR_RACA", 1, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
        ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> resultado = await HandleAsync(mocks, command);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("GrafoFormulario.CitaFatoPosterior");
    }

    [Theory(DisplayName = "TipoRenderizacao incoerente com Domínio/Cardinalidade do fato no catálogo é recusado")]
    [InlineData("COR_RACA", "BOOLEANO", "COR_RACA é CATEGORICO/ESCALAR — só SELECAO_UNICA é coerente")]
    [InlineData("COR_RACA", "NUMERO", "COR_RACA é CATEGORICO/ESCALAR — NUMERO não é coerente com nenhum categórico")]
    [InlineData("COR_RACA", "SELECAO_MULTIPLA", "COR_RACA é ESCALAR, não MULTIVALORADO — só SELECAO_UNICA é coerente")]
    [InlineData("BAIXA_RENDA", "SELECAO_UNICA", "BAIXA_RENDA é BOOLEANO — só BOOLEANO é coerente")]
    [InlineData("BAIXA_RENDA", "NUMERO", "BAIXA_RENDA é BOOLEANO — NUMERO não é coerente")]
    [InlineData("NOME_SOCIAL", "SELECAO_UNICA", "NOME_SOCIAL é TEXTO — só TEXTO é coerente")]
    [InlineData("BAIXA_RENDA", "TEXTO", "BAIXA_RENDA é BOOLEANO — TEXTO não é coerente")]
    [InlineData("DATA_NASCIMENTO", "TEXTO", "DATA_NASCIMENTO é DATA — só DATA é coerente")]
    [InlineData("ENDERECO_RESIDENCIAL", "DATA", "ENDERECO_RESIDENCIAL é ENDERECO — só ENDERECO é coerente")]
    public async Task Handle_TipoRenderizacaoIncoerente_RetornaErroDeCoerencia(
        string fatoCodigo, string tipoRenderizacaoIncoerente, string motivo)
    {
        Result<MutacaoAceita> resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [new FatoColetadoInput(fatoCodigo, 0, "Rótulo", tipoRenderizacaoIncoerente, "NUNCA", null)]);

        resultado.IsFailure.Should().BeTrue(motivo);
        resultado.Error!.Code.Should().Be("ItemFormulario.TipoRenderizacaoIncoerenteComDominio");
    }

    [Theory(DisplayName = "Obrigatoriedade é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele; o resto é recusado no campo")]
    [InlineData("QUANDO", false)]
    [InlineData("SEMPRE", true)]
    [InlineData("OBRIGATORIO", false)]
    public async Task Handle_ObrigatoriedadeIncoerente_Recusa(string tipo, bool comPredicado)
    {
        Result<MutacaoAceita> resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
            new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "NUNCA", null),
            new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", tipo, null,
                PredicadoObrigatoriedade: comPredicado ? [[Condicao("COR_RACA", "IGUAL", "PRETA")]] : null),
        ]);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[1].obrigatoriedade",
            Error = new { Code = FatoColetadoErrorCodes.ObrigatoriedadeInvalida },
        });
    }

    private static FatoColetadoInput[] ItensComObrigatoriedadeQuando() =>
    [
        new FatoColetadoInput("COR_RACA", 0, "Cor ou raça", "SELECAO_UNICA", "SEMPRE", null),
        new FatoColetadoInput("BAIXA_RENDA", 1, "Baixa renda", "BOOLEANO", "QUANDO", null,
            PredicadoObrigatoriedade: [[Condicao("COR_RACA", "IGUAL", "PRETA")]], Ajuda: "Renda por pessoa da família", PedirConfirmacao: true),
    ];

    [Fact(DisplayName = "Obrigatoriedade QUANDO sobre campo anterior é aceita e congela o predicado")]
    public async Task Handle_ObrigatoriedadeQuando_Aceita()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Result<MutacaoAceita> resultado = await DefinirNaHabilitacaoAsync(processo, ItensComObrigatoriedadeQuando());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FatoColetado baixaRenda = processo.FatosColetados.Single(static f => f.FatoCodigo == "BAIXA_RENDA");
        baixaRenda.Obrigatoriedade.Tipo.Should().Be(TipoObrigatoriedade.Quando);
        baixaRenda.Obrigatoriedade.FatosCitados.Should().Equal("COR_RACA");
        baixaRenda.Ajuda.Should().Be("Renda por pessoa da família");
        baixaRenda.PedirConfirmacao.Should().BeTrue();
    }
}
