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

    [Fact(DisplayName = "As fases que produzem resultado são as que publicam ato")]
    public void Seed_ProduzResultadoBateComOQuePublicaAto()
    {
        // Oráculo independente da fonte única: esta lista é o que a decisão de negócio
        // aprovou, e alterá-la é ato deliberado. Cada uma corresponde a um tipo de ato
        // do catálogo de Publicações — homologação, resultados, habilitação,
        // heteroidentificação, convocação e o deferimento da isenção.
        string[] publicamAto =
        [
            FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            "HOMOLOGACAO",
            "RESULTADO_PRELIMINAR",
            "RESULTADO_FINAL",
            "HETEROIDENTIFICACAO",
            "HABILITACAO",
            "HOMOLOGACAO_RESULTADO_FINAL",
            "CHAMADA",
        ];

        FaseCanonicaSeed.Itens.Where(item => item.ProduzResultado)
            .Select(item => item.Codigo)
            .Should().BeEquivalentTo(publicamAto);
    }
}
