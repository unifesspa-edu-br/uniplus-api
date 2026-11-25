namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As condições do predicado vindas do pedido acumulam as recusas de todas as cláusulas e condições, cada uma no
/// caminho da condição e no campo de origem, tanto na regra de aplicabilidade da modalidade quanto nas regras padrão do fato.
/// </summary>
public sealed class RecusasAcumuladasDasCondicoesDoPredicadoTests
{
    private const string FatoVazioNaoInformado = "CondicaoDnf.FatoObrigatorio";
    private const string OperadorDesconhecido = "CondicaoDnf.OperadorInvalido";

    private static CondicaoDoPredicadoInput Condicao(string fato, string operador) =>
        new(fato, operador, JsonSerializer.SerializeToElement(true));

    private static async Task<Result<Guid>> CriarModalidadeAsync(IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> regra) =>
        await CriarModalidadeCommandHandler.Handle(
            new CriarModalidadeCommand("PSVR_AMPLA", NaturezaLegal: "AMPLA", RegraDeAplicabilidade: regra),
            Substitute.For<IModalidadeRepository>(),
            Substitute.For<IFatoCandidatoRepository>(),
            Substitute.For<IPrecedenciaFaseRepository>(),
            Substitute.For<IUnitOfWork>(),
            CancellationToken.None);

    private static Task<Result> DefinirRegrasPadraoAsync(IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>>?[] regras) =>
        DefinirRegrasPadraoCommandHandler.Handle(
            new DefinirRegrasPadraoCommand(Guid.CreateVersion7(), [.. regras.Select(static quando => new RegraPadraoInput(null, quando))]),
            Substitute.For<IFatoCandidatoRepository>(),
            Substitute.For<IPrecedenciaFaseRepository>(),
            Substitute.For<IUnitOfWork>(),
            CancellationToken.None);

    [Fact(DisplayName = "Regra de aplicabilidade com fato vazio e operador desconhecido na mesma condição recusa os dois campos")]
    public async Task Criar_RegraComFatoVazioEOperadorDesconhecido_AcumulaOsDoisCampos()
    {
        Result<Guid> resultado = await CriarModalidadeAsync([[Condicao("", "XPTO")]]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regraDeAplicabilidade[0][0].fato", FatoVazioNaoInformado),
            ("regraDeAplicabilidade[0][0].operador", OperadorDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Regra de aplicabilidade com duas condições inválidas recusa cada uma na sua posição")]
    public async Task Criar_RegraComDuasCondicoesInvalidas_RecusaCadaUmaNaSuaPosicao()
    {
        Result<Guid> resultado = await CriarModalidadeAsync([[Condicao("", "IGUAL")], [Condicao("COR_RACA", "XPTO")]]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regraDeAplicabilidade[0][0].fato", FatoVazioNaoInformado),
            ("regraDeAplicabilidade[1][0].operador", OperadorDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Regra de aplicabilidade com condição nula mantém o código próprio do módulo, na posição da condição")]
    public async Task Criar_RegraComCondicaoNulaEOutraInvalida_TraduzSoACondicaoNula()
    {
        Result<Guid> resultado = await CriarModalidadeAsync([[null!, Condicao("COR_RACA", "XPTO")]]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regraDeAplicabilidade[0][0]", ModalidadeErrorCodes.RegraDeAplicabilidadeMalformada),
            ("regraDeAplicabilidade[0][1].operador", OperadorDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Regras padrão com condições inválidas em duas regras recusam cada uma na posição da regra e da condição")]
    public async Task DefinirRegrasPadrao_DuasRegrasInvalidas_AcumulaPorRegraECondicao()
    {
        Result resultado = await DefinirRegrasPadraoAsync(
            [[[Condicao("", "XPTO")]], [[Condicao("COR_RACA", "IGUAL")], [Condicao("COR_RACA", "XPTO")]]]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regras[0].quando[0][0].fato", FatoVazioNaoInformado),
            ("regras[0].quando[0][0].operador", OperadorDesconhecido),
            ("regras[1].quando[1][0].operador", OperadorDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Regra padrão sem contribuição aponta o próprio item")]
    public async Task DefinirRegrasPadrao_ContribuicaoEmBranco_RecusaNoItem()
    {
        DefinirRegrasPadraoCommand comando = new(Guid.CreateVersion7(), [new RegraPadraoInput(" ", [])]);
        Result resultado = await DefinirRegrasPadraoCommandHandler.Handle(
            comando, Substitute.For<IFatoCandidatoRepository>(), Substitute.For<IPrecedenciaFaseRepository>(),
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "regras[0]" && e.Error.Code == "RegraDerivacao.ContribuiObrigatorio");
    }

    [Fact(DisplayName = "Regra padrão com condição nula mantém o código próprio do módulo")]
    public async Task DefinirRegrasPadrao_CondicaoNula_MantemOCodigoDoModulo()
    {
        Result resultado = await DefinirRegrasPadraoAsync([[[null!]]]);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == "regras[0].quando[0][0]" && e.Error.Code == FatoCandidatoErrorCodes.RegraPadraoMalformada);
    }
}
