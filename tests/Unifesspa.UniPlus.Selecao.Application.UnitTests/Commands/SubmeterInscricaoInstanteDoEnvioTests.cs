namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// A avaliação do envio usa um instante só, mesmo quando o dia vira durante a chamada.
/// </summary>
public sealed class SubmeterInscricaoInstanteDoEnvioTests
{
    private static readonly FatoColetado DataAteOEnvio = FatoColetadoDeTeste.Coletando("DATA_DO_EVENTO", 1)
        .ComRotulo("Data do evento")
        .DoTipo(TipoRenderizacao.Data)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComRestricoes(new FaixaDeData(minima: null, LimiteDeData.NoEnvio(anos: 0, dias: 0)))
        .Construir();

    private static InscricaoRequest RequestComDataDoEvento(Cpf cpf, VersaoTermoEscolhida termo, ConfiguracaoPublicada configuracao, string dataDoEvento)
    {
        InscricaoRequest request = RequestComApelidoETermoAceito(cpf, termo, configuracao);
        Dictionary<string, JsonElement> respostas = new(request.Respostas!, StringComparer.Ordinal)
        {
            ["DATA_DO_EVENTO"] = JsonSerializer.SerializeToElement(dataDoEvento),
        };
        return request with { Respostas = respostas };
    }

    [Fact(DisplayName = "O relógio que vira o dia durante o envio não muda o dia de que a faixa de data se conta")]
    public async Task Handle_ViradaDoDiaDuranteOEnvio_AvaliaNoDiaDoInstanteUnico()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]));

        // 23:59:59,9 de 15/01 em Belém; cada leitura seguinte do relógio já cai em 16/01.
        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(RequestComDataDoEvento(cenario.Cpf, cenario.Termo, cenario.Configuracao, "2026-01-16"), relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200)));

        resultado.Errors.Should().ContainSingle(static e => e.Field == "DATA_DO_EVENTO" && e.Error.Code == InscricaoErrorCodes.DataForaDaFaixa)
            .Which.Error.Message.Should().Be("Informe uma data até 15/01/2026.");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O comprovante leva o mesmo instante com que o envio foi avaliado")]
    public async Task Handle_Envio_ComprovanteLevaOInstanteDaAvaliacao()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]));

        Result<ReciboDaInscricaoDto> resultado = await cenario.SubmeterAsync(RequestComDataDoEvento(cenario.Cpf, cenario.Termo, cenario.Configuracao, "2026-01-15"),
            relogio: new RelogioQueAvanca(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero), TimeSpan.FromMilliseconds(200)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Instante.Should().Be(new DateTimeOffset(2026, 1, 16, 2, 59, 59, 900, TimeSpan.Zero));
    }

    [Fact(DisplayName = "A correção da inscrição enviada conta a faixa de data do instante da correção")]
    public async Task Handle_Correcao_ContaAFaixaDoNovoEnvio()
    {
        CenarioDoEnvioDeTeste cenario = new(PersonasDeTeste.FernandaDrumond, MontarVersaoComItemObrigatorioETermo([DataAteOEnvio]));

        Result<ReciboDaInscricaoDto> envio = await cenario.SubmeterAsync(RequestComDataDoEvento(cenario.Cpf, cenario.Termo, cenario.Configuracao, "2026-01-15"),
            relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        envio.IsSuccess.Should().BeTrue(envio.Error?.Message);

        Result<ReciboDaInscricaoDto> correcao = await cenario.SubmeterAsync(RequestComDataDoEvento(cenario.Cpf, cenario.Termo, cenario.Configuracao, "2026-01-16"),
            relogio: new RelogioFixo(new DateTimeOffset(2026, 1, 16, 12, 0, 0, TimeSpan.Zero)));

        correcao.IsSuccess.Should().BeTrue(correcao.Error?.Message);
    }

    /// <summary>Um relógio cuja leitura seguinte é sempre mais tarde que a anterior, pelo passo.</summary>
    private sealed class RelogioQueAvanca(DateTimeOffset inicio, TimeSpan passo) : TimeProvider
    {
        private int _leituras;

        public override DateTimeOffset GetUtcNow() => inicio + (passo * _leituras++);
    }
}
