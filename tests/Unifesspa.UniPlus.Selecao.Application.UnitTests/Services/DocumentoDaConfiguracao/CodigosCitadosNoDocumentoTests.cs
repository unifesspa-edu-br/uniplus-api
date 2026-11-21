namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os códigos que o documento cita saem sem repetição, por cadastro, e a citação que não tem código e versão não consulta regra.</summary>
public sealed class CodigosCitadosNoDocumentoTests
{
    private static readonly IdentificacaoNoDocumento Identificacao =
        new("Processo de Teste", "SiSU", "CEPS", OrigemCandidatos.ImportacaoExterna, "1504208", null, new("CONTAGEM", "v1"));

    private static readonly ParametrosDeclaradosDoRecurso Prazo = new(new PrazoDeclarado(1m, "horas"), false, PrazoDeclarado.Ausente, "semEfeitoSuspensivo", null);

    private static FaseNoDocumento Fase(int posicao, string faseCanonica, string? unidade = null) =>
        new(posicao, $"fase-{posicao}", new(faseCanonica, unidade, null), new(null, null), new([], false, null, null), [], null);

    private static FaseNoDocumento ComProdutos(FaseNoDocumento fase, params string[] atos) =>
        fase with { Publicacoes = fase.Publicacoes with { Produtos = [.. atos.Select(static (ato, posicao) => new ProdutoNoDocumento(posicao, ato, null))] } };

    private static FaseNoDocumento ComBancas(FaseNoDocumento fase, params BancaNoDocumento[] bancas) => fase with { Bancas = bancas };

    private static BancaNoDocumento Banca(int posicao, string tipo, params string[] categorias) => new(posicao, $"banca-{posicao}", tipo, categorias, null);

    private static FaseNoDocumento ComRegraDeRecurso(FaseNoDocumento fase, ReferenciaDeRegraNoDocumento regra, params ProcedimentoNoDocumento[] procedimentos) =>
        fase with { RegraDeRecurso = new(regra, null, Prazo, procedimentos) };

    private static ProcedimentoNoDocumento Procedimento(int posicao, ReferenciaDeRegraNoDocumento regra) =>
        new(posicao, TipoMotivoDeDecisao.AnaliseDocumental, null, regra);

    private static CodigosCitadosPorCadastro Codigos(IdentificacaoNoDocumento identificacao, params FaseNoDocumento[] fases) =>
        CodigosCitadosNoDocumento.Em(new DeclaracoesDoDocumento(identificacao, fases));

    [Fact(DisplayName = "A identificação cita um código em cada cadastro de que depende, e a fase só o da fase canônica")]
    public void Identificacao_CitaUmCodigoPorCadastro()
    {
        CodigosCitadosPorCadastro codigos = Codigos(Identificacao, Fase(0, "INSCRICAO"));

        codigos.Unidades.Should().Equal("CEPS");
        codigos.TipoDeProcesso.Should().Be("SiSU");
        codigos.Localidade.Should().Be("1504208");
        codigos.FasesCanonicas.Should().Equal("INSCRICAO");
        codigos.Regras.Should().Equal(("CONTAGEM", "v1"));
        codigos.TiposDeBanca.Should().BeEmpty();
        codigos.CategoriasDeDocumento.Should().BeEmpty();
        codigos.TiposDeAto.Should().BeEmpty();
    }

    [Fact(DisplayName = "A identificação vazia e o documento sem fase não citam código algum")]
    public void IdentificacaoVaziaESemFases_NaoCitamCodigo()
    {
        CodigosCitadosPorCadastro codigos = Codigos(new(null, null, null, OrigemCandidatos.Nenhuma, null, null, null));

        new[] { codigos.Unidades, codigos.FasesCanonicas, codigos.TiposDeBanca, codigos.CategoriasDeDocumento, codigos.TiposDeAto }
            .Should().AllSatisfy(static lista => lista.Should().BeEmpty());
        (codigos.TipoDeProcesso, codigos.Localidade).Should().Be((null, null));
        codigos.Regras.Should().BeEmpty();
    }

