namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// O dado que um relatório traz no arquivo não é coletado por campo de formulário (UNI-REQ-0144), salvo o dado
/// básico que algum relatório considerado não traz inteiro, coletado só pelo formulário obrigatório da primeira
/// fase depois da importação (UNI-REQ-0151).
/// </summary>
public sealed class ConferenciaDosDadosDoArquivoTests
{
    private const string Deficiencia = "PESSOA_COM_DEFICIENCIA";
    private const string Endereco = "ENDERECO_RESIDENCIAL";
    private const string Nascimento = "DATA_NASCIMENTO";

    private static readonly string[] Rg = ["RG_NUMERO", "RG_UF", "RG_DATA_EMISSAO"];
    private static readonly CatalogoDosFatosCitados Catalogo =
        new(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>>())
        {
            PresencasNaImportacao = [new("RG_VEIO_NA_IMPORTACAO", new HashSet<string>(Rg, StringComparer.Ordinal))],
        };

    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(5, "HABILITACAO");
    private readonly FaseCronograma _matricula = FaseDeTeste.Criar(8, "MATRICULA");

    private static LeiauteDoRelatorio Relatorio(string codigo, params string[] fatosEmTodaLinha) =>
        LeiauteDeTeste.Criar(codigo, [.. fatosEmTodaLinha.Select(LeiauteDeTeste.EmTodaLinha)]);

