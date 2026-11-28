namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoAtoAvulso;

/// <summary>
/// As recusas do tipo e da correção do ato avulso, cada uma no campo do pedido que a causa, e o que uma recusa impede de
/// consultar.
/// </summary>
public sealed class RecusasDoTipoEDaCorrecaoDoAtoAvulsoTests
{
    private readonly CenarioDaPublicacaoDoAtoAvulso _cenario = new();

    [Fact(DisplayName = "Tipo sem versão vigente no dia do instante é recusado no campo do tipo")]
    public async Task TipoSemVersaoVigente_RecusaNoCampoDoTipo()
    {
        _cenario.Tipos.ObterVigenteAsync("AVISO", DataDoAto, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        await _cenario.DeveRecusar(_cenario.Comando(), "ProcessoSeletivo.TipoDeAtoSemVersaoVigente", "tipoAtoCodigo");
    }

    [Fact(DisplayName = "Tipo que congela configuração não é documento avulso e é recusado no campo do tipo")]
    public async Task TipoQueCongela_RecusaNoCampoDoTipo()
    {
        _cenario.Tipo(congela: true);

        await _cenario.DeveRecusar(_cenario.Comando(), AtoDoProcessoErrorCodes.TipoCongelaConfiguracao, "tipoAtoCodigo");
    }

    [Theory(DisplayName = "Convocação e chamada, como o seed as declara, não são documento avulso")]
    [InlineData("CONVOCACAO")]
    [InlineData("CHAMADA")]
    [InlineData("RESULTADO_CLASSIFICACAO")]
    public async Task TipoNaoPublicavelComoAvulso_Recusa(string codigo)
    {
        _cenario.Tipos.ObterVigenteAsync(codigo, DataDoAto, Arg.Any<CancellationToken>()).Returns(SeedDosTiposDeAto.Tipo(codigo));

        await _cenario.DeveRecusar(_cenario.Comando(tipo: codigo), AtoDoProcessoErrorCodes.TipoNaoPublicavelComoAvulso, "tipoAtoCodigo");
    }

    [Fact(DisplayName = "A errata, como o seed a declara, sem ato corrigido é recusada no campo do ato corrigido")]
    public async Task ErrataSemAtoCorrigido_RecusaNoCampoDoAtoCorrigido()
    {
        _cenario.Tipos.ObterVigenteAsync("ERRATA", DataDoAto, Arg.Any<CancellationToken>()).Returns(SeedDosTiposDeAto.Tipo("ERRATA"));

        await _cenario.DeveRecusar(_cenario.Comando(tipo: "ERRATA"), AtoDoProcessoErrorCodes.TipoExigeAtoCorrigido, "atoCorrigidoId");
    }

    [Fact(DisplayName = "O ato corrigido sem registro na Seleção é recusado no campo do ato corrigido")]
    public async Task AtoCorrigidoSemRegistro_Recusa()
    {
        await _cenario.DeveRecusar(
            _cenario.Comando(atoCorrigidoId: Guid.CreateVersion7(), motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoNaoEncontrado, "atoCorrigidoId");
    }

    [Fact(DisplayName = "O ato corrigido de outro processo seletivo é recusado")]
    public async Task AtoCorrigidoDeOutroProcesso_Recusa()
    {
        Guid deOutroProcesso = _cenario.AtoPublicadoDoProcesso(Guid.CreateVersion7());

        await _cenario.DeveRecusar(
            _cenario.Comando(atoCorrigidoId: deOutroProcesso, motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoDeOutroProcesso, "atoCorrigidoId");
    }

    [Fact(DisplayName = "O ato corrigido que congela configuração, como o edital, não é corrigido por documento avulso")]
    public async Task AtoCorrigidoQueCongela_Recusa()
    {
        Guid edital = _cenario.AtoPublicadoDoProcesso(_cenario.ProcessoId, aoCongelar: true);

        await _cenario.DeveRecusar(
            _cenario.Comando(atoCorrigidoId: edital, motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoDeOutraClasse, "atoCorrigidoId");
    }

    [Fact(DisplayName = "O ato corrigido que é a publicação de um item do edital não é corrigido por documento avulso")]
    public async Task AtoCorrigidoQueEhItemDoEdital_Recusa()
    {
        Guid resultado = _cenario.AtoCorrigivel();
        _cenario.Atos.ExisteItemDoEditalEntreAsync(Arg.Is<IReadOnlyCollection<Guid>>(c => c.Contains(resultado)), Arg.Any<CancellationToken>())
            .Returns(true);

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(atoCorrigidoId: resultado, motivo: "Erro"));

        FieldError recusa = resposta.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("atoCorrigidoId");
        recusa.Error.Code.Should().Be(AtoDoProcessoErrorCodes.AtoCorrigidoEhItemDoEdital);
        recusa.Error.Message.Should().Contain("publicação do próprio item", "a recusa orienta a corrigir pela publicação do item");
        await _cenario.NadaFoiEscrito();
    }

    [Fact(DisplayName = "A segunda correção do mesmo ato é recusada, e a recusa orienta a corrigir a cabeça da cadeia")]
    public async Task SegundaCorrecao_Recusa()
    {
        Guid aviso = _cenario.AtoPublicadoDoProcesso(_cenario.ProcessoId);
        _cenario.Atos.ObterAtoQueCorrigeAsync(aviso, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(atoCorrigidoId: aviso, motivo: "Erro"));

        resposta.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.AtoCorrigidoJaCorrigido);
        resposta.Error.Message.Should().Contain("mais recente da cadeia", "a recusa orienta a corrigir a cabeça da cadeia");
        await _cenario.NadaFoiEscrito();
    }

    [Fact(DisplayName = "O ato corrigido recusado não tem a cadeia lida, porque não há cadeia de um ato que não é do processo")]
    public async Task AtoCorrigidoRecusado_NaoLeACadeia()
    {
        Guid deOutroProcesso = _cenario.AtoPublicadoDoProcesso(Guid.CreateVersion7());

        await _cenario.Publicar(_cenario.Comando(atoCorrigidoId: deOutroProcesso, motivo: "Erro"));

        await _cenario.Atos.DidNotReceive().ListarCadeiaDeCorrecaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cenario.Atos.DidNotReceive().ExisteItemDoEditalEntreAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A correção sem motivo é recusada no campo do motivo, e o ato corrigido continua consultado")]
    public async Task CorrecaoSemMotivo_RecusaNoCampoDoMotivoEConsultaOAtoCorrigido()
    {
        Guid aviso = _cenario.AtoCorrigivel();

        await _cenario.DeveRecusar(
            _cenario.Comando(atoCorrigidoId: aviso), AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio, "motivoDaCorrecao");

        await _cenario.Atos.Received().ObterProcessoDoAtoAsync(aviso, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O motivo sem ato corrigido numa errata dá só a causa do ato corrigido obrigatório: o tipo não é julgado")]
    public async Task MotivoSemAtoCorrigidoNumaErrata_NaoJulgaOQueOTipoExige()
    {
        _cenario.Tipos.ObterVigenteAsync("ERRATA", DataDoAto, Arg.Any<CancellationToken>()).Returns(SeedDosTiposDeAto.Tipo("ERRATA"));

        await _cenario.DeveRecusar(
            _cenario.Comando(tipo: "ERRATA", motivo: "Data errada"), AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio, "atoCorrigidoId");

        await _cenario.Atos.DidNotReceive().ObterProcessoDoAtoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O ato corrigido inválido é recusado na forma, e nenhuma consulta o lê")]
    public async Task AtoCorrigidoInvalido_RecusaNaFormaENaoConsulta()
    {
        await _cenario.DeveRecusar(
            _cenario.Comando(atoCorrigidoId: Guid.Empty, motivo: "Erro"), AtoDoProcessoErrorCodes.AtoCorrigidoInvalido, "atoCorrigidoId");

        await _cenario.Atos.DidNotReceive().ObterProcessoDoAtoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Numa errata sem ato corrigido, o tipo que congela é recusado e não se julga o que o tipo exige")]
    public async Task ErrataQueCongelaSemAtoCorrigido_NaoJulgaOQueOTipoExige()
    {
        _cenario.Tipos.ObterVigenteAsync("ERRATA", DataDoAto, Arg.Any<CancellationToken>())
            .Returns(SeedDosTiposDeAto.Tipo("ERRATA") with { CongelaConfiguracao = true });

        await _cenario.DeveRecusar(_cenario.Comando(tipo: "ERRATA"), AtoDoProcessoErrorCodes.TipoCongelaConfiguracao, "tipoAtoCodigo");
    }

    [Fact(DisplayName = "O pedido sem ato corrigido não consulta ato nenhum")]
    public async Task SemAtoCorrigido_NaoConsultaAtos()
    {
        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando());

        resposta.IsSuccess.Should().BeTrue();
        await _cenario.Atos.DidNotReceive().ObterProcessoDoAtoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
