namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Guarda o seed das fases canônicas (<see cref="FaseCanonicaSeed"/>) contra
/// edições que passariam despercebidas: uma fase acrescentada sem decisão, um
/// atributo normativo trocado, ou um valor que o próprio cadastro recusaria.
/// </summary>
/// <remarks>
/// O seed materializa linhas por <c>HasData</c>, sem passar pela factory do
/// agregado — então nada no caminho de escrita impediria o banco de nascer com um
/// dado que o CRUD rejeitaria. Estes testes fecham essa folga sem precisar de
/// banco: rodam no mesmo job de testes unitários.
/// </remarks>
public sealed class CatalogoDeFasesCanonicasTests
{
    [Fact(DisplayName = "O seed é exatamente o vocabulário fechado — nem a mais, nem a menos")]
    public void Seed_CobreTodoOVocabularioFechado()
    {
        // A fase é vocabulário estrutural: o rol do catálogo e o do seed são o mesmo
        // conjunto por construção. Uma fase no catálogo sem linha no seed volta a
        // produzir o estado que esta issue corrige — código aceito, registro ausente.
        FaseCanonicaSeed.Itens.Select(item => item.Codigo)
            .Should().BeEquivalentTo(FaseCanonicaCatalogo.Codigos);
    }

    [Fact(DisplayName = "Cada fase do seed é aceita pela factory do agregado")]
    public void Seed_PassaPelaFactory()
    {
        // O HasData escreve direto no banco. Submeter cada item à factory é o que
        // impede o seed de materializar um dado que a tela recusaria — por exemplo,
        // agrupar etapas fora da avaliação ou permitir complementação fora do rol legal.
        foreach (FaseCanonicaSeedItem item in FaseCanonicaSeed.Itens)
        {
            Result<FaseCanonica> resultado = FaseCanonica.Criar(
                item.Codigo,
                item.Nome,
                item.Descricao,
                DonosTipicos.ParaTokenCanonico(item.DonoTipico),
                item.AgrupaEtapas,
                item.PermiteComplementacao,
                item.BaseLegal,
                item.ProduzResultado,
                item.ResultadoDefinitivo,
                item.ColetaInscricao,
                item.ColetaSolicitacaoIsencao,
                OrigensDataFase.ParaTokenCanonico(item.OrigemData));

            resultado.IsSuccess.Should().BeTrue(
                $"a fase {item.Codigo} do seed precisa ser aceita pelo mesmo caminho que valida o cadastro");
        }
    }

