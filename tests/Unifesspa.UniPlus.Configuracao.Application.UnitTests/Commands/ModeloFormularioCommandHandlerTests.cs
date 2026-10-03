namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A escrita do modelo de formulário confere o conteúdo contra o catálogo de fatos, de termos e de
/// tipos de processo pelas mesmas regras do formulário do processo (UNI-REQ-0144), e recusa só o
/// vínculo novo a fato ou tipo desativado (ADR-0136).
/// </summary>
public sealed class ModeloFormularioCommandHandlerTests
{
    private const string Finalidade = "Verificação dos requisitos do processo seletivo.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    private readonly IModeloFormularioRepository _repository = Substitute.For<IModeloFormularioRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly ITermoConsentimentoReader _termos = Substitute.For<ITermoConsentimentoReader>();
    private readonly ITipoProcessoReader _tipos = Substitute.For<ITipoProcessoReader>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private readonly FatoCandidato _certificado = Declarado("CERTIFICADO");
    private readonly FatoCandidato _dataNascimento = Declarado("DATA_NASCIMENTO", DominioFato.Data);
    private readonly FatoCandidato _faixaEtaria = FatoCandidato.Criar(
        "FAIXA_ETARIA", "Faixa etária", null, DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, null, null, "INSCRICAO",
        "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese, sistema: true).Value!;
    private readonly FatoCandidato _maiorIdade = Declarado("MAIOR_IDADE", escopo: EscopoFato.MembroGrupo);
    private readonly FatoCandidato _semRenda = Declarado("SEM_RENDA", escopo: EscopoFato.MembroGrupo);
    private readonly FatoCandidato _cpf = Declarado("CPF_RESPONSAVEL", DominioFato.Texto, FormatoTexto.Cpf);
    private readonly FatoCandidato _idade = FatoCandidato.Criar(
        "IDADE", "Idade", null, DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, null, null, "INSCRICAO",
        "ATRIBUTO_CANDIDATO:IDADE", EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese, sistema: true).Value!;

    public ModeloFormularioCommandHandlerTests()
    {
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [_certificado, _cpf, _idade, _maiorIdade, _semRenda, _dataNascimento, _faixaEtaria]);
        _termos.ListarVersoesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        _tipos.ObterAtivoPorCodigoAsync("PSR", Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.NewGuid(), "PSR", "PSR", null));
    }

    [Fact(DisplayName = "Cria o modelo conferido contra o catálogo, com o formato do campo de texto vindo do fato")]
    public async Task Criar_ConteudoValido_GravaComOFormatoDoCatalogo()
    {
        ModeloFormulario? gravado = null;
        await _repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CPF_RESPONSAVEL", 0, "TEXTO")), tipo: "PSR");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        gravado!.Conteudo.Itens.Single().Formato.Should().Be("CPF");
    }

    [Fact(DisplayName = "O campo do grupo cita o campo anterior da mesma ocorrência, e o grupo é gravado")]
    public async Task Criar_CampoDoGrupoCitaCampoAnterior_Grava()
    {
        ModeloFormulario? gravado = null;
        await _repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarAsync(ComComposicao(
            Campo("MAIOR_IDADE", 0), Campo("SEM_RENDA", 1) with { Precondicao = Quando("MAIOR_IDADE", true) }));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        gravado!.Conteudo.Grupos.Single().Subitens.Select(static s => s.FatoCodigo).Should().Equal("MAIOR_IDADE", "SEM_RENDA");
    }

    [Fact(DisplayName = "Grupo que inclui o candidato sem o campo de parentesco é recusado no modelo")]
    public async Task Criar_GrupoQueIncluiOCandidatoSemParentesco_Recusa()
    {
        ConteudoDoModeloInput conteudo = ComComposicao(Campo("MAIOR_IDADE", 0));
        conteudo = conteudo with { Grupos = [conteudo.Grupos![0] with { IncluiCandidato = true }] };

        Result<Guid> resultado = await CriarAsync(conteudo);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "conteudo.grupos[0].subitens" && e.Error.Code == GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto);
    }

    [Fact(DisplayName = "Campo do grupo com fato do candidato não é coletável na ocorrência")]
    public async Task Criar_CampoDoGrupoComFatoDoCandidato_Recusa()
    {
        Result<Guid> resultado = await CriarAsync(ComComposicao(Campo("CPF_RESPONSAVEL", 0) with { TipoRenderizacao = "TEXTO" }));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.grupos[0].subitens[0].fatoCodigo");
    }

    [Fact(DisplayName = "Campo do grupo com fato de membro desativado é recusado como vínculo novo")]
    public async Task Criar_CampoDoGrupoDesativado_Recusa()
    {
        _maiorIdade.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await CriarAsync(ComComposicao(Campo("MAIOR_IDADE", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Theory(DisplayName = "Regra cita a faixa etária quando a data de nascimento é coletada antes; sem ela, é recusada")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Criar_RegraCitaFaixaEtaria_PelaDataDeNascimento(bool coletaDataDeNascimento)
    {
        FatoColetadoInput cita = Item("CERTIFICADO", 1) with { Precondicao = Quando("FAIXA_ETARIA", 18) };
        ConteudoDoModeloInput conteudo = coletaDataDeNascimento ? Conteudo(Item("DATA_NASCIMENTO", 0, "DATA"), cita) : Conteudo(cita);

        Result<Guid> resultado = await CriarAsync(conteudo);

        if (coletaDataDeNascimento)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
        }
    }

    [Fact(DisplayName = "Item de fato calculado pelo sistema não é coletável")]
    public async Task Criar_ItemDeFatoCalculado_Recusa()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("IDADE", 0, "NUMERO")));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.itens[0].fatoCodigo", new DomainError(ItemFormularioErrorCodes.FatoNaoColetavel, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "Regra que cita valor fora do tipo do fato é recusada pela semântica do predicado")]
    public async Task Criar_RegraComValorDeOutroTipo_Recusa()
    {
        FatoColetadoInput exibido = Item("CPF_RESPONSAVEL", 1, "TEXTO") with { Precondicao = Quando("CERTIFICADO", "SIM") };

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0), exibido));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("conteudo.itens[1].precondicao");
    }

    [Fact(DisplayName = "Regra que cita fato calculado de atributos do candidato é recusada")]
    public async Task Criar_RegraCitaAtributo_Recusa()
    {
        FatoColetadoInput exibido = Item("CERTIFICADO", 0) with { Precondicao = Quando("IDADE", 18) };

        Result<Guid> resultado = await CriarAsync(Conteudo(exibido));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("conteudo.itens[0].precondicao", GrafoFormularioErrorCodes.CitaAtributoDoCandidato));
    }

    [Fact(DisplayName = "Termo cuja versão não existe no catálogo é recusado")]
    public async Task Criar_VersaoDeTermoInexistente_Recusa()
    {
        TermoExigidoInput termo = new("LGPD", 0, Guid.NewGuid(), Guid.NewGuid(), null, "SEMPRE", null);

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Termos = [termo] });

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.termos[0].versaoId", new DomainError(ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "Termo que o catálogo já não traz continua no modelo que o escolheu; o termo novo é recusado")]
    public async Task Termo_RecusaSoOVinculoNovo()
    {
        TermoDoModelo gravado = new("LGPD", 0, Guid.NewGuid(), Guid.NewGuid(), null, Obrigatoriedade.Sempre);
        ItemDoModelo item = new("CERTIFICADO", 0, "DADOS", "Certificado", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false);
        ModeloFormulario existente = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, null,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [gravado], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
        TermoExigidoInput mantido = new(gravado.Codigo, 0, gravado.TermoId, gravado.VersaoId, null, "SEMPRE", null);
        TermoExigidoInput novo = new("IMAGEM", 1, Guid.NewGuid(), Guid.NewGuid(), null, "SEMPRE", null);

        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)) with { Termos = [mantido, novo] }, tipo: null);

        editar.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.termos[1].versaoId", new DomainError(ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "Tipo de processo inexistente ou desativado é recusado; o que o modelo já usava continua na edição")]
    public async Task TipoDeProcesso_RecusaSoONovo()
    {
        Result<Guid> criar = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: "PSIQ");
        ModeloFormulario existente = Existente("PSIQ");

        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: "PSIQ");

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModeloFormularioErrorCodes.TipoProcessoInexistente);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Fato desativado é recusado como vínculo novo; o modelo que já o usava continua com ele")]
    public async Task FatoDesativado_RecusaSoOVinculoNovo()
    {
        ModeloFormulario existente = Existente(tipo: null);
        _certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> criar = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)));
        Result editar = await AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        criar.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        editar.IsSuccess.Should().BeTrue(editar.Error?.Message);
    }

    [Fact(DisplayName = "Código de fato desativado com espaço é o mesmo fato: o vínculo novo é recusado")]
    public async Task Criar_FatoDesativadoComEspaco_Recusa()
    {
        _certificado.Desativar().IsSuccess.Should().BeTrue();

        Result<Guid> resultado = await CriarAsync(Conteudo(Item(" CERTIFICADO", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Derivado desativado cujas respostas formam as opções de um item é vínculo novo recusado")]
    public async Task Criar_OpcoesDeDerivadoDesativado_Recusa()
    {
        FatoCandidato opcao = FatoCandidato.CriarDoAdministrador(
            "OPCAO", "Opção", null, DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Global, null, "INSCRICAO",
            EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        FatoCandidato perfil = FatoCandidato.CriarDerivadoDoAdministrador(
            "PERFIL", "Perfil", null, DominioFato.Categorico, "INSCRICAO", EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        opcao.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        perfil.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        RegraDerivacao regra = RegraDerivacao.Criar(
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("CERTIFICADO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            "A").Value!;
        perfil.DefinirRegrasPadrao([regra], new CatalogoDeFatos([_certificado, opcao, perfil], [])).IsSuccess.Should().BeTrue();
        perfil.Desativar().IsSuccess.Should().BeTrue();
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([_certificado, opcao, perfil]);
        FatoColetadoInput comOpcoes = Item("OPCAO", 1, "SELECAO_UNICA") with { Restricoes = [new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["PERFIL"])] };

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0), comOpcoes));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact(DisplayName = "Pressuposto é fato coletável: o calculado pelo sistema é recusado")]
    public async Task Criar_PressupostoNaoColetavel_Recusa()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Pressupostos = ["IDADE"] });

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("conteudo.pressupostos[0]", ItemFormularioErrorCodes.FatoNaoColetavel));
    }

    [Fact(DisplayName = "Obrigatoriedade QUANDO sem predicado é recusada na leitura")]
    public async Task Criar_QuandoSemPredicado_Recusa()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0) with { Obrigatoriedade = "QUANDO" }));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new FieldError("conteudo.itens[0].obrigatoriedade", new DomainError(ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, string.Empty)),
            static o => o.Excluding(static e => e.Error.Message));
    }

    [Fact(DisplayName = "As recusas do catálogo e as do modelo saem no mesmo lote")]
    public async Task Criar_RecusasDoCatalogoEDoModelo_Acumulam()
    {
        Result<Guid> resultado = await CriarAsync(Conteudo(Item("IDADE", 0, "NUMERO")) with { Titulo = new string('T', 301) });

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [ItemFormularioErrorCodes.FatoNaoColetavel, EstruturaFormularioErrorCodes.TituloTamanho]);
    }

    [Fact(DisplayName = "Conteúdo que não se lê não esconde as recusas do cadastro")]
    public async Task Criar_RecusaDeLeitura_AcumulaAsDoCadastro()
    {
        CriarModeloFormularioCommand command = new(
            "", "Habilitação", null, "XYZ", null, Conteudo(Item("CERTIFICADO", 0) with { Obrigatoriedade = "QUANDO" }));

        Result<Guid> resultado = await CriarModeloFormularioCommandHandler.Handle(
            command, _repository, _fatos, _termos, _tipos, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("conteudo.itens[0].obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida),
            ("codigo", ModeloFormularioErrorCodes.CodigoObrigatorio),
            ("finalidade", EstruturaFormularioErrorCodes.FinalidadeInvalida),
        ]);
    }

    [Fact(DisplayName = "O tipo de processo é conferido na forma em que o cadastro o grava, sem normalizar a grafia")]
    public async Task Criar_TipoNaGrafiaDoCadastro_Aceita()
    {
        const string Decomposto = "ADMISSA\u0303O";
        _tipos.ObterAtivoPorCodigoAsync(Decomposto, Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.NewGuid(), Decomposto, "Admissão", null));

        Result<Guid> resultado = await CriarAsync(Conteudo(Item("CERTIFICADO", 0)), tipo: $" {Decomposto} ");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Corrida de concorrência (xmin) na gravação descarta o rastreamento e devolve conflito")]
    public async Task Atualizar_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await AtualizarAsync(Existente(tipo: null), Conteudo(Item("CERTIFICADO", 0)), tipo: null);

        resultado.Error!.Code.Should().Be(ModeloFormularioErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private Task<Result<Guid>> CriarAsync(ConteudoDoModeloInput conteudo, string? tipo = null) =>
        CriarModeloFormularioCommandHandler.Handle(
            new CriarModeloFormularioCommand("HABILITACAO_MEDICINA", "Habilitação Medicina", null, "HABILITACAO", tipo, conteudo),
            _repository, _fatos, _termos, _tipos, _unitOfWork, CancellationToken.None);

    private Task<Result> AtualizarAsync(ModeloFormulario existente, ConteudoDoModeloInput conteudo, string? tipo)
    {
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        return AtualizarModeloFormularioCommandHandler.Handle(
            new AtualizarModeloFormularioCommand(existente.Id, "Habilitação Medicina", null, tipo, conteudo),
            _repository, _fatos, _termos, _tipos, _unitOfWork, CancellationToken.None);
    }

    /// <summary>O modelo gravado com o certificado como item, antes de qualquer mudança no catálogo.</summary>
    private static ModeloFormulario Existente(string? tipo)
    {
        ItemDoModelo item = new("CERTIFICADO", 0, "DADOS", "Certificado", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false);
        return ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, tipo,
            new ConteudoDoModelo("Habilitação", [Secao, Revisao], [item], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }

    private static readonly EtapaDoModelo Secao = new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null);
    private static readonly EtapaDoModelo Revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    private static ConteudoDoModeloInput Conteudo(params FatoColetadoInput[] itens) => new(
        "Habilitação",
        [new EtapaFormularioInput("DADOS", 0, "SECAO", null, "Dados", null, null), new EtapaFormularioInput("REVISAO", 1, "BLOCO", "REVISAO_E_ACEITE", "Revisão", null, null)],
        itens,
        [],
        []);

    private static ConteudoDoModeloInput ComComposicao(params FatoColetadoInput[] campos) =>
        Conteudo(Item("CERTIFICADO", 0)) with
        {
            Grupos = [new GrupoColetadoInput("COMPOSICAO_FAMILIAR", 1, "Composição familiar", "DADOS", 1, 10, null, "SEMPRE", null, campos)],
        };

    private static FatoColetadoInput Campo(string fato, int ordem) => Item(fato, ordem) with { EtapaCodigo = null };

    private static FatoColetadoInput Item(string fato, int ordem, string tipo = "BOOLEANO") =>
        new(fato, ordem, fato, tipo, "SEMPRE", null, EtapaCodigo: "DADOS");

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando(string fato, object valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];

    private static FatoCandidato Declarado(
        string codigo, DominioFato dominio = DominioFato.Booleano, FormatoTexto? formato = null, EscopoFato escopo = EscopoFato.Candidato) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar, null, formato, "INSCRICAO", escopo,
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
}
