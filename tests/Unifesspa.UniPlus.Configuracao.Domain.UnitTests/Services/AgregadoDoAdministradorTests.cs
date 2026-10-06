namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O cadastro do agregado sobre grupo repetível (ADR-0138, UNI-REQ-0146): a forma sai do fato de
/// membro, e o catálogo recusa o que não é fato declarado de membro de domínio que agrega, o fato
/// desativado, a proteção mais fraca e a resolução anterior à dele.
/// </summary>
public sealed class AgregadoDoAdministradorTests
{
    private const string Finalidade = "Comprovação da renda familiar.";
    private const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;

    [Fact]
    public void CriarAgregado_SobreFatoBooleano_EhExisteBooleanoComVinculoAoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA");

        FatoCandidato agregado = Criar("EXISTE_MENOR_SOB_GUARDA", "MENOR_SOB_GUARDA", membro).Value!;

        agregado.Dominio.Should().Be(DominioFato.Booleano);
        agregado.Cardinalidade.Should().Be(CardinalidadeFato.Escalar);
        agregado.Escopo.Should().Be(EscopoFato.Candidato);
        agregado.Binding.Should().Be("AGREGACAO_GRUPO:MENOR_SOB_GUARDA");
        agregado.FatoDeMembroAgregado.Should().Be("MENOR_SOB_GUARDA");
    }

    [Fact]
    public void CriarAgregado_SobreFatoCategorico_EhValoresPresentesComAFonteDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", DominioFato.Categorico);

        FatoCandidato agregado = Criar("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA", membro).Value!;

        agregado.Dominio.Should().Be(DominioFato.Categorico);
        agregado.Cardinalidade.Should().Be(CardinalidadeFato.Multivalorado);
        agregado.FonteValores.Should().Be(FonteValoresFato.Processo);
    }

    [Theory]
    [InlineData("INEXISTENTE", FatoCandidatoErrorCodes.AgregadoSemFatoDeMembro)]
    [InlineData("RENDA_DECLARADA", FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo)]
    [InlineData("IDADE_DO_MEMBRO", FatoCandidatoErrorCodes.AgregadoSobreDominioQueNaoAgrega)]
    public void CriarAgregado_FatoDeMembroInvalido_Recusa(string fatoDeMembro, string esperado)
    {
        FatoCandidato doCandidato = FatoCandidato.CriarDoAdministrador(
            "RENDA_DECLARADA", "Renda", null, DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO",
            EscopoFato.Candidato, ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        FatoCandidato numerico = DeMembro("IDADE_DO_MEMBRO", DominioFato.Numerico);

        Result<FatoCandidato> agregado = Criar("AGREGADO", fatoDeMembro, doCandidato, numerico);

        agregado.Errors.Select(static e => e.Error.Code).Should().Equal(esperado);
    }

    [Fact]
    public void CriarAgregado_SobreFatoDoCandidatoMaisProtegido_RecusaSoPeloEscopo()
    {
        FatoCandidato doCandidato = FatoCandidato.CriarDoAdministrador(
            "RENDA_DECLARADA", "Renda", null, DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "HABILITACAO",
            EscopoFato.Candidato, ClassificacaoProtecaoDado.Sensivel, Finalidade, Hipotese).Value!;

        Criar("AGREGADO", "RENDA_DECLARADA", doCandidato).Errors.Select(static e => e.Error.Code)
            .Should().Equal(FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo);
    }

    [Fact]
    public void CriarAgregado_FatoDeMembroDesativado_RecusaVinculoNovo()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA");
        membro.Desativar().IsSuccess.Should().BeTrue();

        Criar("EXISTE_MENOR", "MENOR_SOB_GUARDA", membro).Errors.Select(static e => e.Error.Code)
            .Should().Equal(VinculoCatalogoErrorCodes.FatoDesativado);
    }

    [Fact]
    public void CriarAgregado_ProtecaoMaisFracaQueADoFatoDeMembro_Recusa()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA", classificacao: ClassificacaoProtecaoDado.Sensivel);

        Criar("EXISTE_MENOR", "MENOR_SOB_GUARDA", membro).Errors.Select(static e => e.Error.Code)
            .Should().Equal(FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia);
    }

    [Fact]
    public void CriarAgregado_ResolvendoAntesDoFatoDeMembro_Recusa()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA", ponto: "HABILITACAO");
        PrecedenciaFase[] precedencias = [PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!];

        Result<FatoCandidato> agregado = FatoCandidato.CriarAgregadoDoAdministrador(
            "EXISTE_MENOR", "Existe menor", null, "MENOR_SOB_GUARDA", new CatalogoDeFatos([membro], precedencias), "INSCRICAO",
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese);

        agregado.Errors.Select(static e => e.Error.Code).Should().Equal(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia);
    }

    [Fact]
    public void CriarAgregado_SemFatoDeMembroENomeVazio_AcumulaSemRecusasDoDominio()
    {
        Result<FatoCandidato> agregado = FatoCandidato.CriarAgregadoDoAdministrador(
            "AGREGADO", " ", null, "INEXISTENTE", new CatalogoDeFatos([], []), "INSCRICAO",
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese);

        agregado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["fatoDeMembro", "nome"]);
    }

    [Fact]
    public void CriarAgregado_NomeVazioEDependenciasIncoerentes_AcumulaTodas()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA", ponto: "HABILITACAO", classificacao: ClassificacaoProtecaoDado.Sensivel);
        PrecedenciaFase[] precedencias = [PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!];

        Result<FatoCandidato> agregado = FatoCandidato.CriarAgregadoDoAdministrador(
            "EXISTE_MENOR", " ", null, "MENOR_SOB_GUARDA", new CatalogoDeFatos([membro], precedencias), "INSCRICAO",
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese);

        agregado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia),
            ("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia),
        ]);
    }

    [Fact]
    public void AdicionarValorDominio_NoAgregado_RecusaPorqueOsValoresSaoDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", DominioFato.Categorico, fonte: FonteValoresFato.Global);
        FatoCandidato agregado = Criar("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA", membro).Value!;

        agregado.AdicionarValorDominio("RURAL", null, 0, ativo: true).Error!.Code.Should().Be(FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios);
    }

    [Fact]
    public void RegraSobreAgregadoCategorico_UsaOsValoresDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", DominioFato.Categorico, ponto: "INSCRICAO", fonte: FonteValoresFato.Global);
        membro.AdicionarValorDominio("RURAL", "Trabalho rural", 0, ativo: true).IsSuccess.Should().BeTrue();
        FatoCandidato agregado = FatoCandidato.CriarAgregadoDoAdministrador(
            "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda", null, "CATEGORIA_RENDA", new CatalogoDeFatos([membro], []), "INSCRICAO",
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        FatoCandidato derivado = FatoCandidato.CriarDerivadoDoAdministrador(
            "TEM_RENDA_RURAL", "Tem renda rural", null, DominioFato.Booleano, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        RegraDerivacao regra = RegraDerivacao.CriarBooleana(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CATEGORIAS_RENDA_FAMILIA", Operador.Em, System.Text.Json.JsonSerializer.SerializeToElement(new[] { "RURAL" })).Value!)]).Value!);

        Result resultado = derivado.DefinirRegrasPadrao([regra], new CatalogoDeFatos([membro, agregado, derivado], []));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        VocabularioDoCatalogo.ParaRegras([membro, agregado])["CATEGORIAS_RENDA_FAMILIA"].Valores.Select(static v => v.Codigo).Should().Equal("RURAL");
    }

    [Fact]
    public void RegraSobreAgregado_CitandoValorDesativadoNoFatoDeMembro_Recusa()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", DominioFato.Categorico, ponto: "INSCRICAO", fonte: FonteValoresFato.Global);
        membro.AdicionarValorDominio("RURAL", "Trabalho rural", 0, ativo: true).IsSuccess.Should().BeTrue();
        membro.DesativarValor("RURAL").IsSuccess.Should().BeTrue();
        FatoCandidato agregado = FatoCandidato.CriarAgregadoDoAdministrador(
            "CATEGORIAS_RENDA_FAMILIA", "Categorias de renda", null, "CATEGORIA_RENDA", new CatalogoDeFatos([membro], []), "INSCRICAO",
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        FatoCandidato derivado = FatoCandidato.CriarDerivadoDoAdministrador(
            "TEM_RENDA_RURAL", "Tem renda rural", null, DominioFato.Booleano, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese).Value!;
        RegraDerivacao regra = RegraDerivacao.CriarBooleana(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CATEGORIAS_RENDA_FAMILIA", Operador.Em, System.Text.Json.JsonSerializer.SerializeToElement(new[] { "RURAL" })).Value!)]).Value!);

        Result resultado = derivado.DefinirRegrasPadrao([regra], new CatalogoDeFatos([membro, agregado, derivado], []));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(FatoCandidatoErrorCodes.RegraCitaValorDesativado);
    }

    [Fact(DisplayName = "O modelo leva os agregados do catálogo cujo fato de membro é campo de um grupo dele, com a operação do domínio do membro")]
    public void AgregadosDosGrupos_SoOsQueAgregamCampoDeGrupoDoModelo()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA");
        FatoCandidato foraDoModelo = DeMembro("TRABALHA_NO_CAMPO");
        FatoCandidato agregado = Criar("EXISTE_MENOR_SOB_GUARDA", "MENOR_SOB_GUARDA", membro).Value!;
        FatoCandidato outro = Criar("EXISTE_TRABALHADOR_RURAL", "TRABALHA_NO_CAMPO", foraDoModelo).Value!;
        GrupoDoModelo familia = new(
            "COMPOSICAO_FAMILIAR", 0, "DADOS", "Composição familiar", 0, null, null, Regras.Formularios.Obrigatoriedade.Nunca,
            [new ItemDoModelo("MENOR_SOB_GUARDA", 0, null, "Menor sob guarda", TipoRenderizacao.Booleano, null, null,
                Regras.Formularios.Obrigatoriedade.Sempre, null, [], false)]);

        IReadOnlyList<Regras.Formularios.DefinicaoAgregado> agregados = VocabularioDoCatalogo.AgregadosDosGrupos([membro, foraDoModelo, agregado, outro], [familia]);

        agregados.Should().ContainSingle().Which.Should().Be(new Regras.Formularios.DefinicaoAgregado(
            "EXISTE_MENOR_SOB_GUARDA", "COMPOSICAO_FAMILIAR", "MENOR_SOB_GUARDA", Regras.Formularios.OperacaoAgregado.Existe));
    }

    private static Result<FatoCandidato> Criar(string codigo, string fatoDeMembro, params FatoCandidato[] catalogo) =>
        FatoCandidato.CriarAgregadoDoAdministrador(
            codigo, codigo, null, fatoDeMembro, new CatalogoDeFatos(catalogo, []), "HABILITACAO",
            ClassificacaoProtecaoDado.Pessoal, Finalidade, Hipotese);

    private static FatoCandidato DeMembro(
        string codigo,
        DominioFato dominio = DominioFato.Booleano,
        string ponto = "HABILITACAO",
        ClassificacaoProtecaoDado classificacao = ClassificacaoProtecaoDado.Pessoal,
        FonteValoresFato fonte = FonteValoresFato.Processo) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, dominio, CardinalidadeFato.Escalar, dominio == DominioFato.Categorico ? fonte : null, null, ponto,
            EscopoFato.MembroGrupo, classificacao, Finalidade, Hipotese).Value!;
}
