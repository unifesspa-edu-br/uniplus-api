namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O catálogo como as regras o conferem: o descritor de cada fato citável, o domínio dos categóricos
/// que o catálogo não enumera, os fatos do catálogo e as opções que cada campo do modelo oferece.
/// </summary>
public sealed class VocabularioDoCatalogoTests
{
    private const string Finalidade = "Comprovação da renda familiar.";

    [Fact(DisplayName = "O vocabulário do predicado traz os fatos de sim ou não, numérico e de opção do candidato, na ordem do catálogo")]
    public void Descritores_SoOsFatosDoCandidatoQueOPredicadoSabeAvaliar()
    {
        FatoCandidato[] catalogo = CatalogoVariado();

        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDoCatalogo.Descritores(catalogo);

        vocabulario.Keys.Should().Equal("SIM_OU_NAO", "NUMERICO", "CATALOGO", "OPCOES_DO_PROCESSO", "UNIDADES_FEDERATIVAS");
        vocabulario.Values.Select(static descritor => descritor.TipoDominio).Should().Equal(
            TipoDominioFato.Booleano,
            TipoDominioFato.Numerico,
            TipoDominioFato.CategoricoEstatico,
            TipoDominioFato.CategoricoDinamico,
            TipoDominioFato.CategoricoDinamico);
    }

    [Fact(DisplayName = "O descritor do categórico global leva os códigos dos valores e os demais não levam domínio")]
    public void Descritores_CategoricoGlobal_LevaOsCodigosDosValores()
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDoCatalogo.Descritores(CatalogoVariado());

