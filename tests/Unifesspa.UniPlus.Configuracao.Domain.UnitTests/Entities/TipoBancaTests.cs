namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoBancaTests
{
    private static DeclaracaoDoTipoDeBanca Declaracao(
        string? nome = "Banca de entrevista",
        string? faseTipica = null,
        string? descricao = null) =>
        new(nome, faseTipica, descricao, "AVALIA_O_MERITO", TiposDeDecisaoJulgados: ["NOTA_DA_ETAPA"]);

    private static Result<TipoBanca> Criar(
        string codigo = "BANCA_ENTREVISTA",
        string? nome = "Banca de entrevista",
        string? faseTipica = null,
        string? descricao = null) =>
        TipoBanca.Criar(codigo, Declaracao(nome, faseTipica, descricao));

    // ── Factory válida ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Tipo de banca válido preenche os campos e fica ativo com Guid v7")]
    public void Criar_Valido_Aceita()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_DOCUMENTAL", nome: "Banca de análise documental").Value!;

        b.Id.Should().NotBe(Guid.Empty);
        b.Codigo.Should().Be(TipoDeBanca.AnaliseDocumental);
        b.Nome.Should().Be("Banca de análise documental");
        b.FaseTipica.Should().BeNull();
        b.IsDeleted.Should().BeFalse();
    }

    // ── Leitura exata do código ────────────────────────────────────────────────

    [Theory(DisplayName = "Código com minúscula, hífen, espaço nas pontas ou dígito não é do vocabulário")]
    [InlineData("banca_entrevista")]
    [InlineData("BANCA-ENTREVISTA")]
    [InlineData(" BANCA_ENTREVISTA ")]
    [InlineData("BANCA2")]
    public void Criar_CodigoEscritoDeOutroJeito_NaoEDoVocabulario(string codigo)
    {
        Result<TipoBanca> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<TipoBanca> r = Criar(codigo: codigo);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoObrigatorio);
    }

    // ── Domínio canônico ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Código bem-formado fora do vocabulário é rejeitado")]
    public void Criar_CodigoForaDoConjuntoCanonico_Falha()
    {
        Result<TipoBanca> r = Criar(codigo: "BANCA_LOGISTICA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
    }

    [Fact(DisplayName = "Mensagem de código fora do conjunto canônico não ecoa o valor submetido")]
    public void Criar_CodigoForaDoConjuntoCanonico_NaoEcoaValorSubmetidoNaMensagem()
    {
        Result<TipoBanca> r = Criar(codigo: "BANCA_LOGISTICA_MUITO_ESPECIFICA");

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().NotContain("BANCA_LOGISTICA_MUITO_ESPECIFICA");
    }

    [Theory(DisplayName = "Todo código do vocabulário de tipos de banca é aceito e vira o tipo correspondente")]
    [InlineData("BANCA_ANALISE_DOCUMENTAL", TipoDeBanca.AnaliseDocumental)]
    [InlineData("BANCA_ENTREVISTA", TipoDeBanca.Entrevista)]
    [InlineData("BANCA_CORRECAO_REDACOES", TipoDeBanca.CorrecaoDeRedacoes)]
    [InlineData("BANCA_ANALISE_RECURSOS", TipoDeBanca.AnaliseDeRecursos)]
    [InlineData("BANCA_HETEROIDENTIFICACAO", TipoDeBanca.Heteroidentificacao)]
    [InlineData("BANCA_BIOPSICOSSOCIAL", TipoDeBanca.AvaliacaoBiopsicossocial)]
    [InlineData("BANCA_HOMOLOGACAO_INSCRICOES", TipoDeBanca.HomologacaoDasInscricoes)]
    [InlineData("BANCA_IMPORTACAO_NOTAS_INEP", TipoDeBanca.ImportacaoDasNotasDoInep)]
    [InlineData("BANCA_IMPORTACAO_CANDIDATOS", TipoDeBanca.ImportacaoDeCandidatos)]
    [InlineData("BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA", TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia)]
    public void Criar_CodigoDoVocabulario_Aceita(string codigo, TipoDeBanca esperado)
    {
        Result<TipoBanca> r = Criar(codigo: codigo);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Codigo.Should().Be(esperado);
    }

    [Fact(DisplayName = "A recusa do código fora do vocabulário diz o que o campo aceita, sem repetir o valor recebido")]
    public void Criar_CodigoForaDoVocabulario_DetalheFixo()
    {
        Result<TipoBanca> r = Criar(codigo: "BANCA_LOGISTICA");

        r.Error!.Message.Should().Be(
            "O tipo de banca informado não é um dos aceitos. Tipos de banca aceitos: "
            + "Banca de análise documental; Banca de entrevista; Banca de correção de redações; "
            + "Banca de análise de recursos; Banca de heteroidentificação; "
            + "Banca de avaliação biopsicossocial; Banca de homologação das inscrições; "
            + "Banca de importação das notas do INEP; Banca de importação de candidatos; "
            + "Banca de análise de recursos de 2ª instância.");
        r.Error!.Message.Should().NotContain("BANCA_LOGISTICA");
    }

    // ── Nome ───────────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Nome ausente é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<TipoBanca> r = Criar(nome: nome);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome acima de 200 caracteres é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<TipoBanca> r = Criar(nome: new string('a', 201));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.NomeTamanho);
    }

    // ── Fase típica (orientativa, não vinculante) ──────────────────────────────

    [Fact(DisplayName = "Banca sem fase típica é aceita (fase típica nula)")]
    public void Criar_SemFaseTipica_Aceita()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_RECURSOS", nome: "Banca de análise de recursos", faseTipica: null).Value!;

        b.FaseTipica.Should().BeNull();
    }

    [Fact(DisplayName = "Fase típica é rótulo orientativo — valor sem correspondência é aceito")]
    public void Criar_FaseTipicaNaoVinculante_Aceita()
    {
        TipoBanca b = Criar(faseTipica: "Fase que não corresponde a nenhum código de fase").Value!;

        b.FaseTipica.Should().Be("Fase que não corresponde a nenhum código de fase");
    }

    [Fact(DisplayName = "Fase típica acima de 60 caracteres é rejeitada")]
    public void Criar_FaseTipicaLonga_Falha()
    {
        Result<TipoBanca> r = Criar(faseTipica: new string('a', 61));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.FaseTipicaTamanho);
    }

    /// <summary>
    /// O limite vale sobre o valor já normalizado (Trim), não sobre o bruto — 60
    /// caracteres úteis mais espaços nas pontas continua válido.
    /// </summary>
    [Fact(DisplayName = "Fase típica com 60 caracteres úteis mais espaços nas pontas é aceita")]
    public void Criar_FaseTipicaNoLimiteComEspacosNasPontas_Aceita()
    {
        TipoBanca b = Criar(faseTipica: "  " + new string('a', 60) + "  ").Value!;

        b.FaseTipica.Should().Be(new string('a', 60));
    }

    // ── Descrição ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Descrição acima de 300 caracteres é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<TipoBanca> r = Criar(descricao: new string('a', 301));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(TipoBancaErrorCodes.DescricaoTamanho);
    }

    // ── Acumulação (ADR-0125) ──────────────────────────────────────────────────

    /// <summary>
    /// Antes: Criar retornava no primeiro campo inválido (código), mascarando as
    /// demais violações — paridade com o FluentValidation removido, que reportava
    /// cada campo em separado.
    /// </summary>
    [Fact(DisplayName = "Código fora do canônico e nome ausente ao mesmo tempo acumulam as duas violações rotuladas")]
    public void Criar_CodigoForaDoCanonicoENomeAusente_AcumulaAsDuasViolacoesRotuladas()
    {
        Result<TipoBanca> r = TipoBanca.Criar("BANCA_LOGISTICA", Declaracao(nome: ""));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(2);
        r.Errors[0].Field.Should().Be("codigo");
        r.Errors[0].Error.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
        r.Errors[1].Field.Should().Be("nome");
        r.Errors[1].Error.Code.Should().Be(TipoBancaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome ausente, fase típica e descrição longas acumulam as três violações")]
    public void Criar_NomeAusenteFaseTipicaEDescricaoLongas_AcumulaAsTresViolacoes()
    {
        Result<TipoBanca> r = TipoBanca.Criar(
            "BANCA_ENTREVISTA", Declaracao(nome: "", faseTipica: new string('a', 61), descricao: new string('b', 301)));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(3);
        r.Errors[0].Field.Should().Be("nome");
        r.Errors[1].Field.Should().Be("faseTipica");
        r.Errors[2].Field.Should().Be("descricao");
    }

    [Fact(DisplayName = "Atualizar com nome ausente e descrição longa acumula as duas violações sem mutar o agregado")]
    public void Atualizar_NomeAusenteEDescricaoLonga_AcumulaAsDuasViolacoesSemMutar()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_RECURSOS", nome: "Banca de análise de recursos").Value!;

        Result r = b.Atualizar(Declaracao(nome: "", descricao: new string('a', 301)));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(2);
        b.Nome.Should().Be("Banca de análise de recursos", "falha de validação não pode mutar o agregado");
    }

    // ── Imutabilidade / atualização ────────────────────────────────────────────

    [Fact(DisplayName = "Atualizar troca atributos editáveis mantendo Codigo e Id imutáveis")]
    public void Atualizar_MantemCodigoEId()
    {
        TipoBanca b = Criar(codigo: "BANCA_ANALISE_RECURSOS", nome: "Banca de análise de recursos").Value!;
        Guid idOriginal = b.Id;

        Result r = b.Atualizar(Declaracao(nome: "Banca de recursos (novo)", faseTipica: "Recursos", descricao: "desc"));

        r.IsSuccess.Should().BeTrue();
        b.Codigo.Should().Be(TipoDeBanca.AnaliseDeRecursos, "o código é imutável");
        b.Id.Should().Be(idOriginal, "o Id é imutável");
        b.Nome.Should().Be("Banca de recursos (novo)");
        b.FaseTipica.Should().Be("Recursos");
    }

    // ── Papel, marcas e tipos de decisão ───────────────────────────────────────

    [Fact(DisplayName = "Tipo de banca que avalia o mérito guarda o papel, as marcas e os tipos de decisão em ordem alfabética do código")]
    public void Criar_ComAtuacao_GuardaPapelMarcasETiposOrdenados()
    {
        DeclaracaoDoTipoDeBanca declaracao = new(
            "Banca de análise documental", Papel: "AVALIA_O_MERITO", ExigeDuasOuMaisBancas: true, EncerraRecursoNaPrimeiraInstancia: true,
            TiposDeDecisaoJulgados: ["ISENCAO_DA_TAXA", "ANALISE_DOCUMENTAL"]);

        TipoBanca b = TipoBanca.Criar("BANCA_ANALISE_DOCUMENTAL", declaracao).Value!;

        b.Atuacao.Papel.Should().Be(PapelDaBanca.AvaliaOMerito);
        b.Atuacao.ExigeDuasOuMaisBancas.Should().BeTrue();
        b.Atuacao.EncerraRecursoNaPrimeiraInstancia.Should().BeTrue();
        b.Atuacao.TiposDeDecisaoJulgados.Should().Equal(TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.IsencaoDaTaxa);
    }

    [Fact(DisplayName = "Tipo de banca de outro papel é criado sem tipos de decisão e com as marcas apagadas por padrão")]
    public void Criar_PapelDeImportacao_SemTiposDeDecisao()
    {
        TipoBanca b = TipoBanca.Criar(
            "BANCA_IMPORTACAO_CANDIDATOS", new DeclaracaoDoTipoDeBanca("Banca de importação de candidatos", Papel: "IMPORTA_CANDIDATOS")).Value!;

        b.Atuacao.Papel.Should().Be(PapelDaBanca.ImportaCandidatos);
        b.Atuacao.ExigeDuasOuMaisBancas.Should().BeFalse();
        b.Atuacao.EncerraRecursoNaPrimeiraInstancia.Should().BeFalse();
        b.Atuacao.TiposDeDecisaoJulgados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Código fora do vocabulário, nome ausente e papel ausente acumulam as três violações, na ordem dos campos")]
    public void Criar_CodigoNomeEPapelInvalidos_AcumulaAsTresViolacoes()
    {
        Result<TipoBanca> r = TipoBanca.Criar("BANCA_LOGISTICA", new DeclaracaoDoTipoDeBanca(""));

        r.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("codigo", TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico),
            ("nome", TipoBancaErrorCodes.NomeObrigatorio),
            ("papel", TipoBancaErrorCodes.PapelObrigatorio));
    }

    [Fact(DisplayName = "Atualizar troca o papel, as marcas e os tipos de decisão, substituindo o que estava gravado")]
    public void Atualizar_TrocaAAtuacao()
    {
        TipoBanca b = Criar().Value!;

        Result r = b.Atualizar(new DeclaracaoDoTipoDeBanca(
            "Banca de entrevista", Papel: "JULGA_A_PRIMEIRA_INSTANCIA", ExigeDuasOuMaisBancas: true));

        r.IsSuccess.Should().BeTrue();
        b.Atuacao.Papel.Should().Be(PapelDaBanca.JulgaAPrimeiraInstancia);
        b.Atuacao.ExigeDuasOuMaisBancas.Should().BeTrue();
        b.Atuacao.TiposDeDecisaoJulgados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Atualizar com papel fora do vocabulário recusa e não muda o papel nem os tipos de decisão")]
    public void Atualizar_PapelInvalido_NaoMuta()
    {
        TipoBanca b = Criar().Value!;

        Result r = b.Atualizar(new DeclaracaoDoTipoDeBanca("Outro nome", Papel: "PAPEL_QUE_NAO_EXISTE", TiposDeDecisaoJulgados: ["CLASSIFICACAO"]));

        r.Error!.Code.Should().Be(TipoBancaErrorCodes.PapelForaDoVocabulario);
        b.Nome.Should().Be("Banca de entrevista");
        b.Atuacao.Papel.Should().Be(PapelDaBanca.AvaliaOMerito);
        b.Atuacao.TiposDeDecisaoJulgados.Should().Equal(TipoMotivoDeDecisao.NotaDaEtapa);
    }
}
