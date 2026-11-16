namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services.FatoDeTesteDaAlteracao;

/// <summary>
/// A alteração dos eixos do fato do administrador (ADR-0136, emenda de 17/10/2026): o subconjunto que
/// o cadastro de cada tipo aceita, as validações do cadastro acumuladas e a coerência com os valores.
/// </summary>
public sealed class AlteracaoDaDefinicaoDoFatoTests
{
    [Fact(DisplayName = "Fato declarado troca domínio, fonte, escopo, ponto de resolução e proteção; código, origem e vínculo ficam")]
    public void Declarado_TrocaOsEixos()
    {
        FatoCandidato fato = Declarado("TEM_RENDA");

        Result resultado = fato.AlterarDefinicao(
            Definicao(DominioFato.Categorico, CardinalidadeFato.Multivalorado, FonteValoresFato.Global, ponto: "HABILITACAO",
                escopo: EscopoFato.MembroGrupo, classificacao: ClassificacaoDeProtecao.Sensivel, finalidade: " Nova finalidade "),
            Catalogo(fato));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Should().BeEquivalentTo(new
        {
            Codigo = "TEM_RENDA",
            Origem = OrigemFato.Declarado,
            Binding = "CAMPO_FORMULARIO:TEM_RENDA",
            Dominio = DominioFato.Categorico,
            Cardinalidade = CardinalidadeFato.Multivalorado,
            FonteValores = FonteValoresFato.Global,
            PontoResolucao = "HABILITACAO",
            Escopo = EscopoFato.MembroGrupo,
            ClassificacaoProtecao = ClassificacaoDeProtecao.Sensivel,
            FinalidadeTratamento = "Nova finalidade",
        });
    }

    [Fact(DisplayName = "Derivado por regra troca o domínio e infere a cardinalidade e a fonte dele")]
    public void Derivado_TrocaODominio()
    {
        FatoCandidato fato = Derivado("PERFIL");

        Result resultado = fato.AlterarDefinicao(
            Definicao(DominioFato.Categorico, ponto: "HABILITACAO", escopo: EscopoFato.Candidato), Catalogo(fato));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.Should().BeEquivalentTo(new
        {
            Dominio = DominioFato.Categorico,
            Cardinalidade = CardinalidadeFato.Multivalorado,
            FonteValores = FonteValoresFato.Global,
            PontoResolucao = "HABILITACAO",
            Binding = "REGRA_DERIVACAO:PERFIL",
        });
    }

    [Fact(DisplayName = "Agregado troca só o ponto de resolução e a proteção; domínio, fonte e escopo continuam os do membro")]
    public void Agregado_TrocaPontoEProtecao()
    {
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);