    [Fact(DisplayName = "Os identificadores do seed são únicos e do prefixo determinístico")]
    public void Seed_IdentificadoresDeterministicos()
    {
        // O Down da migration apaga por LIKE no prefixo. Id fora do padrão sobreviveria
        // ao rollback; id repetido quebraria a chave primária na primeira migração.
        FaseCanonicaSeed.Itens.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        FaseCanonicaSeed.Itens.Should().OnlyContain(
            item => item.Id.ToString().StartsWith("f45e0000-0000-7000-8000-", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Só a avaliação agrupa etapas, e só uma fase coleta inscrição")]
    public void Seed_RespeitaAsExclusividadesDoCiclo()
    {
        FaseCanonicaSeed.Itens.Where(item => item.AgrupaEtapas)
            .Select(item => item.Codigo)
            .Should().Equal(FaseCanonicaCatalogo.CodigoAvaliacao);

        // A âncora da janela de solicitação de isenção deriva da fase que coleta
        // inscrição (UNI-REQ-0106). Duas fases coletando tornariam a âncora ambígua;
        // nenhuma a tornaria inexistente.
        FaseCanonicaSeed.Itens.Where(item => item.ColetaInscricao)
            .Select(item => item.Codigo)
            .Should().Equal("INSCRICAO");
    }

    [Fact(DisplayName = "Complementação documental só nas fases legalmente permitidas")]
    public void Seed_ComplementacaoApenasOndeALeiPermite()
    {
        FaseCanonicaSeed.Itens.Where(item => item.PermiteComplementacao)
            .Select(item => item.Codigo)
            .Should().BeEquivalentTo(FaseCanonicaCatalogo.CodigosComComplementacaoPermitida);
    }

    [Fact(DisplayName = "Resultado definitivo implica produzir resultado")]
    public void Seed_ResultadoDefinitivoImplicaProduzir()
    {
        // Mesma invariante que ValidarCamposComuns aplica no cadastro. Aqui ela pega
        // a incoerência antes de virar linha no banco.
        FaseCanonicaSeed.Itens.Where(item => item.ResultadoDefinitivo)
            .Should().OnlyContain(item => item.ProduzResultado);
    }

    [Fact(DisplayName = "As fases delegadas ao MEC são as do fluxo SiSU")]
    public void Seed_OrigemDelegadaApenasNoFluxoSisu()
    {
        // Lista de espera e chamada têm data definida pelo cronograma do MEC, não pela
        // instituição — é o que `DELEGADA` significa. Qualquer outra fase com origem
        // delegada seria uma janela que a instituição não controla sem que ninguém
        // tenha decidido isso.
        FaseCanonicaSeed.Itens.Where(item => item.OrigemData == OrigemDataFase.Delegada)
            .Select(item => item.Codigo)
            .Should().BeEquivalentTo("LISTA_ESPERA", "CHAMADA");

        FaseCanonicaSeed.Itens.Where(item => item.DonoTipico == DonoTipico.Mec)
            .Select(item => item.Codigo)
            .Should().BeEquivalentTo("LISTA_ESPERA", "CHAMADA");
    }

    [Fact(DisplayName = "A avaliação biopsicossocial tem os atributos publicados em UNI-REQ-0139")]
    public void Seed_AvaliacaoBiopsicossocial_TemOsAtributosDoRequisitoCanonico()
    {
        // Oráculo dos atributos normativos da fase (UNI-REQ-0139): espelha
        // exatamente HETEROIDENTIFICACAO — banca cujo parecer verifica direito à
        // reserva de vagas antes da homologação do resultado final. Alterar
        // qualquer um destes campos é ato deliberado, não refatoração.
        FaseCanonicaSeedItem item = FaseCanonicaSeed.Itens.Single(i => i.Codigo == "AVALIACAO_BIOPSICOSSOCIAL");

        item.Id.Should().Be(Guid.Parse("f45e0000-0000-7000-8000-000000000016"));
        item.Nome.Should().Be("Avaliação biopsicossocial");
        item.DonoTipico.Should().Be(DonoTipico.Ceps);
        item.OrigemData.Should().Be(OrigemDataFase.Propria);
        item.ProduzResultado.Should().BeTrue();
        item.ResultadoDefinitivo.Should().BeFalse();
        item.ColetaInscricao.Should().BeFalse();
        item.ColetaSolicitacaoIsencao.Should().BeFalse();
        item.AgrupaEtapas.Should().BeFalse();
        item.PermiteComplementacao.Should().BeFalse();
        item.BaseLegal.Should().Be(
            "Lei nº 13.146/2015, art. 2º §1º e art. 30; Lei nº 12.711/2012 c/c Lei nº 13.409/2016");
    }

    [Fact(DisplayName = "Codigos é exatamente o conjunto de Descritos, na mesma ordem")]
    public void Codigos_DerivaDeDescritos()
    {
        // Codigos é uma projeção de Descritos, não uma segunda lista escrita à mão — este
        // teste prova a derivação, não apenas o conjunto (BeEquivalentTo aceitaria ordem
        // trocada, o que aqui seria sintoma de alguém ter voltado a duas listas irmãs).
        FaseCanonicaCatalogo.Codigos.Should().Equal(
            [.. FaseCanonicaCatalogo.Descritos.Select(static d => d.Codigo)]);
    }

    [Fact(DisplayName = "Toda fase canônica do vocabulário tem rótulo não vazio")]
    public void Descritos_TemRotuloNaoVazio()
    {
        FaseCanonicaCatalogo.Descritos.Should().AllSatisfy(
            static d => d.Nome.Should().NotBeNullOrWhiteSpace());
    }

    [Fact(DisplayName = "O rótulo do vocabulário coincide com o Nome inicial do seed, código a código")]
    public void Descritos_RotuloCoincideComNomeDoSeed()
    {
        // FaseCanonicaCatalogo.Descritos (o rótulo do vocabulário) e FaseCanonicaSeed.Itens
        // (o Nome persistido) são fontes independentes de propósito — o seed é dado
        // editável pelo CRUD depois do deploy, o vocabulário é rótulo fixo do código. Este
        // teste é o oráculo que garante que elas nascem coerentes, sem acoplar as duas.
        Dictionary<string, string> nomesDoSeed = FaseCanonicaSeed.Itens
            .ToDictionary(static item => item.Codigo, static item => item.Nome, StringComparer.Ordinal);

        FaseCanonicaCatalogo.Descritos.Should().AllSatisfy(descrito =>
            nomesDoSeed[descrito.Codigo].Should().Be(descrito.Nome,
                $"o rótulo do vocabulário para {descrito.Codigo} deveria coincidir com o Nome inicial do seed"));
    }

    [Fact(DisplayName = "As fases que produzem resultado são as que publicam ato")]
    public void Seed_ProduzResultadoBateComOQuePublicaAto()
    {
        // Oráculo independente da fonte única: esta lista é o que a decisão de negócio
        // aprovou, e alterá-la é ato deliberado. Cada uma corresponde a um tipo de ato
        // do catálogo de Publicações — homologação, resultados, habilitação,
        // heteroidentificação, avaliação biopsicossocial, convocação e o deferimento
        // da isenção. A avaliação biopsicossocial publica ato pelo mesmo motivo que a
        // heteroidentificação: o parecer da banca (defere/indefere a condição de PcD
        // para concorrer à reserva de vagas) é decisão publicável, não rascunho interno.
        string[] publicamAto =
        [
            FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            "HOMOLOGACAO",
            "RESULTADO_PRELIMINAR",
            "RESULTADO_FINAL",
            "HETEROIDENTIFICACAO",
            "AVALIACAO_BIOPSICOSSOCIAL",
            "HABILITACAO",
            "HOMOLOGACAO_RESULTADO_FINAL",
            "CHAMADA",
        ];

        FaseCanonicaSeed.Itens.Where(item => item.ProduzResultado)
            .Select(item => item.Codigo)
            .Should().BeEquivalentTo(publicamAto);
    }
}
