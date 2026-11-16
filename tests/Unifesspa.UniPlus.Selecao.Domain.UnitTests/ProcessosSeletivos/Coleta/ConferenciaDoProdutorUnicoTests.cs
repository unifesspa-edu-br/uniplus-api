namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// Cada fato tem um único campo que o produz no processo, em um único formulário (UNI-REQ-0144). A escolha de
/// fundamentos do pedido de isenção é registrada por formulário e fica fora da regra do campo produtor.
/// </summary>
public sealed class ConferenciaDoProdutorUnicoTests
{
    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

    [Fact(DisplayName = "Fatos distintos em formulários distintos são aceitos")]
    public void FatosDistintos_Aceita()
    {
        ConferenciaDoProdutorUnico.Conferir(
        [
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar"),
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), "carteira"),
        ]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O fato coletado por campos de dois formulários é recusado")]
    public void FatoEmDoisFormularios_Recusa()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDoProdutorUnico.Conferir(
        [
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao), "origem_escolar"),
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao), "origem_escolar"),
        ]);

        recusas.Should().ContainSingle().Which.Error.Code.Should().Be("FatoColetado.FatoDuplicado");
        recusas.Single().Field.Should().Be("fatos.origem_escolar");
        recusas.Single().Error.Message.Should().NotContain("origem_escolar");
    }

    [Fact(DisplayName = "O fato coletado por dois campos do mesmo formulário, inclusive de grupo, é recusado")]
    public void FatoEmDoisCamposDoMesmoFormulario_Recusa()
    {
        Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios.FormularioDeclarado formulario = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ColetaDoFormulario comGrupo = ColetaDeTeste.ColetaComGrupo(formulario, "dependentes", "nome") with { Itens = [new("nome", 1, null, [])] };

        ConferenciaDoProdutorUnico.Conferir([comGrupo]).Should().ContainSingle().Which.Field.Should().Be("fatos.nome");
    }

    [Fact(DisplayName = "A escolha de fundamentos registrada em mais de um formulário é aceita")]
    public void EscolhaDeFundamentosPorFormulario_Aceita()
    {
        ConferenciaDoProdutorUnico.Conferir(
        [
            ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("PEDIDO_A", _inscricao), "renda") with { FatoDaEscolhaDeFundamentos = "fundamento_da_isencao" },
            ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("PEDIDO_B", _inscricao), "outra_renda") with { FatoDaEscolhaDeFundamentos = "fundamento_da_isencao" },
        ]).Should().BeEmpty();
    }

    [Fact(DisplayName = "O fato duplicado sai uma vez, mesmo coletado três vezes")]
    public void FatoTresVezes_UmaRecusa()
    {
        ConferenciaDoProdutorUnico.Conferir(
        [
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("A", _inscricao), "x"),
            ColetaDeTeste.Coleta(ColetaDeTeste.Obrigatorio("B", _habilitacao), "x"),
            ColetaDeTeste.Coleta(ColetaDeTeste.Facultativo("C", _habilitacao), "x"),
        ]).Should().ContainSingle();
    }
}
