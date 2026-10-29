namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A alteração da definição do fato do administrador é recusada, sem gravar, quando deixaria de
/// valer o que o cita dentro da Configuração: regra padrão de outro derivado, agregado, modelo de
/// formulário e regra de aplicabilidade de modalidade (ADR-0136).
/// </summary>
public sealed class AlterarDefinicaoDoFatoCandidatoCommandHandlerTests
{
    private const string Finalidade = "Enquadramento na reserva de vagas.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;
    private static readonly Dictionary<string, Guid> Ids = [];

    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IPrecedenciaFaseRepository _precedencias = Substitute.For<IPrecedenciaFaseRepository>();
    private readonly IModeloFormularioRepository _modelos = Substitute.For<IModeloFormularioRepository>();
    private readonly IModalidadeRepository _modalidades = Substitute.For<IModalidadeRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    public AlterarDefinicaoDoFatoCandidatoCommandHandlerTests()
    {
        // O repositório devolve, a cada leitura, instâncias novas com a mesma identidade: a lista
        // sem rastreamento e o fato rastreado para mutação são objetos distintos, como no banco.
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => Catalogo());
        _precedencias.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);
        _modelos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([ModeloComCertificado()]);
        _modalidades.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([ModalidadeQueCitaOptou()]);
        foreach (FatoCandidato fato in Catalogo())
        {
            _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(_ => Catalogo().Single(f => f.Id == fato.Id));
        }
    }

    [Fact(DisplayName = "Alteração que não deixa de valer nenhuma citação é gravada")]
    public async Task Handle_SemCitacaoQuebrada_Grava()
    {
        Result resultado = await AlterarAsync("CERTIFICADO", Definicao("BOOLEANO", ponto: "HABILITACAO"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Domínio novo que o modelo de formulário não comporta é recusado, listando o modelo")]
    public async Task Handle_QuebraModelo_Recusa()
    {
        Result resultado = await AlterarAsync("CERTIFICADO", DefinicaoCategorica());

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "citacoes.modelos.HABILITACAO_MEDICINA" && e.Error.Code == FatoCandidatoErrorCodes.AlteracaoQuebraModeloDeFormulario);
        await DeveTerSidoDescartadoSemGravarAsync();
    }

    [Fact(DisplayName = "Domínio novo que a regra padrão de outro derivado não comporta é recusado, listando o derivado")]
    public async Task Handle_QuebraRegraPadraoDeOutroDerivado_Recusa()
    {
        Result resultado = await AlterarAsync("RENDA", DefinicaoCategorica());

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "citacoes.regrasPadrao.PERFIL" && e.Error.Code == FatoCandidatoErrorCodes.AlteracaoQuebraRegraPadraoDeOutroFato);
        await DeveTerSidoDescartadoSemGravarAsync();
    }

    [Fact(DisplayName = "Fonte nova do fato de membro que o agregado não acompanha é recusada, listando o agregado")]
    public async Task Handle_QuebraAgregado_Recusa()
    {
        Result resultado = await AlterarAsync(
            "CATEGORIA_RENDA", Definicao("CATEGORICO", cardinalidade: "ESCALAR", fonte: "PROCESSO", escopo: "MEMBRO_GRUPO"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "citacoes.agregados.CATEGORIAS_RENDA" && e.Error.Code == FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato);
        await DeveTerSidoDescartadoSemGravarAsync();
    }

    [Fact(DisplayName = "Domínio novo que a regra de aplicabilidade de uma modalidade não comporta é recusado, listando a modalidade")]
    public async Task Handle_QuebraRegraDeAplicabilidade_Recusa()
    {
        Result resultado = await AlterarAsync("OPTOU", DefinicaoCategorica());

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "citacoes.modalidades.PSVR_AMPLA" && e.Error.Code == FatoCandidatoErrorCodes.AlteracaoQuebraRegraDeAplicabilidade);
        await DeveTerSidoDescartadoSemGravarAsync();
    }

    [Fact(DisplayName = "Modelo de formulário inativo também é conferido")]
    public async Task Handle_QuebraModeloInativo_Recusa()
    {
        ModeloFormulario inativo = ModeloComCertificado(ativo: false);
        _modelos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([inativo]);

        Result resultado = await AlterarAsync("CERTIFICADO", DefinicaoCategorica());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("citacoes.modelos.HABILITACAO_MEDICINA");
    }

    [Fact(DisplayName = "Fato inexistente não carrega o catálogo, e definição recusada pelo próprio fato não carrega modelos nem modalidades")]
    public async Task Handle_RecusaDoProprioFato_NaoCarregaModelosNemModalidades()
    {
        Result inexistente = await AlterarAsync(Guid.NewGuid(), Definicao("BOOLEANO"));
        await _fatos.DidNotReceive().ListarTodosAsync(Arg.Any<CancellationToken>());
        Result invalida = await AlterarAsync(Ids["CERTIFICADO"], Definicao("FOO"));

        inexistente.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaoEncontrado);
        invalida.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoCandidatoErrorCodes.DominioInvalido);
        await _modelos.DidNotReceive().ListarTodosAsync(Arg.Any<CancellationToken>());
        await _modalidades.DidNotReceive().ListarVivasAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Agregado que já resolvia antes do membro não trava a alteração que não o causa")]
    public async Task Handle_AgregadoJaQuebradoAntes_NaoBloqueia()
    {
        _precedencias.ListarVivasAsync(Arg.Any<CancellationToken>())
            .Returns([PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!]);
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => Catalogo("HABILITACAO"));
        _fatos.ObterPorIdAsync(Ids["CATEGORIA_RENDA"], Arg.Any<CancellationToken>()).Returns(_ => Catalogo("HABILITACAO").Single(f => f.Codigo == "CATEGORIA_RENDA"));

        Result resultado = await AlterarAsync(
            "CATEGORIA_RENDA", Definicao("CATEGORICO", fonte: "GLOBAL", ponto: "HABILITACAO", escopo: "MEMBRO_GRUPO") with { FinalidadeTratamento = "Outra finalidade." });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private async Task DeveTerSidoDescartadoSemGravarAsync()
    {
        // O agregado já recebeu a definição nova; sem descartar, o save do Wolverine a gravaria.
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O agregado altera a definição enviando só os eixos que todo tipo exige")]
    public async Task Handle_AgregadoSemOsEixosOpcionais_Grava()
    {
        Result resultado = await AlterarAsync("CATEGORIAS_RENDA", new DefinicaoDoFatoInput("INSCRICAO", "PESSOAL", Finalidade, "CUMPRIMENTO_OBRIGACAO_LEGAL"));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Classificação e hipótese legal fora do vocabulário são recusadas no próprio campo, como fora do vocabulário")]
    public async Task Handle_TokenInvalidoDeProtecao_RecusaForaDoVocabulario()
    {
        Result resultado = await AlterarAsync(
            "CERTIFICADO", Definicao("BOOLEANO") with { ClassificacaoProtecao = "SENSIVELL", HipoteseLegal = "NAO_EXISTE" });

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(static e => e.Error.Message.Contains("fora do vocabulário", StringComparison.Ordinal));
        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo("classificacaoProtecao", "hipoteseLegal");
    }

    [Theory(DisplayName = "Alteração: eixo vazio ou só espaços é não informado e o desconhecido é inválido, como no cadastro")]
    [InlineData("dominio", "", FatoCandidatoErrorCodes.DominioObrigatorio)]
    [InlineData("dominio", "  ", FatoCandidatoErrorCodes.DominioObrigatorio)]
    [InlineData("dominio", "BOOL", FatoCandidatoErrorCodes.DominioInvalido)]
    [InlineData("cardinalidade", " ", FatoCandidatoErrorCodes.CardinalidadeObrigatoria)]
    [InlineData("cardinalidade", "MUITOS", FatoCandidatoErrorCodes.CardinalidadeInvalida)]
    [InlineData("escopo", "", FatoCandidatoErrorCodes.EscopoObrigatorio)]
    [InlineData("escopo", "TODOS", FatoCandidatoErrorCodes.EscopoObrigatorio)]
    [InlineData("classificacaoProtecao", "", FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria)]
    [InlineData("classificacaoProtecao", "SENSIVELL", FatoCandidatoErrorCodes.ClassificacaoProtecaoInvalida)]
    [InlineData("hipoteseLegal", " ", FatoCandidatoErrorCodes.HipoteseLegalObrigatoria)]
    [InlineData("hipoteseLegal", "NAO_EXISTE", FatoCandidatoErrorCodes.HipoteseLegalInvalida)]
    public async Task Handle_TokenDoEixo_RecusaComoNoCadastro(string campo, string token, string codigo)
    {
        DefinicaoDoFatoInput definicao = campo switch
        {
            "dominio" => Definicao("BOOLEANO") with { Dominio = token },
            "cardinalidade" => Definicao("BOOLEANO") with { Cardinalidade = token },
            "escopo" => Definicao("BOOLEANO") with { Escopo = token },
            "classificacaoProtecao" => Definicao("BOOLEANO") with { ClassificacaoProtecao = token },
            _ => Definicao("BOOLEANO") with { HipoteseLegal = token },
        };

        Result resultado = await AlterarAsync("CERTIFICADO", definicao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e => e.Field == campo && e.Error.Code == codigo);
    }

    [Fact(DisplayName = "Alteração: token com espaços nas pontas vale como o token aparado")]
    public async Task Handle_TokenComEspacos_Aceito()
    {
        Result resultado = await AlterarAsync("CERTIFICADO", Definicao(" BOOLEANO ", cardinalidade: " ESCALAR ", ponto: "HABILITACAO", escopo: " CANDIDATO "));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Alteração do agregado: eixo que o tipo infere, enviado vazio, é não informado e não é recusado")]
    public async Task Handle_AgregadoComEixosVazios_Grava()
    {
        Result resultado = await AlterarAsync(
            "CATEGORIAS_RENDA",
            new DefinicaoDoFatoInput("INSCRICAO", "PESSOAL", Finalidade, "CUMPRIMENTO_OBRIGACAO_LEGAL", "", "", " ", "", ""));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Modelo que já recusa a precondição por causa de outro fato não esconde a recusa que a alteração causa")]
    public async Task Handle_ModeloJaRecusadoPorOutroFato_RecusaACausaDaAlteracao()
    {
        _fatos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns(_ => [.. Catalogo(), Declarado("SEM_VALORES", DominioFato.Categorico, FonteValoresFato.Global)]);
        _modelos.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([ModeloComPrecondicaoSobre("OPTOU", "SEM_VALORES")]);
        _modalidades.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);

        // OPTOU passa a ser categórico sem valores: fora do vocabulário dos predicados, como SEM_VALORES já era.
        Result resultado = await AlterarAsync("OPTOU", DefinicaoCategorica());

        FieldError citacao = resultado.Errors.Should().ContainSingle().Which;
        citacao.Field.Should().Be("citacoes.modelos.HABILITACAO_MEDICINA");
        citacao.Error.Message.Should().Contain("O fato 'OPTOU' não pertence ao vocabulário fechado.");
    }

    private Task<Result> AlterarAsync(string codigo, DefinicaoDoFatoInput definicao) => AlterarAsync(Ids[codigo], definicao);

    private Task<Result> AlterarAsync(Guid id, DefinicaoDoFatoInput definicao) =>
        AlterarDefinicaoDoFatoCandidatoCommandHandler.Handle(
            new AlterarDefinicaoDoFatoCandidatoCommand(id, definicao),
            _fatos, _precedencias, _modelos, _modalidades, _unitOfWork, CancellationToken.None);

    private static DefinicaoDoFatoInput Definicao(
        string dominio, string? cardinalidade = "ESCALAR", string? fonte = null, string ponto = "INSCRICAO", string escopo = "CANDIDATO") =>
        new(ponto, "PESSOAL", Finalidade, "CUMPRIMENTO_OBRIGACAO_LEGAL", dominio, cardinalidade, fonte, null, escopo);

    private static DefinicaoDoFatoInput DefinicaoCategorica() => Definicao("CATEGORICO", fonte: "GLOBAL");

    private static List<FatoCandidato> Catalogo(string pontoDoMembro = "INSCRICAO")
    {
        FatoCandidato certificado = Declarado("CERTIFICADO");
        FatoCandidato renda = Declarado("RENDA");
        FatoCandidato optou = Declarado("OPTOU");
        FatoCandidato opcaoPelasCotas = Declarado(OpcaoPelasCotasDaLei.Fato);
        FatoCandidato membro = Declarado("CATEGORIA_RENDA", DominioFato.Categorico, FonteValoresFato.Global, EscopoFato.MembroGrupo, pontoDoMembro);
        FatoCandidato perfil = FatoCandidato.CriarDerivadoDoAdministrador(
            "PERFIL", "PERFIL", null, DominioFato.Booleano, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese).Value!;
        FatoCandidato agregado = FatoCandidato.CriarAgregadoDoAdministrador(
            "CATEGORIAS_RENDA", "CATEGORIAS_RENDA", null, "CATEGORIA_RENDA", new CatalogoDeFatos([membro], []), "INSCRICAO",
            ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese).Value!;
        perfil.DefinirRegrasPadrao([RegraBooleana("RENDA")], new CatalogoDeFatos([renda, perfil], [])).IsSuccess.Should().BeTrue();

        List<FatoCandidato> fatos = [certificado, renda, optou, opcaoPelasCotas, membro, perfil, agregado];
        foreach (FatoCandidato fato in fatos)
        {
            // A identidade é a mesma em todas as leituras; EntityBase só a atribui na criação.
            typeof(EntityBase).GetProperty(nameof(EntityBase.Id))!.SetValue(fato, Ids.TryGetValue(fato.Codigo, out Guid id) ? id : Ids[fato.Codigo] = fato.Id);
        }

        return fatos;
    }

    private static FatoCandidato Declarado(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        FonteValoresFato? fonte = null,
        EscopoFato escopo = EscopoFato.Candidato,
        string ponto = "INSCRICAO") =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar, fonte, null, ponto, escopo,
            ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese).Value!;

    private static RegraDerivacao RegraBooleana(string fato) =>
        RegraDerivacao.CriarBooleana(Quando(fato));

    private static PredicadoDnf Quando(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    private static Modalidade ModalidadeQueCitaOptou() =>
        Modalidade.Criar(
            "PSVR_AMPLA", "Ampla", "ACAO_AFIRMATIVA", "SUPLEMENTAR_AO_TOTAL", null, "DESTINO_UNICO", "AC", null, null,
            PredicadoDnf.CriarDeCondicoesAgrupadas(
            [
                (0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(false)).Value!),
                (0, CondicaoDnf.Criar("OPTOU", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
            ]).Value!, null, null).Value!;

    private static ModeloFormulario ModeloComPrecondicaoSobre(string primeiro, string segundo)
    {
        PredicadoDnf precondicao = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (0, CondicaoDnf.Criar(primeiro, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
            (0, CondicaoDnf.Criar(segundo, Operador.Igual, JsonSerializer.SerializeToElement("x")).Value!),
        ]).Value!;
        EtapaDoModelo secao = new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null);
        EtapaDoModelo revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);
        ItemDoModelo[] itens =
        [
            new(primeiro, 0, "DADOS", primeiro, TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false),
            new(segundo, 1, "DADOS", segundo, TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false),
            new("CERTIFICADO", 2, "DADOS", "Certificado", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, precondicao, [], false),
        ];
        ModeloFormulario modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, null,
            new ConteudoDoModelo("Habilitação", [secao, revisao], itens, [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
        modelo.Ativar();
        return modelo;
    }

    private static ModeloFormulario ModeloComCertificado(bool ativo = true)
    {
        EtapaDoModelo secao = new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null);
        EtapaDoModelo revisao = new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);
        ItemDoModelo item = new("CERTIFICADO", 0, "DADOS", "Certificado", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false);
        ModeloFormulario modelo = ModeloFormulario.Criar(
            "HABILITACAO_MEDICINA", "Habilitação Medicina", null, FinalidadeFormulario.Habilitacao, null,
            new ConteudoDoModelo("Habilitação", [secao, revisao], [item], [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
        if (ativo)
        {
            modelo.Ativar();
        }

        return modelo;
    }
}