    [Fact(DisplayName = "A unidade administradora e as responsáveis dividem o cadastro de unidades, e a mesma unidade vale uma vez")]
    public void Unidades_AdministradoraEResponsaveisDividemOCadastro()
    {
        CodigosCitadosPorCadastro codigos = Codigos(
            Identificacao, Fase(0, "INSCRICAO", "CEPS"), Fase(1, "RECURSO", "CGA"), Fase(2, "RESULTADO", "CGA"), Fase(3, "ENCERRAMENTO"));

        codigos.Unidades.Should().Equal("CEPS", "CGA");
    }

    [Fact(DisplayName = "A fase canônica citada em várias fases do documento vale uma vez, na ordem em que aparece")]
    public void FaseCanonicaRepetida_ValeUmaVez()
    {
        CodigosCitadosPorCadastro codigos = Codigos(Identificacao, Fase(0, "RECURSO"), Fase(1, "INSCRICAO"), Fase(2, "RECURSO"));

        codigos.FasesCanonicas.Should().Equal("RECURSO", "INSCRICAO");
    }

    [Fact(DisplayName = "O tipo e as categorias das bancas valem uma vez cada, mesmo citados em fases diferentes")]
    public void Bancas_TipoECategoriasValemUmaVez()
    {
        FaseNoDocumento primeira = ComBancas(Fase(0, "ANALISE"), Banca(0, "BANCA_DOCUMENTAL", "RENDA", "IDENTIDADE"), Banca(1, "BANCA_COLEGIADA", "RENDA"));
        FaseNoDocumento segunda = ComBancas(Fase(1, "RECURSO"), Banca(0, "BANCA_DOCUMENTAL", "IDENTIDADE", "ESCOLARIDADE"));

        CodigosCitadosPorCadastro codigos = Codigos(Identificacao, primeira, segunda);

        codigos.TiposDeBanca.Should().Equal("BANCA_DOCUMENTAL", "BANCA_COLEGIADA");
        codigos.CategoriasDeDocumento.Should().Equal("RENDA", "IDENTIDADE", "ESCOLARIDADE");
    }

    [Fact(DisplayName = "O tipo de ato publicado em várias fases vale uma vez")]
    public void TipoDeAtoRepetido_ValeUmaVez()
    {
        FaseNoDocumento primeira = ComProdutos(Fase(0, "ANALISE"), "RESULTADO_PRELIMINAR", "AVISO");
        FaseNoDocumento segunda = ComProdutos(Fase(1, "RESULTADO"), "AVISO", "RESULTADO_DEFINITIVO");

        CodigosCitadosPorCadastro codigos = Codigos(Identificacao, primeira, segunda);

        codigos.TiposDeAto.Should().Equal("RESULTADO_PRELIMINAR", "AVISO", "RESULTADO_DEFINITIVO");
    }

    [Fact(DisplayName = "A regra do catálogo vem do algoritmo de contagem, da regra de recurso e dos procedimentos, cada par uma vez")]
    public void Regras_VemDoAlgoritmoDaRegraDeRecursoEDosProcedimentos()
    {
        FaseNoDocumento fase = ComRegraDeRecurso(
            Fase(0, "ANALISE"), new("PRAZO-ANCORADO", "v1"), Procedimento(0, new("CONFERENCIA", "v1")), Procedimento(1, new("CONFERENCIA", "v1")));
        FaseNoDocumento outra = ComRegraDeRecurso(Fase(1, "RECURSO"), new("PRAZO-ANCORADO", "v1"), Procedimento(0, new("CONFERENCIA", "v2")));

        CodigosCitadosPorCadastro codigos = Codigos(Identificacao, fase, outra);

        codigos.Regras.Should().Equal(("CONTAGEM", "v1"), ("PRAZO-ANCORADO", "v1"), ("CONFERENCIA", "v1"), ("CONFERENCIA", "v2"));
    }

    [Fact(DisplayName = "A regra citada sem código ou sem versão não consulta o catálogo")]
    public void RegraSemCodigoOuSemVersao_NaoConsultaOCatalogo()
    {
        FaseNoDocumento fase = ComRegraDeRecurso(
            Fase(0, "ANALISE"), new("PRAZO-ANCORADO", null), Procedimento(0, new(null, "v1")), Procedimento(1, new(null, null)));

        CodigosCitadosPorCadastro codigos = Codigos(Identificacao with { AlgoritmoContagemPrazo = new("CONTAGEM", null) }, fase);

        codigos.Regras.Should().BeEmpty();
    }
}
