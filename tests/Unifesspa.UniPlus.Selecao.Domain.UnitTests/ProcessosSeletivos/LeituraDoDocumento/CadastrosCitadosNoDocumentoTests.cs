namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>Os cadastros citados são um valor lido uma vez, num instante único: só têm o que o cadastro achou, e o código ausente é o que o cadastro não tem.</summary>
public sealed class CadastrosCitadosNoDocumentoTests
{
    [Fact(DisplayName = "Os cadastros guardam o instante único da leitura e só o que o cadastro achou, pelo código")]
    public void Cadastros_GuardamOInstanteEOQueFoiAchado()
    {
        CadastrosCitadosNoDocumento cadastros = Cadastros();

        cadastros.LidoEm.Should().Be(LidoEm);
        cadastros.Unidades.Keys.Should().BeEquivalentTo("CEPS", "CGA");
        cadastros.TiposDeProcesso.Keys.Should().Equal("SISU");
        cadastros.FasesCanonicas.Keys.Should().BeEquivalentTo("INSCRICAO", "RECURSO", "RESULTADO");
        cadastros.TiposDeBanca.Keys.Should().Equal("ANALISE_RECURSOS");
        cadastros.CategoriasDeDocumento.Keys.Should().Equal("IDENTIDADE");
        cadastros.Localidades.Keys.Should().Equal("1504208");
        cadastros.Regras.Keys.Should().HaveCount(3);
        cadastros.TiposDeAto.Should().BeEquivalentTo(AtoDoResultado, AtoDoDefinitivo);
    }

    [Fact(DisplayName = "O cadastro nulo é defeito de quem monta e não vira cadastro vazio")]
    public void Cadastro_Nulo_Lanca()
    {
        Action montar = () => _ = new CadastrosCitadosNoDocumento(LidoEm) { Unidades = null! };

        montar.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Os cadastros que o documento não alimenta nascem vazios, e o código ausente não é achado")]
    public void Cadastros_SemEntrada_NascemVazios()
    {
        CadastrosCitadosNoDocumento vazios = new(LidoEm);

        vazios.Unidades.Should().BeEmpty();
        vazios.Regras.Should().BeEmpty();
        vazios.TiposDeAto.Should().BeEmpty();
        vazios.DadosDoDesempate.Should().BeEmpty();
        Cadastros().Unidades.ContainsKey("INEXISTENTE").Should().BeFalse();
    }

    [Fact(DisplayName = "O tipo de etapa e o quadro de pesos citados se acham pelo código e pela resolução, e o que o cadastro não tem não é achado")]
    public void TipoDeEtapaEQuadroDePesos_SeAchamPeloQueFoiCitado()
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        QuadroDePesosCitado quadro = new([], ["Ampla concorrência"]);
        CadastrosCitadosNoDocumento cadastros = new(LidoEm)
        {
            TiposDeEtapa = new Dictionary<string, TipoEtapaSnapshot> { ["PROVA_OBJETIVA"] = tipo },
            QuadrosDePesos = new Dictionary<string, QuadroDePesosCitado> { ["RESOLUCAO 1/2026"] = quadro },
        };

        cadastros.TipoDeEtapa("PROVA_OBJETIVA").Should().BeSameAs(tipo);
        cadastros.TipoDeEtapa("PROVA_ORAL").Should().BeNull();
        cadastros.QuadroDePesos("RESOLUCAO 1/2026").Should().BeSameAs(quadro);
        cadastros.QuadroDePesos("RESOLUCAO 2/2026").Should().BeNull();
        quadro.Completo.Should().BeFalse();
        new QuadroDePesosCitado([], []).Completo.Should().BeTrue();
    }

    [Fact(DisplayName = "Os cadastros de tipos de etapa e de quadros de pesos que o documento não alimenta nascem vazios")]
    public void TiposDeEtapaEQuadros_SemEntrada_NascemVazios()
    {
        CadastrosCitadosNoDocumento vazios = new(LidoEm);

        vazios.TiposDeEtapa.Should().BeEmpty();
        vazios.QuadrosDePesos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os cadastros das vagas e das exigências nascem vazios, e o código ausente não é achado em nenhum deles")]
    public void CadastrosDasVagasEDasExigencias_SemEntrada_NascemVazios()
    {
        CadastrosCitadosNoDocumento vazios = new(LidoEm);

        vazios.VagasEExigencias.OfertasDeCurso.Should().BeEmpty();
        vazios.OfertaDeCurso("OFERTA").Should().BeNull();
        vazios.Modalidade("AMPLA").Should().BeNull();
        vazios.ReferenciaDemografica("CENSO_2022").Should().BeNull();
        vazios.CondicaoDeAtendimento("PCD").Should().BeNull();
        vazios.RecursoDeAcessibilidade("LIBRAS").Should().BeNull();
        vazios.TipoDeDeficiencia("VISUAL").Should().BeNull();
        vazios.TipoDeDocumento("RG").Should().BeNull();
        vazios.BaseLegalDoFada("PORTARIA").Should().BeNull();
    }

    [Fact(DisplayName = "Cada cadastro das vagas devolve só o que foi citado e achado, pelo código como foi escrito")]
    public void CadastrosDasVagasEDasExigencias_DevolvemOQueFoiAchado()
    {
        OpcaoDeAtendimentoCitada condicao = new(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaDeCursoCitada oferta = new(Guid.CreateVersion7(), "OFERTA", "Curso", "BACHARELADO", "Unidade", ["NOTURNO"], 40, null, null);
        CadastrosCitadosNoDocumento cadastros = new(LidoEm)
        {
            VagasEExigencias = new()
            {
                OfertasDeCurso = new Dictionary<string, OfertaDeCursoCitada> { ["OFERTA"] = oferta },
                CondicoesDeAtendimento = new Dictionary<string, OpcaoDeAtendimentoCitada> { ["PCD"] = condicao },
            },
        };

        cadastros.OfertaDeCurso("OFERTA").Should().BeSameAs(oferta);
        cadastros.OfertaDeCurso("oferta").Should().BeNull();
        cadastros.CondicaoDeAtendimento("PCD").Should().BeSameAs(condicao);
        cadastros.RecursoDeAcessibilidade("PCD").Should().BeNull();
    }

    [Fact(DisplayName = "O cadastro das vagas nulo é defeito de quem monta e não vira cadastro vazio")]
    public void CadastroDasVagas_Nulo_Lanca()
    {
        Action montar = () => _ = new CadastrosCitadosNoDocumento(LidoEm) { VagasEExigencias = null! };

        montar.Should().Throw<ArgumentNullException>();
    }
}