        Result resultado = agregado.AlterarDefinicao(
            Definicao(dominio: null, ponto: "HABILITACAO", classificacao: ClassificacaoDeProtecao.Sensivel, escopo: null), Catalogo(membro, agregado));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        agregado.Should().BeEquivalentTo(new
        {
            Dominio = DominioFato.Categorico,
            Cardinalidade = CardinalidadeFato.Multivalorado,
            FonteValores = FonteValoresFato.Global,
            Escopo = EscopoFato.Candidato,
            PontoResolucao = "HABILITACAO",
            ClassificacaoProtecao = ClassificacaoDeProtecao.Sensivel,
            Binding = "AGREGACAO_GRUPO:CATEGORIA_RENDA",
        });
    }

    [Fact(DisplayName = "Fato de sistema recusa a alteração dos eixos")]
    public void Sistema_Recusa()
    {
        FatoCandidato sistema = CriacaoDoFato.Criar(
            "COR_RACA", "Cor ou raça", "Descrição do fato.", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, FonteValoresFato.Global,
            null, "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, Finalidade, Hipotese,
            sistema: true).Value!;

        sistema.AlterarDefinicao(Definicao(DominioFato.Booleano), Catalogo(sistema))
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
    }

    [Fact(DisplayName = "As recusas do cadastro acumulam, cada uma no campo do eixo, e o fato fica como estava")]
    public void Declarado_RecusasDoCadastro_AcumulamESemMutar()
    {
        FatoCandidato fato = Declarado("TEM_RENDA");

        Result resultado = fato.AlterarDefinicao(
            new DefinicaoDoFato(
                DominioFato.Texto, CardinalidadeFato.Escalar, FonteValoresFato.Global, FormatoTexto.Nenhum, "INSCRICAO", EscopoFato.Candidato,
                ClassificacaoDeProtecao.Publico, Finalidade, HipoteseLegalTratamento.Consentimento),
            Catalogo(fato));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("fonteValores", FatoCandidatoErrorCodes.FonteValoresForaDeCategorico),
            ("formato", FatoCandidatoErrorCodes.FormatoObrigatorio),
            ("classificacaoProtecao", FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio),
        ]);
        fato.Should().BeEquivalentTo(new { Dominio = DominioFato.Booleano, FonteValores = (FonteValoresFato?)null, ClassificacaoProtecao = ClassificacaoDeProtecao.Pessoal });
    }

    [Fact(DisplayName = "Hipótese legal que não cabe na classificação e formato fora de texto são recusados")]
    public void Declarado_HipoteseEFormato_Recusa()
    {
        FatoCandidato fato = Declarado("TEM_RENDA");

        Result resultado = fato.AlterarDefinicao(
            DeDeclarado(DominioFato.Booleano, FormatoTexto.Cpf, ClassificacaoDeProtecao.Sensivel, HipoteseLegalTratamento.ExecucaoContrato),
            Catalogo(fato));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("formato", FatoCandidatoErrorCodes.FormatoForaDeTexto),
            ("hipoteseLegal", FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao),
        ]);
    }

    [Fact(DisplayName = "Derivado por regra de domínio texto é recusado no domínio")]
    public void Derivado_Texto_Recusa()
    {
        FatoCandidato fato = Derivado("PERFIL");

        fato.AlterarDefinicao(Definicao(DominioFato.Texto, classificacao: ClassificacaoDeProtecao.Pessoal), Catalogo(fato))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "dominio",
                Error = new { Code = FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico },
            });
    }

    [Fact(DisplayName = "O eixo que o tipo do fato infere não se informa")]
    public void EixoInferido_Informado_Recusa()
    {
        FatoCandidato derivado = Derivado("PERFIL");
        FatoCandidato membro = DeMembro("CATEGORIA_RENDA");
        FatoCandidato agregado = Agregado("CATEGORIAS_RENDA", membro);

        derivado.AlterarDefinicao(Definicao(DominioFato.Booleano, cardinalidade: CardinalidadeFato.Multivalorado), Catalogo(derivado))
            .Errors.Should().ContainSingle().Which.Field.Should().Be("cardinalidade");
        agregado.AlterarDefinicao(Definicao(DominioFato.Categorico, escopo: EscopoFato.MembroGrupo), Catalogo(membro, agregado))
            .Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [
                ("dominio", FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato),
                ("escopo", FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato),
            ]);
    }

    [Fact(DisplayName = "Domínio ou fonte que não comporta os valores cadastrados é recusado no campo do eixo")]
    public void Declarado_ComValores_RecusaDominioEFonteQueNaoOsComportam()
    {
        FatoCandidato fato = Declarado("TIPO_RENDA", DominioFato.Categorico, FonteValoresFato.Global);
        fato.AdicionarValorDominio("RURAL", "Rural", 0, ativo: true).IsSuccess.Should().BeTrue();
        fato.DesativarValor("RURAL").IsSuccess.Should().BeTrue();

        fato.AlterarDefinicao(DeDeclarado(DominioFato.Booleano), Catalogo(fato))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "dominio",
                Error = new { Code = FatoCandidatoErrorCodes.DominioNaoComportaOsValores },
            }, "o valor desativado continua no fato, porque o código nunca é removido");
        fato.AlterarDefinicao(Definicao(DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Processo), Catalogo(fato))
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "fonteValores",
                Error = new { Code = FatoCandidatoErrorCodes.FonteNaoComportaOsValores },
            });
    }

    [Fact(DisplayName = "Domínio que deixa de valer as regras padrão do derivado é recusado no domínio")]
    public void Derivado_RegrasPadrao_RecusaDominioQueDeixaDeValer()
    {
        FatoCandidato renda = Declarado("TEM_RENDA");
        FatoCandidato derivado = Derivado("PERFIL");
        derivado.DefinirRegrasPadrao([Booleana(("TEM_RENDA", true))], Catalogo(renda, derivado)).IsSuccess.Should().BeTrue();

        Result resultado = derivado.AlterarDefinicao(Definicao(DominioFato.Categorico), Catalogo(renda, derivado));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("dominio");
        derivado.Dominio.Should().Be(DominioFato.Booleano);
    }
}