    private static IReadOnlyList<FieldError> Conferir(RelatoriosConsiderados relatorios, Guid? primeiraFaseId, params ColetaDoFormulario[] coletas) =>
        ConferenciaDosDadosDoArquivo.Conferir(coletas, relatorios, new ProcessoDosFormularios(false, false, primeiraFaseId), Catalogo);

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> recusas) =>
        recusas.Select(static recusa => (recusa.Field, recusa.Error.Code));

    [Fact(DisplayName = "O fato que o relatório traz e um campo coleta é recusado, inclusive a data de nascimento trazida por todos")]
    public void FatoTrazidoEColetado_Recusa()
    {
        RelatoriosConsiderados relatorios = new([Relatorio("A", Deficiencia, Nascimento), Relatorio("B", Nascimento)], []);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Deficiencia, Nascimento);

        Recusas(Conferir(relatorios, _habilitacao.Id, coleta)).Should().Equal(
            ($"fatos.{Deficiencia}", ImportacaoDoResultadoErrorCodes.FatoColetadoPorFormulario),
            ($"fatos.{Nascimento}", ImportacaoDoResultadoErrorCodes.FatoColetadoPorFormulario));
    }

    [Fact(DisplayName = "O fato que nenhum relatório traz não conflita com campo algum")]
    public void FatoQueNenhumRelatorioTraz_NaoConflita() =>
        Conferir(new([Relatorio("A", Nascimento)], []), _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Deficiencia))
            .Should().BeEmpty();

    [Fact(DisplayName = "O dado básico que um relatório não traz é coletado pelo obrigatório da primeira fase depois da importação")]
    public void DadoBasicoQueUmRelatorioNaoTraz_NoObrigatorioDaPrimeiraFase_Aceita()
    {
        RelatoriosConsiderados relatorios = new([Relatorio("REGULAR", Endereco), Relatorio("PERFIL")], []);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Endereco);

        Conferir(relatorios, _habilitacao.Id, coleta).Should().BeEmpty();
    }

    [Fact(DisplayName = "O RG que um relatório traz só com o número é coletado inteiro pelo obrigatório da primeira fase")]
    public void RgIncompletoEmUmRelatorio_ColetadoInteiro_Aceita()
    {
        RelatoriosConsiderados relatorios = new([Relatorio("REGULAR", Rg), Relatorio("ESPERA", "RG_NUMERO")], []);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Rg);

        Conferir(relatorios, _habilitacao.Id, coleta).Should().BeEmpty();
    }

    [Fact(DisplayName = "O relatório já importado, mesmo retirado do documento, mantém o dado como a complementar")]
    public void RelatorioImportadoRetirado_MantemAExcecao()
    {
        RelatoriosConsiderados relatorios = new([Relatorio("REGULAR", Endereco)], [Relatorio("PERFIL")]);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Endereco);

        Conferir(relatorios, _habilitacao.Id, coleta).Should().BeEmpty();
    }

    [Fact(DisplayName = "O dado básico a complementar em formulário facultativo é recusado")]
    public void DadoBasicoAComplementar_EmFacultativo_Recusa()
    {
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao), Endereco);

        Recusas(Conferir(new([Relatorio("REGULAR", Endereco), Relatorio("PERFIL")], []), _habilitacao.Id, coleta)).Should().Equal(
            ($"fatos.{Endereco}", ImportacaoDoResultadoErrorCodes.DadoBasicoComplementarForaDaPrimeiraFase));
    }

    [Fact(DisplayName = "O dado básico a complementar em obrigatório de outra fase é recusado")]
    public void DadoBasicoAComplementar_EmOutraFase_Recusa()
    {
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("MATRICULA", _matricula), Endereco);

        Recusas(Conferir(new([Relatorio("REGULAR", Endereco), Relatorio("PERFIL")], []), _habilitacao.Id, coleta)).Should().Equal(
            ($"fatos.{Endereco}", ImportacaoDoResultadoErrorCodes.DadoBasicoComplementarForaDaPrimeiraFase));
    }

    [Fact(DisplayName = "Sem a primeira fase declarada, a exceção não é avaliada e a recusa que orienta é a da primeira fase por declarar")]
    public void SemPrimeiraFase_NaoAvaliaAExcecao()
    {
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao), Endereco);

        Conferir(new([Relatorio("REGULAR", Endereco), Relatorio("PERFIL")], []), null, coleta).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem relatório considerado, o arquivo não traz fato algum e nada conflita")]
    public void SemRelatorio_NaoConflita() =>
        Conferir(RelatoriosConsiderados.Nenhum, _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao), Endereco, Deficiencia))
            .Should().BeEmpty();

    [Fact(DisplayName = "O fato em coluna opcional também é do arquivo: coletá-lo por campo é recusado")]
    public void FatoEmColunaOpcional_Coletado_Recusa()
    {
        RelatoriosConsiderados relatorios = new([LeiauteDeTeste.Criar("A", LeiauteDeTeste.Opcional(Deficiencia))], []);
        ColetaDoFormulario coleta = ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Deficiencia);

        Recusas(Conferir(relatorios, _habilitacao.Id, coleta)).Should().Equal(($"fatos.{Deficiencia}", ImportacaoDoResultadoErrorCodes.FatoColetadoPorFormulario));
    }

    [Fact(DisplayName = "O dado básico em coluna opcional de todos os relatórios é coletado só pelo obrigatório da primeira fase")]
    public void DadoBasicoEmColunaOpcional_SoNoObrigatorioDaPrimeiraFase()
    {
        RelatoriosConsiderados relatorios = new([LeiauteDeTeste.Criar("A", LeiauteDeTeste.Opcional(Endereco))], []);

        Conferir(relatorios, _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), Endereco)).Should().BeEmpty();
        Recusas(Conferir(relatorios, _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao), Endereco))).Should().Equal(
            ($"fatos.{Endereco}", ImportacaoDoResultadoErrorCodes.DadoBasicoComplementarForaDaPrimeiraFase));
    }

    [Fact(DisplayName = "O componente do dado básico que nenhum relatório declara só é coletado pelo obrigatório da primeira fase")]
    public void ComponenteAusenteDoDadoBasico_SoNoObrigatorioDaPrimeiraFase()
    {
        RelatoriosConsiderados relatorios = new([Relatorio("A", "RG_NUMERO")], []);

        Conferir(relatorios, _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), "RG_UF")).Should().BeEmpty();
        Recusas(Conferir(relatorios, _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("HABILITACAO", _habilitacao), "RG_UF"))).Should().Equal(
            ("fatos.RG_UF", ImportacaoDoResultadoErrorCodes.DadoBasicoComplementarForaDaPrimeiraFase));
        Recusas(Conferir(relatorios, _habilitacao.Id, ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("MATRICULA", _matricula), "RG_UF"))).Should().Equal(
            ("fatos.RG_UF", ImportacaoDoResultadoErrorCodes.DadoBasicoComplementarForaDaPrimeiraFase));
    }
}
