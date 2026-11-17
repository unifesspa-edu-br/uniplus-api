namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Avisos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AvisosDaNumeracaoTests
{
    private const string MensagemDeDoisConflitos = "Número EDITAL 13/2026 do órgão CEPS já consta em 2 outro(s) ato(s).";

    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly AtoNormativo _ato = new AtoNormativoDeTeste().Registrar();

    [Fact(DisplayName = "Ato sem número não tem aviso e nem consulta a numeração")]
    public async Task Calcular_AtoSemNumero_NaoConsultaNemAvisa()
    {
        AtoNormativo semNumero = (new AtoNormativoDeTeste() with { Numero = null }).Registrar();

        IReadOnlyList<AvisoNumeracao> avisos = await AvisosDaNumeracao.CalcularAsync(_atos, semNumero, [], CancellationToken.None);

        avisos.Should().BeEmpty();
        await _atos.DidNotReceiveWithAnyArgs().ListarIdsComMesmaNumeracaoAsync(default, CancellationToken.None);
    }

    [Fact(DisplayName = "Consulta os atos pela numeração do ato")]
    public async Task Calcular_ConsultaPelaNumeracaoDoAto()
    {
        _atos.ListarIdsComMesmaNumeracaoAsync(Arg.Any<NumeracaoDoAto>(), Arg.Any<CancellationToken>()).Returns([]);

        await AvisosDaNumeracao.CalcularAsync(_atos, _ato, [], CancellationToken.None);

        await _atos.Received(1).ListarIdsComMesmaNumeracaoAsync(
            new NumeracaoDoAto("CEPS", "EDITAL", 2026, "13"), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem outro ato com a mesma numeração não há aviso")]
    public async Task Calcular_SemConflitantes_NaoAvisa()
    {
        _atos.ListarIdsComMesmaNumeracaoAsync(Arg.Any<NumeracaoDoAto>(), Arg.Any<CancellationToken>()).Returns([]);

        (await AvisosDaNumeracao.CalcularAsync(_atos, _ato, [], CancellationToken.None)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os atos de mesma numeração viram um aviso com o código, a mensagem e a lista dos conflitantes")]
    public async Task Calcular_ComConflitantes_DevolveOAvisoDeNumeroDuplicado()
    {
        Guid[] conflitantes = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _atos.ListarIdsComMesmaNumeracaoAsync(Arg.Any<NumeracaoDoAto>(), Arg.Any<CancellationToken>()).Returns(conflitantes);

        IReadOnlyList<AvisoNumeracao> avisos = await AvisosDaNumeracao.CalcularAsync(_atos, _ato, [], CancellationToken.None);

        AvisoNumeracao aviso = avisos.Should().ContainSingle().Which;
        aviso.Codigo.Should().Be(AtoNormativoRegras.AvisoNumeroDuplicado);
        aviso.Mensagem.Should().Be(MensagemDeDoisConflitos);
        aviso.AtosConflitantes.Should().Equal(conflitantes);
    }

    [Fact(DisplayName = "A linhagem excluída sai dos conflitantes e a mensagem conta só os que ficam")]
    public async Task Calcular_ExcluiALinhagem_ContaSoOsDeFora()
    {
        Guid daLinhagem = Guid.CreateVersion7();
        Guid[] deFora = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _atos.ListarIdsComMesmaNumeracaoAsync(Arg.Any<NumeracaoDoAto>(), Arg.Any<CancellationToken>())
            .Returns([deFora[0], daLinhagem, deFora[1]]);

        IReadOnlyList<AvisoNumeracao> avisos = await AvisosDaNumeracao.CalcularAsync(_atos, _ato, [daLinhagem], CancellationToken.None);

        AvisoNumeracao aviso = avisos.Should().ContainSingle().Which;
        aviso.AtosConflitantes.Should().Equal(deFora);
        aviso.Mensagem.Should().Be(MensagemDeDoisConflitos);
    }

    [Fact(DisplayName = "Quando só a linhagem tem a mesma numeração não há aviso")]
    public async Task Calcular_SoALinhagemTemANumeracao_NaoAvisa()
    {
        Guid daLinhagem = Guid.CreateVersion7();
        _atos.ListarIdsComMesmaNumeracaoAsync(Arg.Any<NumeracaoDoAto>(), Arg.Any<CancellationToken>())
            .Returns([daLinhagem]);

        (await AvisosDaNumeracao.CalcularAsync(_atos, _ato, [daLinhagem], CancellationToken.None)).Should().BeEmpty();
    }

    [Fact(DisplayName = "NumeroDuplicado nomeia série, número, ano e órgão do ato e a quantidade de conflitantes")]
    public void NumeroDuplicado_NomeiaANumeracaoEContaOsConflitantes()
    {
        AvisoNumeracao aviso = AvisosDaNumeracao.NumeroDuplicado(_ato, [Guid.CreateVersion7()]);

        aviso.Mensagem.Should().Be("Número EDITAL 13/2026 do órgão CEPS já consta em 1 outro(s) ato(s).");
    }
}
