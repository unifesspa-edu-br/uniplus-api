namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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

        agregado.Dominio.Should().Be(TipoDoDado.SimOuNao);
        agregado.Cardinalidade.Should().Be(QuantidadeDeValores.ValorUnico);
        agregado.Escopo.Should().Be(EscopoFato.Candidato);
        agregado.Binding.Should().Be("RESUMO_DE_GRUPO:MENOR_SOB_GUARDA");
        agregado.FatoDeMembroAgregado.Should().Be("MENOR_SOB_GUARDA");
    }

    [Fact]
    public void CriarAgregado_SobreFatoCategorico_EhValoresPresentesComAFonteDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", TipoDoDado.Opcao);

        FatoCandidato agregado = Criar("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA", membro).Value!;

        agregado.Dominio.Should().Be(TipoDoDado.Opcao);
        agregado.Cardinalidade.Should().Be(QuantidadeDeValores.VariosValores);
        agregado.FonteValores.Should().Be(FonteValoresFato.OpcoesDoProcesso);
    }

    [Fact]
    public void CriarAgregado_FatoForaDoCatalogo_NaoRepeteCodigo()
    {
        FieldError erro = Criar("AGREGADO", "FATO_DE_TESTE").Errors.Single();

        erro.Field.Should().Be("fatoDeMembro");
        erro.Error.Code.Should().Be(FatoCandidatoErrorCodes.AgregadoSemFatoDeMembro);
        erro.Error.Message.Should().NotContain("FATO_DE_TESTE");
    }

    [Fact]
    public void CriarAgregado_FatoQueNaoEhDeMembro_NaoRepeteCodigo()
    {
        FatoCandidato fato = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("FATO_DE_TESTE", "Fato", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
        FieldError erro = Criar("AGREGADO", "FATO_DE_TESTE", fato).Errors.Single();

        erro.Field.Should().Be("fatoDeMembro");
        erro.Error.Code.Should().Be(FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo);
        erro.Error.Message.Should().NotContain("FATO_DE_TESTE");
    }

    [Fact]
    public void CriarAgregado_FatoDeMembroDesativado_NomeiaOFatoSemRepetirOCodigo()
    {
        FatoCandidato fato = DeMembro("FATO_DE_TESTE", nome: "Renda de cada membro");
        fato.Desativar().IsSuccess.Should().BeTrue();
        FieldError erro = Criar("AGREGADO", "FATO_DE_TESTE", fato).Errors.Single();

        erro.Field.Should().Be("fatoDeMembro");
        erro.Error.Code.Should().Be(VinculoCatalogoErrorCodes.FatoDesativado);
        erro.Error.Message.Should().Be("O dado do candidato 'Renda de cada membro', de membro de grupo, está desativado no catálogo e não aceita vínculo novo.");
    }

    [Theory]
    [InlineData("INEXISTENTE", FatoCandidatoErrorCodes.AgregadoSemFatoDeMembro)]
    [InlineData("RENDA_DECLARADA", FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo)]
    [InlineData("IDADE_DO_MEMBRO", FatoCandidatoErrorCodes.AgregadoSobreDominioQueNaoAgrega)]
    public void CriarAgregado_FatoDeMembroInvalido_Recusa(string fatoDeMembro, string esperado)
    {
        FatoCandidato doCandidato = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("RENDA_DECLARADA", "Renda", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
        FatoCandidato numerico = DeMembro("IDADE_DO_MEMBRO", TipoDoDado.Numerico);

        Result<FatoCandidato> agregado = Criar("AGREGADO", fatoDeMembro, doCandidato, numerico);

        agregado.Errors.Select(static e => e.Error.Code).Should().Equal(esperado);
    }

    [Fact]
    public void CriarAgregado_SobreFatoDoCandidatoMaisProtegido_RecusaSoPeloEscopo()
    {
        FatoCandidato doCandidato = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato("RENDA_DECLARADA", "Renda", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "HABILITACAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese)).Value!;

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
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA", classificacao: ClassificacaoDeProtecao.Sensivel);

        Criar("EXISTE_MENOR", "MENOR_SOB_GUARDA", membro).Errors.Select(static e => e.Error.Code)
            .Should().Equal(FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia);
    }

    [Fact]
    public void CriarAgregado_ResolvendoAntesDoFatoDeMembro_Recusa()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA", ponto: "HABILITACAO");
        PrecedenciaFase[] precedencias = [PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!];

        Result<FatoCandidato> agregado = CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato("EXISTE_MENOR", "Existe menor", "Descrição do fato."),
            "MENOR_SOB_GUARDA",
            new CatalogoDeFatos([membro], precedencias),
            new DefinicaoDoFato(null, null, null, null, "INSCRICAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese));

        agregado.Errors.Select(static e => e.Error.Code).Should().Equal(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia);
    }

    [Fact]
    public void CriarAgregado_SemFatoDeMembroENomeVazio_AcumulaSemRecusasDoDominio()
    {
        Result<FatoCandidato> agregado = CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato("AGREGADO", " ", "Descrição do fato."),
            "INEXISTENTE",
            new CatalogoDeFatos([], []),
            new DefinicaoDoFato(null, null, null, null, "INSCRICAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese));

        agregado.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["fatoDeMembro", "nome"]);
    }

    [Fact]
    public void CriarAgregado_NomeVazioEDependenciasIncoerentes_AcumulaTodas()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA", ponto: "HABILITACAO", classificacao: ClassificacaoDeProtecao.Sensivel);
        PrecedenciaFase[] precedencias = [PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!];

        Result<FatoCandidato> agregado = CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato("EXISTE_MENOR", " ", "Descrição do fato."),
            "MENOR_SOB_GUARDA",
            new CatalogoDeFatos([membro], precedencias),
            new DefinicaoDoFato(null, null, null, null, "INSCRICAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese));

        agregado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia),
            ("pontoResolucao", FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia),
        ]);
    }

    [Fact]
    public void AdicionarValorDominio_NoAgregado_RecusaPorqueOsValoresSaoDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", TipoDoDado.Opcao, fonte: FonteValoresFato.Catalogo);
        FatoCandidato agregado = Criar("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA", membro).Value!;

        agregado.AdicionarValorDominio("RURAL", null, 0, ativo: true).Error!.Code.Should().Be(FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios);
    }

    [Fact]
    public void CorrigirEReordenarValores_NoAgregado_RecusaPorqueOsValoresSaoDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", TipoDoDado.Opcao, fonte: FonteValoresFato.Catalogo);
        FatoCandidato agregado = Criar("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA", membro).Value!;

        agregado.CorrigirValor("RURAL", "Rural", null).Error!.Code.Should().Be(FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios);
        agregado.ReordenarValores([]).Error!.Code.Should().Be(FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios);
    }

    [Fact]
    public void RegraSobreAgregadoCategorico_UsaOsValoresDoFatoDeMembro()
    {
        FatoCandidato membro = DeMembroCategoricoComValorRural();
        FatoCandidato agregado = AgregadoDeCategorias(membro);

        Result resultado = DefinirRegraQueCitaRural(membro, agregado);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        VocabularioDoCatalogo.ParaRegras([membro, agregado])["CATEGORIAS_RENDA_FAMILIA"].Valores.Select(static v => v.Codigo).Should().Equal("RURAL");
    }

    [Fact]
    public void RegraSobreAgregado_CitandoValorDesativadoNoFatoDeMembro_Recusa()
    {
        FatoCandidato membro = DeMembroCategoricoComValorRural();
        membro.DesativarValor("RURAL").IsSuccess.Should().BeTrue();
        FatoCandidato agregado = AgregadoDeCategorias(membro);

        Result resultado = DefinirRegraQueCitaRural(membro, agregado);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(FatoCandidatoErrorCodes.RegraCitaValorDesativado);
    }

    [Fact(DisplayName = "O modelo leva os agregados do catálogo cujo fato de membro é campo de um grupo dele, com a operação do domínio do membro")]
    public void AgregadosDosGrupos_SoOsQueAgregamCampoDeGrupoDoModelo()
    {
        FatoCandidato membro = DeMembro("MENOR_SOB_GUARDA");
        FatoCandidato foraDoModelo = DeMembro("TRABALHA_NO_CAMPO");
        FatoCandidato agregado = Criar("EXISTE_MENOR_SOB_GUARDA", "MENOR_SOB_GUARDA", membro).Value!;
        FatoCandidato outro = Criar("EXISTE_TRABALHADOR_RURAL", "TRABALHA_NO_CAMPO", foraDoModelo).Value!;
        GrupoDoModelo familia = ComposicaoFamiliarComMenorSobGuarda();

        IReadOnlyList<Regras.Formularios.DefinicaoAgregado> agregados = VocabularioDoCatalogo.AgregadosDosGrupos([membro, foraDoModelo, agregado, outro], [familia]);

        agregados.Should().ContainSingle().Which.Should().Be(new Regras.Formularios.DefinicaoAgregado(
            "EXISTE_MENOR_SOB_GUARDA", "COMPOSICAO_FAMILIAR", "MENOR_SOB_GUARDA", Regras.Formularios.OperacaoAgregado.Existe));
    }

    private static GrupoDoModelo ComposicaoFamiliarComMenorSobGuarda() => new(
        "COMPOSICAO_FAMILIAR", 0, "DADOS", "Composição familiar", new Regras.Formularios.OcorrenciasDoGrupo(0, null, IncluiCandidato: false),
        new Regras.Formularios.ExibicaoEObrigatoriedade(null, Regras.Formularios.Obrigatoriedade.Nunca),
        [new ItemDoModelo(
            "MENOR_SOB_GUARDA",
            0,
            null,
            new Regras.Formularios.ApresentacaoDoCampo("Menor sob guarda", TipoDeCampo.SimOuNao),
            null,
            new Regras.Formularios.RegrasDoCampo(null, Regras.Formularios.Obrigatoriedade.Sempre, []))]);

    private static FatoCandidato DeMembroCategoricoComValorRural()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA", TipoDoDado.Opcao, ponto: "INSCRICAO", fonte: FonteValoresFato.Catalogo);
        membro.AdicionarValorDominio("RURAL", "Trabalho rural", 0, ativo: true).IsSuccess.Should().BeTrue();
        return membro;
    }

    private static FatoCandidato AgregadoDeCategorias(FatoCandidato membro) =>
        CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato("CATEGORIAS_RENDA_FAMILIA", "Categorias de renda", "Descrição do fato."),
            "CATEGORIA_RENDA",
            new CatalogoDeFatos([membro], []),
            new DefinicaoDoFato(null, null, null, null, "INSCRICAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;

    private static Result DefinirRegraQueCitaRural(FatoCandidato membro, FatoCandidato agregado)
    {
        FatoCandidato derivado = CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato("TEM_RENDA_RURAL", "Tem renda rural", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, null, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese)).Value!;
        RegraDerivacao regra = RegraDerivacao.CriarBooleana(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("CATEGORIAS_RENDA_FAMILIA", Operador.AlgumDe, System.Text.Json.JsonSerializer.SerializeToElement(new[] { "RURAL" })).Value!)]).Value!);

        return derivado.DefinirRegrasPadrao([regra], new CatalogoDeFatos([membro, agregado, derivado], []));
    }

    private static Result<FatoCandidato> Criar(string codigo, string fatoDeMembro, params FatoCandidato[] catalogo) =>
        CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            fatoDeMembro,
            new CatalogoDeFatos(catalogo, []),
            new DefinicaoDoFato(null, null, null, null, "HABILITACAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, Hipotese));

    private static FatoCandidato DeMembro(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        string ponto = "HABILITACAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        FonteValoresFato fonte = FonteValoresFato.OpcoesDoProcesso,
        string? nome = null) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, nome ?? codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, dominio == TipoDoDado.Opcao ? fonte : null, null, ponto, EscopoFato.MembroGrupo, classificacao, Finalidade, Hipotese)).Value!;
}