        vocabulario["CATALOGO"].ValoresDominio.Should().Equal("RURAL", "URBANA");
        vocabulario["OPCOES_DO_PROCESSO"].ValoresDominio.Should().BeNull();
        vocabulario["SIM_OU_NAO"].ValoresDominio.Should().BeNull();
    }

    [Fact(DisplayName = "O categórico global sem valores ainda não é citável")]
    public void Descritores_CategoricoGlobalSemValores_FicaDeFora()
    {
        FatoCandidato semValores = Fato("SEM_VALORES", TipoDoDado.Opcao, FonteValoresFato.Catalogo);

        VocabularioDoCatalogo.Descritores([semValores, Fato("SIM_OU_NAO")]).Keys.Should().Equal("SIM_OU_NAO");
    }

    [Fact(DisplayName = "O vocabulário do formulário traz também os fatos de membro de grupo")]
    public void DescritoresDoFormulario_TrazOsFatosDeMembroDeGrupo()
    {
        FatoCandidato[] catalogo = CatalogoVariado();

        VocabularioDoCatalogo.DescritoresDoFormulario(catalogo).Keys.Should().Equal("SIM_OU_NAO", "NUMERICO", "CATALOGO", "OPCOES_DO_PROCESSO", "UNIDADES_FEDERATIVAS", "DO_MEMBRO");
    }

    [Fact(DisplayName = "O domínio dos categóricos: UF enumerada, município por formato, o que só o processo oferece sem restrição")]
    public void DominiosDinamicos_UmDominioPorFontePropriaDoCategorico()
    {
        Dictionary<string, DominioDeValores> dominios = VocabularioDoCatalogo.DominiosDinamicos(CatalogoDeFontes());

        dominios.Keys.Should().Equal("UNIDADES_FEDERATIVAS", "MUNICIPIOS", "OPCOES_DO_PROCESSO", "MODALIDADE", "MUNICIPIOS_DO_FADA", "TIPOS_DE_DEFICIENCIA", "MUNICIPIOS_PROVA_PRESENCIAL");
        dominios["UNIDADES_FEDERATIVAS"].Valores.Should().BeEquivalentTo(ReferenciaCidadeGeo.Ufs);
        dominios["UNIDADES_FEDERATIVAS"].Contem("XX").Should().BeFalse();
        dominios["MUNICIPIOS"].Valores.Should().BeNull();
        dominios["MUNICIPIOS"].Contem("não é município").Should().BeFalse();
    }

    [Theory(DisplayName = "O que só existe no processo ou em outro cadastro admite qualquer código")]
    [InlineData("OPCOES_DO_PROCESSO")]
    [InlineData("MODALIDADE")]
    [InlineData("MUNICIPIOS_DO_FADA")]
    [InlineData("TIPOS_DE_DEFICIENCIA")]
    [InlineData("MUNICIPIOS_PROVA_PRESENCIAL")]
    public void DominiosDinamicos_FonteForaDoCatalogo_AdmiteQualquerCodigo(string fato)
    {
        DominioDeValores dominio = VocabularioDoCatalogo.DominiosDinamicos(CatalogoDeFontes())[fato];

        dominio.Valores.Should().BeNull();
        dominio.Contem("QUALQUER_CODIGO").Should().BeTrue();
    }

    [Fact(DisplayName = "O fato do catálogo para as regras traz os tokens canônicos e os valores com a situação de cada um")]
    public void ParaRegras_TraduzOFatoParaOsTokensDoCatalogo()
    {
        FatoCandidato global = FatoGlobalComValores("CATALOGO", EscopoFato.Candidato, ("URBANA", true), ("RURAL", false));

        FatoDoCatalogo fato = VocabularioDoCatalogo.ParaRegras([global, Fato("SIM_OU_NAO")])["CATALOGO"];

        fato.Should().BeEquivalentTo(new FatoDoCatalogo(
            "CATALOGO", TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, OrigemFato.Declarado, "CAMPO_FORMULARIO:CATALOGO", FonteValoresFato.Catalogo, "CANDIDATO", true,
            [new ValorDoCatalogo("URBANA", true), new ValorDoCatalogo("RURAL", false)], false));
    }

    [Fact(DisplayName = "O fato sem fonte de valores nem valores vai ao catálogo das regras sem fonte e sem valores")]
    public void ParaRegras_FatoBooleano_SemFonteNemValores()
    {
        Dictionary<string, FatoDoCatalogo> fatos = VocabularioDoCatalogo.ParaRegras([Fato("SIM_OU_NAO"), Fato("DO_MEMBRO", escopo: EscopoFato.MembroGrupo)]);

        fatos.Keys.Should().Equal("SIM_OU_NAO", "DO_MEMBRO");
        fatos["SIM_OU_NAO"].FonteValores.Should().BeNull();
        fatos["SIM_OU_NAO"].Valores.Should().BeEmpty();
        fatos["DO_MEMBRO"].Escopo.Should().Be("MEMBRO_GRUPO");
    }

    [Fact(DisplayName = "As opções do campo de categórico global são os valores ativos, por ordem e depois por código")]
    public void OpcoesDoModelo_CategoricoGlobal_ValoresAtivosEmOrdemDeApresentacao()
    {
        FatoCandidato global = Fato("CATALOGO", TipoDoDado.Opcao, FonteValoresFato.Catalogo);
        global.AdicionarValorDominio("B", "Bê", 1, ativo: true).IsSuccess.Should().BeTrue();
        global.AdicionarValorDominio("A", "Á", 1, ativo: true).IsSuccess.Should().BeTrue();
        global.AdicionarValorDominio("C", "Cê", 2, ativo: true).IsSuccess.Should().BeTrue();
        global.AdicionarValorDominio("INATIVO", "Inativo", 0, ativo: false).IsSuccess.Should().BeTrue();
        global.AdicionarValorDominio("Z", "Zê", 0, ativo: true, "Orientação do Z").IsSuccess.Should().BeTrue();

        IReadOnlyList<ValorSelecionavel> opcoes = VocabularioDoCatalogo.OpcoesDoModelo([global], ConteudoCom("CATALOGO"))["CATALOGO"]!;

        opcoes.Select(static opcao => opcao.Codigo).Should().Equal("Z", "A", "B", "C");
        opcoes.Single(static opcao => opcao.Codigo == "Z").Should().Be(new ValorSelecionavel("Z", "Zê", 0, "Orientação do Z"));
    }

    [Fact(DisplayName = "As opções do campo de UF são as unidades federativas do Geo, com a posição de cada uma")]
    public void OpcoesDoModelo_FonteGeoUf_AsUnidadesFederativas()
    {
        FatoCandidato uf = Fato("UF", TipoDoDado.Opcao, FonteValoresFato.UnidadesFederativas);

        IReadOnlyList<ValorSelecionavel> opcoes = VocabularioDoCatalogo.OpcoesDoModelo([uf], ConteudoCom("UF"))["UF"]!;

        opcoes.Should().Equal(ReferenciaCidadeGeo.UnidadesFederativas.Select(static (uf, posicao) => new ValorSelecionavel(uf.Sigla, uf.Nome, posicao)));
    }

    [Fact(DisplayName = "Não há opções do catálogo para o categórico que o processo oferece, para o que não é categórico nem para o código que o catálogo não tem")]
    public void OpcoesDoModelo_SemOpcoesNoCatalogo_Nulas()
    {
        FatoCandidato[] catalogo = [Fato("OPCOES_DO_PROCESSO", TipoDoDado.Opcao, FonteValoresFato.OpcoesDoProcesso), Fato("MUNICIPIO", TipoDoDado.Opcao, FonteValoresFato.Municipios), Fato("SIM_OU_NAO")];

        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = VocabularioDoCatalogo.OpcoesDoModelo(catalogo, ConteudoCom("OPCOES_DO_PROCESSO", "MUNICIPIO", "SIM_OU_NAO", "AUSENTE"));

        opcoes.Keys.Should().Equal("OPCOES_DO_PROCESSO", "MUNICIPIO", "SIM_OU_NAO", "AUSENTE");
        opcoes.Values.Should().AllSatisfy(static opcoesDoCampo => opcoesDoCampo.Should().BeNull());
    }

    [Fact(DisplayName = "As opções do campo de grupo vêm do catálogo como as dos itens, e o agregado oferece as opções do fato de membro")]
    public void OpcoesDoModelo_CampoDeGrupoEAgregado_OpcoesDoFatoDeMembro()
    {
        FatoCandidato membro = FatoGlobalComValores("CATEGORIA_RENDA", EscopoFato.MembroGrupo, ("RURAL", true), ("URBANA", true));
        FatoCandidato agregado = CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato("CATEGORIAS_RENDA_FAMILIA", "Categorias de renda", "Descrição do fato."),
            "CATEGORIA_RENDA",
            new CatalogoDeFatos([membro], []),
            new DefinicaoDoFato(null, null, null, null, "INSCRICAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        ConteudoDoModelo conteudo = ConteudoCom(
            [ConteudosDoModeloDeTeste.Grupo("FAMILIA", 0, null, ConteudosDoModeloDeTeste.Item("CATEGORIA_RENDA"))], "CATEGORIAS_RENDA_FAMILIA");

        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = VocabularioDoCatalogo.OpcoesDoModelo([membro, agregado], conteudo);

        opcoes.Keys.Should().Equal("CATEGORIAS_RENDA_FAMILIA", "CATEGORIA_RENDA");
        opcoes["CATEGORIAS_RENDA_FAMILIA"]!.Select(static opcao => opcao.Codigo).Should().Equal("RURAL", "URBANA");
        opcoes["CATEGORIA_RENDA"]!.Select(static opcao => opcao.Codigo).Should().Equal("RURAL", "URBANA");
    }

    [Fact(DisplayName = "O descritor do agregado categórico leva os códigos dos valores do fato de membro, na ordem em que o membro os declara, ativos ou não")]
    public void Descritores_AgregadoCategorico_ValoresDoFatoDeMembro()
    {
        (FatoCandidato membro, FatoCandidato agregado) = MembroEAgregadoDeRenda();

        Dictionary<string, DescritorFatoCandidato> doPredicado = VocabularioDoCatalogo.Descritores([membro, agregado]);
        Dictionary<string, DescritorFatoCandidato> doFormulario = VocabularioDoCatalogo.DescritoresDoFormulario([membro, agregado]);

        doPredicado.Keys.Should().Equal("CATEGORIAS_RENDA_FAMILIA");
        doPredicado["CATEGORIAS_RENDA_FAMILIA"].ValoresDominio.Should().Equal("RURAL", "URBANA");
        doFormulario.Keys.Should().Equal("CATEGORIA_RENDA", "CATEGORIAS_RENDA_FAMILIA");
        doFormulario["CATEGORIA_RENDA"].ValoresDominio.Should().Equal("RURAL", "URBANA");
        doFormulario["CATEGORIAS_RENDA_FAMILIA"].ValoresDominio.Should().Equal("RURAL", "URBANA");
    }

    [Fact(DisplayName = "O fato do agregado para as regras leva os valores do fato de membro, com a situação de cada um")]
    public void ParaRegras_AgregadoCategorico_ValoresDoFatoDeMembroComASituacao()
    {
        (FatoCandidato membro, FatoCandidato agregado) = MembroEAgregadoDeRenda();

        Dictionary<string, FatoDoCatalogo> fatos = VocabularioDoCatalogo.ParaRegras([membro, agregado]);

        fatos["CATEGORIAS_RENDA_FAMILIA"].Valores.Should().Equal(new ValorDoCatalogo("RURAL", true), new ValorDoCatalogo("URBANA", false));
        fatos["CATEGORIA_RENDA"].Valores.Should().Equal(new ValorDoCatalogo("RURAL", true), new ValorDoCatalogo("URBANA", false));
    }

    [Fact(DisplayName = "A apresentação do fato traz o nome do catálogo e o tipo de campo que o domínio e a fonte dos valores admitem")]
    public void ApresentacaoDos_FatosDoCatalogo_NomeETipoDeCampo()
    {
        FatoCandidato renda = Fato("RENDA_DECLARADA", nome: "Renda declarada");
        FatoCandidato categoria = FatoGlobalComValores("CATEGORIA", EscopoFato.Candidato, ("RURAL", true));
        FatoCandidato idade = Fato("IDADE", TipoDoDado.Numerico);

        Dictionary<string, ApresentacaoDoFato> apresentacoes = VocabularioDoCatalogo.ApresentacaoDos([renda, categoria, idade], ["IDADE", "CATEGORIA", "RENDA_DECLARADA"]);

        apresentacoes.Keys.Should().Equal("IDADE", "CATEGORIA", "RENDA_DECLARADA");
        apresentacoes["RENDA_DECLARADA"].Should().Be(new ApresentacaoDoFato("RENDA_DECLARADA", "Renda declarada", TipoRenderizacao.SimOuNao.Codigo()));
        apresentacoes["CATEGORIA"].Should().Be(new ApresentacaoDoFato("CATEGORIA", "CATEGORIA", TipoRenderizacao.EscolhaUnica.Codigo()));
        apresentacoes["IDADE"].Should().Be(new ApresentacaoDoFato("IDADE", "IDADE", TipoRenderizacao.Numero.Codigo()));
    }

    [Fact(DisplayName = "A apresentação do município do Geo é campo de município, e a das demais fontes de categórico, seleção única")]
    public void ApresentacaoDos_FonteDeValoresDoGeo_TipoDeCampoQueSoOTokenDaFonteProduz()
    {
        FatoCandidato municipio = Fato("MUNICIPIO_RESIDENCIA", TipoDoDado.Opcao, FonteValoresFato.Municipios);
        FatoCandidato uf = Fato("UF_RESIDENCIA", TipoDoDado.Opcao, FonteValoresFato.UnidadesFederativas);
        FatoCandidato opcoesDoProcesso = Fato("OPCAO_DO_PROCESSO", TipoDoDado.Opcao, FonteValoresFato.OpcoesDoProcesso);

        Dictionary<string, ApresentacaoDoFato> apresentacoes = VocabularioDoCatalogo.ApresentacaoDos(
            [municipio, uf, opcoesDoProcesso], ["MUNICIPIO_RESIDENCIA", "UF_RESIDENCIA", "OPCAO_DO_PROCESSO"]);

        apresentacoes["MUNICIPIO_RESIDENCIA"].TipoRenderizacao.Should().Be(TipoRenderizacao.Municipio.Codigo());
        apresentacoes["UF_RESIDENCIA"].TipoRenderizacao.Should().Be(TipoRenderizacao.EscolhaUnica.Codigo());
        apresentacoes["OPCAO_DO_PROCESSO"].TipoRenderizacao.Should().Be(TipoRenderizacao.EscolhaUnica.Codigo());
    }

    [Fact(DisplayName = "A apresentação do fato deixa de fora o código que o catálogo não tem e não repete o código citado mais de uma vez")]
    public void ApresentacaoDos_CodigoAusenteERepetido_SoUmaVezECodigosDoCatalogo()
    {
        FatoCandidato[] catalogo = [Fato("RENDA_DECLARADA"), Fato("IDADE", TipoDoDado.Numerico)];

        Dictionary<string, ApresentacaoDoFato> apresentacoes = VocabularioDoCatalogo.ApresentacaoDos(catalogo, ["RENDA_DECLARADA", "AUSENTE", "IDADE", "RENDA_DECLARADA"]);

        apresentacoes.Keys.Should().Equal("RENDA_DECLARADA", "IDADE");
    }

    [Fact(DisplayName = "Sem códigos citados, não há apresentação")]
    public void ApresentacaoDos_SemCodigos_Vazia()
    {
        VocabularioDoCatalogo.ApresentacaoDos([Fato("RENDA_DECLARADA")], []).Should().BeEmpty();
    }

    private static (FatoCandidato Membro, FatoCandidato Agregado) MembroEAgregadoDeRenda()
    {
        FatoCandidato membro = Fato("CATEGORIA_RENDA", TipoDoDado.Opcao, FonteValoresFato.Catalogo, EscopoFato.MembroGrupo);
        membro.AdicionarValorDominio("RURAL", "Rural", 2, ativo: true).IsSuccess.Should().BeTrue();
        membro.AdicionarValorDominio("URBANA", "Urbana", 1, ativo: false).IsSuccess.Should().BeTrue();
        FatoCandidato agregado = CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato("CATEGORIAS_RENDA_FAMILIA", "Categorias de renda", "Descrição do fato."),
            "CATEGORIA_RENDA",
            new CatalogoDeFatos([membro], []),
            new DefinicaoDoFato(null, null, null, null, "INSCRICAO", null, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        return (membro, agregado);
    }

    private static FatoCandidato[] CatalogoVariado() =>
    [
        Fato("SIM_OU_NAO"),
        Fato("NUMERICO", TipoDoDado.Numerico),
        FatoGlobalComValores("CATALOGO", EscopoFato.Candidato, ("RURAL", true), ("URBANA", true)),
        Fato("OPCOES_DO_PROCESSO", TipoDoDado.Opcao, FonteValoresFato.OpcoesDoProcesso),
        Fato("UNIDADES_FEDERATIVAS", TipoDoDado.Opcao, FonteValoresFato.UnidadesFederativas),
        Fato("TEXTO", TipoDoDado.Texto),
        Fato("DO_MEMBRO", escopo: EscopoFato.MembroGrupo),
    ];

    private static FatoCandidato[] CatalogoDeFontes() =>
    [
        Fato("UNIDADES_FEDERATIVAS", TipoDoDado.Opcao, FonteValoresFato.UnidadesFederativas),
        Fato("MUNICIPIOS", TipoDoDado.Opcao, FonteValoresFato.Municipios),
        Fato("CATALOGO", TipoDoDado.Opcao, FonteValoresFato.Catalogo),
        Fato("OPCOES_DO_PROCESSO", TipoDoDado.Opcao, FonteValoresFato.OpcoesDoProcesso),
        Fato("MODALIDADE", TipoDoDado.Opcao, FonteValoresFato.Modalidade),
        Fato("MUNICIPIOS_DO_FADA", TipoDoDado.Opcao, FonteValoresFato.MunicipiosDoFada),
        Fato("TIPOS_DE_DEFICIENCIA", TipoDoDado.Opcao, FonteValoresFato.TiposDeDeficiencia),
        Fato("MUNICIPIOS_PROVA_PRESENCIAL", TipoDoDado.Opcao, FonteValoresFato.MunicipiosProvaPresencial),
        Fato("SIM_OU_NAO"),
    ];

    private static ConteudoDoModelo ConteudoCom(params string[] campos) => ConteudoCom([], campos);

    private static ConteudoDoModelo ConteudoCom(IReadOnlyList<GrupoDoModelo> grupos, params string[] campos) =>
        new(null, [], [.. campos.Select(static campo => ConteudosDoModeloDeTeste.Item(campo))], [], [], grupos);

    private static FatoCandidato FatoGlobalComValores(string codigo, EscopoFato escopo, params (string Codigo, bool Ativo)[] valores)
    {
        FatoCandidato fato = Fato(codigo, TipoDoDado.Opcao, FonteValoresFato.Catalogo, escopo);
        for (int ordem = 0; ordem < valores.Length; ordem++)
        {
            fato.AdicionarValorDominio(valores[ordem].Codigo, valores[ordem].Codigo, ordem + 1, valores[ordem].Ativo).IsSuccess.Should().BeTrue();
        }

        return fato;
    }

    private static FatoCandidato Fato(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        FonteValoresFato? fonte = null,
        EscopoFato escopo = EscopoFato.Candidato,
        string? nome = null) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, nome ?? codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, fonte, dominio == TipoDoDado.Texto ? FormatoTexto.Livre : null, "INSCRICAO", escopo, ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
}
