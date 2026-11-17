namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.MotivoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// A conferência dos motivos nas transições que congelam versão (UNI-REQ-0122, UNI-REQ-0123): recopia a descrição vigente da
/// escolha que entra na versão que nasce, recusa o motivo desativado e confere a cobertura sobre a escolha atual.
/// </summary>
public sealed class ConferenciaDosMotivosAoCongelarTests
{
    private const int VersaoQueNasce = 1;

    [Fact(DisplayName = "A falta de cobertura recusa, uma vez por sentido que o fundamento de isenção exige")]
    public async Task SemCobertura_Recusa()
    {
        MotivoDeDecisao origem = Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", null);

        Result resultado = await ConferirAsync(ProcessoComTaxaDeIsencao(), new MotivoEscolhidoComOrigem(Escolha(origem), origem));

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(e => e.Error.Code == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    [Fact(DisplayName = "A escolha de motivo desativado recusa antes da cobertura")]
    public async Task MotivoDesativado_Recusa()
    {
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = Escolha(origem);
        origem.Desativar();

        Result resultado = await ConferirAsync(NovoProcessoConforme(), new MotivoEscolhidoComOrigem(escolhido, origem));

        resultado.Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoDesativado);
    }

    [Fact(DisplayName = "Com cobertura completa a conferência aceita e recopia a descrição vigente do cadastro")]
    public async Task ComCobertura_AceitaERecopia()
    {
        MotivoDeDecisao origem = Motivo();
        MotivoDoProcesso escolhido = Escolha(origem);
        origem.Atualizar("Renda per capita superior ao limite legal.").IsSuccess.Should().BeTrue();

        Result resultado = await ConferirAsync(NovoProcessoConforme(), new MotivoEscolhidoComOrigem(escolhido, origem));

        resultado.IsSuccess.Should().BeTrue();
        escolhido.Descricao.Should().Be("Renda per capita superior ao limite legal.");
    }

    [Fact(DisplayName = "Sem nenhum motivo escolhido, a falta de cobertura recusa uma vez por decisão")]
    public async Task SemNenhumMotivo_RecusaPorCobertura()
    {
        Result resultado = await ConferirAsync(ProcessoComTaxaDeIsencao());

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(e => e.Error.Code == MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
    }

    private static Task<Result> ConferirAsync(ProcessoSeletivo processo, params MotivoEscolhidoComOrigem[] escolhidos) =>
        new ConferenciaDosMotivosAoCongelar(MotivosDoProcessoDeTeste.Com(escolhidos)).ConferirAsync(processo, VersaoQueNasce, CancellationToken.None);

    private static MotivoDoProcesso Escolha(MotivoDeDecisao origem)
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        return Escolhido(processo, LugarDaPrimeiraFase(processo), origem);
    }

    private static ProcessoSeletivo ProcessoComTaxaDeIsencao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(true, 100m, [FundamentoIsencaoCodigo.CadastroUnico]).Value!);

        return processo;
    }
}
