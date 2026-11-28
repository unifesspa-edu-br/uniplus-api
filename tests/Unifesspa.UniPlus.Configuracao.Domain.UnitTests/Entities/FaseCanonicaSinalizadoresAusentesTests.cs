namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDosTestesDeFaseCanonica;

public sealed class FaseCanonicaSinalizadoresAusentesTests
{
    [Theory(DisplayName = "Sinalizador ausente é recusado com o campo e a orientação do próprio sinalizador")]
    [InlineData("agrupaEtapas", "Informe se o tipo de fase agrupa as etapas pontuadas.")]
    [InlineData("permiteComplementacao", "Informe se o tipo de fase admite complementação documental.")]
    [InlineData("coletaInscricao", "Informe se o tipo de fase coleta inscrições.")]
    [InlineData("coletaSolicitacaoIsencao", "Informe se o tipo de fase coleta a solicitação de isenção da taxa.")]
    [InlineData("organizadaEmChamadas", "Informe se o tipo de fase é organizada em chamadas.")]
    public void Criar_SinalizadorAusente_RecusaSoAqueleSinalizador(string campo, string orientacao)
    {
        Result<FaseCanonica> resultado = Criar(sinalizadores: SinalizadoresSem(campo));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be(campo);
        resultado.Errors[0].Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
        resultado.Errors[0].Error.Message.Should().Be(orientacao);
    }

    [Fact(DisplayName = "Os cinco sinalizadores ausentes saem na ordem: agrupaEtapas, permiteComplementacao, coletaInscricao, coletaSolicitacaoIsencao, organizadaEmChamadas")]
    public void Criar_CincoSinalizadoresAusentes_RecusaNaOrdem()
    {
        Result<FaseCanonica> resultado = Criar(sinalizadores: new SinalizadoresInformados(null, null, null, null, null));

        resultado.Errors.Select(recusa => recusa.Field).Should().Equal(
            "agrupaEtapas", "permiteComplementacao", "coletaInscricao", "coletaSolicitacaoIsencao", "organizadaEmChamadas");
        resultado.Errors.Should().OnlyContain(recusa => recusa.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "Marca de isenção ausente na fase de isenção devolve só a ausência, sem a incoerência com o código")]
    public void Criar_MarcaDeIsencaoAusenteNaFaseDeIsencao_DevolveSoAAusencia()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            sinalizadores: Sinalizadores(coletaSolicitacaoIsencao: null));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("coletaSolicitacaoIsencao", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Inscrição ausente com a marca de isenção ligada não dispara a exclusividade")]
    public void Criar_InscricaoAusenteComIsencaoLigada_NaoDisparaAExclusividade()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            sinalizadores: Sinalizadores(coletaInscricao: null, coletaSolicitacaoIsencao: true));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("coletaInscricao", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Complementação ausente com agrupar etapas ligado fora da avaliação recusa a ausência e depois a coerência com o código")]
    public void Criar_AusenciaComAgrupaEtapasForaDaAvaliacao_AcumulaAusenciaECoerencia()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: "HOMOLOGACAO_INSCRICOES",
            sinalizadores: Sinalizadores(agrupaEtapas: true, permiteComplementacao: null));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("permiteComplementacao", CodigosDeValidacao.Obrigatorio),
            ("agrupaEtapas", FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao));
    }

    [Fact(DisplayName = "Chamadas ausente com inscrição e isenção ligadas fora da fase de isenção recusa a ausência, a coerência com o código e a exclusividade, nesta ordem")]
    public void Criar_AusenciaComAsDuasColetasForaDaFaseDeIsencao_AcumulaAusenciaCoerenciaEExclusividade()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: "INSCRICAO",
            sinalizadores: Sinalizadores(coletaInscricao: true, coletaSolicitacaoIsencao: true, organizadaEmChamadas: null));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("organizadaEmChamadas", CodigosDeValidacao.Obrigatorio),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.SolicitacaoIsencaoApenasNaFaseDeIsencao),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
    }

    [Fact(DisplayName = "Chamadas ausente com as duas coletas na fase de isenção recusa a ausência e depois a exclusividade")]
    public void Criar_AusenciaComAsDuasColetasNaFaseDeIsencao_AcumulaAusenciaEExclusividade()
    {
        Result<FaseCanonica> resultado = Criar(
            codigo: FaseCanonicaCatalogo.CodigoSolicitacaoIsencao,
            sinalizadores: Sinalizadores(coletaInscricao: true, coletaSolicitacaoIsencao: true, organizadaEmChamadas: null));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("organizadaEmChamadas", CodigosDeValidacao.Obrigatorio),
            ("coletaSolicitacaoIsencao", FaseCanonicaErrorCodes.ColetaInscricaoEIsencaoMutuamenteExclusivas));
    }

    [Fact(DisplayName = "Nome vazio com sinalizador ausente recusa o nome primeiro")]
    public void Criar_NomeVazioESinalizadorAusente_RecusaONomePrimeiro()
    {
        Result<FaseCanonica> resultado = Criar(nome: " ", sinalizadores: Sinalizadores(organizadaEmChamadas: null));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("nome", FaseCanonicaErrorCodes.NomeObrigatorio),
            ("organizadaEmChamadas", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Atualizar com sinalizador ausente é recusado e mantém os sinalizadores gravados")]
    public void Atualizar_SinalizadorAusente_RecusaEMantemOsGravados()
    {
        FaseCanonica fase = Criar(codigo: "INSCRICAO", sinalizadores: Sinalizadores(coletaInscricao: true)).Value!;
        SinalizadoresDaFase gravados = fase.Sinalizadores;

        Result resultado = fase.Atualizar("Inscrição", null, null, "NO_EDITAL", Sinalizadores(permiteComplementacao: null));

        resultado.Errors.Select(recusa => recusa.Field).Should().Equal("permiteComplementacao");
        fase.Sinalizadores.Should().BeSameAs(gravados);
    }

    [Fact(DisplayName = "Validar o que não depende do código recusa o sinalizador ausente")]
    public void ValidarOQueNaoDependeDoCodigo_SinalizadorAusente_Recusa()
    {
        Result resultado = FaseCanonica.ValidarOQueNaoDependeDoCodigo(
            "Fase", null, null, "NO_EDITAL", Sinalizadores(agrupaEtapas: null));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("agrupaEtapas", CodigosDeValidacao.Obrigatorio));
    }

    private static SinalizadoresInformados SinalizadoresSem(string campo) =>
        campo switch
        {
            "agrupaEtapas" => Sinalizadores(agrupaEtapas: null),
            "permiteComplementacao" => Sinalizadores(permiteComplementacao: null),
            "coletaInscricao" => Sinalizadores(coletaInscricao: null),
            "coletaSolicitacaoIsencao" => Sinalizadores(coletaSolicitacaoIsencao: null),
            _ => Sinalizadores(organizadaEmChamadas: null),
        };
}
