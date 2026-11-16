namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// O dado que um relatório traz em toda linha é resposta anterior a todos os formulários (UNI-REQ-0144), e o fato
/// "veio na importação" do dado básico é conhecido quando algum relatório traz o dado inteiro (UNI-REQ-0151).
/// O fato do arquivo continua sujeito à fase do catálogo.
/// </summary>
public sealed class ConhecimentoDosFatosDaImportacaoTests
{
    private const string Deficiencia = "PESSOA_COM_DEFICIENCIA";
    private const string RgVeioNaImportacao = "RG_VEIO_NA_IMPORTACAO";

    private static readonly HashSet<string> Rg = ["RG_NUMERO", "RG_ORGAO"];

    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");

    private ConhecimentoDosFatos Conhecimento(RelatoriosConsiderados relatorios, Dictionary<string, string>? fases = null, params ColetaDoFormulario[] coletas) =>
        new(coletas, Catalogo(fases ?? []), [_inscricao, _habilitacao]) { Relatorios = relatorios };

    private static CatalogoDosFatosCitados Catalogo(Dictionary<string, string> fases) =>
        new(fases, [Deficiencia, .. Rg], new Dictionary<string, IReadOnlyCollection<string>>()) { PresencasNaImportacao = [new(RgVeioNaImportacao, Rg)] };

    private static RelatoriosConsiderados Trazendo(params DadoDoArquivo[] dados) => new([LeiauteDeTeste.Criar("CHAMADA_REGULAR", dados)], []);

    private PontoDeUso NaHabilitacao(out FormularioDeclarado formulario)
    {
        formulario = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        return new PontoDeUso(_habilitacao, formulario);
    }

    [Fact(DisplayName = "O dado que o relatório traz em toda linha é conhecido na habilitação, sem formulário que o colete")]
    public void DadoDoArquivo_EConhecidoSemFormulario()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo(LeiauteDeTeste.EmTodaLinha(Deficiencia)));

        conhecimento.NoPonto(Deficiencia, NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "Sem declaração da importação, o dado declarado que nenhum formulário coleta fica fora do processo")]
    public void SemImportacao_DadoDeclaradoForaDoProcesso()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(RelatoriosConsiderados.Nenhum);

        conhecimento.NoPonto(Deficiencia, NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
    }

    [Fact(DisplayName = "O dado em coluna opcional não é conhecido pelo arquivo")]
    public void ColunaOpcional_NaoEConhecido()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo(LeiauteDeTeste.Opcional(Deficiencia)));

        conhecimento.NoPonto(Deficiencia, NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
    }

    [Fact(DisplayName = "O dado do arquivo com fase do catálogo posterior ao ponto não é conhecido")]
    public void DadoDoArquivo_FaseDoCatalogoPosterior()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo(LeiauteDeTeste.EmTodaLinha(Deficiencia)), new() { [Deficiencia] = "HABILITACAO" });
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        conhecimento.NoPonto(Deficiencia, new PontoDeUso(_inscricao, inscricao)).Should().Be(ConhecimentoDoFatoNoPonto.ConhecidoEmFasePosterior);
    }

    [Fact(DisplayName = "O fato de presença é conhecido quando algum relatório traz o dado inteiro")]
    public void Presenca_DadoInteiro_EConhecido()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo([.. Rg.Select(LeiauteDeTeste.EmTodaLinha)]));

        conhecimento.NoPonto(RgVeioNaImportacao, NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }

    [Fact(DisplayName = "O fato de presença fica fora do processo quando nenhum relatório traz o dado inteiro")]
    public void Presenca_DadoEmPedacos_ForaDoProcesso()
    {
        RelatoriosConsiderados relatorios = new(
            [LeiauteDeTeste.Criar("A", LeiauteDeTeste.EmTodaLinha("RG_NUMERO")), LeiauteDeTeste.Criar("B", LeiauteDeTeste.EmTodaLinha("RG_ORGAO"))], []);

        Conhecimento(relatorios).NoPonto(RgVeioNaImportacao, NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
    }

    [Fact(DisplayName = "O campo que complementa o dado cita a presença dele sem formar ciclo com o próprio dado")]
    public void CampoComplementar_CitaPresencaDoProprioDado_SemCiclo()
    {
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(habilitacao, "RG_NUMERO") with
        {
            Itens = [new("RG_NUMERO", 1, null, [RgVeioNaImportacao])],
        };
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo([.. Rg.Select(LeiauteDeTeste.EmTodaLinha)]), null, coleta);

        ConferenciaDasRegrasDosFormularios.Conferir(conhecimento).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os fatos conhecidos antes do formulário incluem os do arquivo e a presença do dado inteiro")]
    public void ConhecidosAntesDoFormulario_IncluemOsDoArquivo()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo([LeiauteDeTeste.EmTodaLinha(Deficiencia), .. Rg.Select(LeiauteDeTeste.EmTodaLinha)]));

        conhecimento.ConhecidosAntesDoFormulario(NaHabilitacao(out _)).Should().BeEquivalentTo([Deficiencia, .. Rg, RgVeioNaImportacao]);
    }

    [Fact(DisplayName = "O dado que o formulário complementa só é conhecido onde o formulário é alcançado, porque nem todo relatório o traz")]
    public void DadoAComplementar_SoConhecidoOndeOFormularioEAlcancado()
    {
        FormularioDeclarado complementar = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);
        FormularioDeclarado outroDaMesmaFase = ColetaDeTeste.Obrigatorio("OUTRO", _habilitacao);
        ConhecimentoDosFatos conhecimento = Conhecimento(
            Trazendo(LeiauteDeTeste.EmTodaLinha(Deficiencia)), null, ColetaDeTeste.Coleta(complementar, Deficiencia), ColetaDeTeste.Coleta(outroDaMesmaFase, "outro"));

        conhecimento.NoPonto(Deficiencia, new PontoDeUso(_habilitacao, outroDaMesmaFase)).Should().Be(ConhecimentoDoFatoNoPonto.RespostaNaoAlcancada);
    }

    [Fact(DisplayName = "O componente de dado básico que nenhum relatório traz inteiro não é conhecido: o dado incompleto não vira resposta")]
    public void ComponenteDeDadoBasicoIncompleto_NaoEConhecido()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo(LeiauteDeTeste.EmTodaLinha("RG_NUMERO")));

        conhecimento.NoPonto("RG_NUMERO", NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.ForaDoProcesso);
    }

    [Fact(DisplayName = "O fato de presença é conhecido quando o relatório declara o dado inteiro só em coluna opcional, que pode vir preenchida")]
    public void Presenca_DadoInteiroEmColunaOpcional_EConhecido()
    {
        ConhecimentoDosFatos conhecimento = Conhecimento(Trazendo([.. Rg.Select(LeiauteDeTeste.Opcional)]));

        conhecimento.NoPonto(RgVeioNaImportacao, NaHabilitacao(out _)).Should().Be(ConhecimentoDoFatoNoPonto.Conhecido);
    }
}
