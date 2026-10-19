namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O registro de codecs dos testes que não se importam com o que a versão vigente congelou.</summary>
internal static class CodecsDeTeste
{
    /// <summary>
    /// Reidrata qualquer versão como uma configuração sem tipo de ato congelado: a retificação lê da versão
    /// vigente a classificação já congelada, e esses testes só exercitam outros gates.
    /// </summary>
    public static IRegistroCodecsEnvelope QueReidrataSemTiposDeAto()
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS de teste");
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(_ => Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            new GrafoConfiguracao(
                [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas],
                processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
                [], [.. processo.DocumentosExigidos], [], null, numeroDeOpcoesDeCurso: 1),
            DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null)));
        return registro;
    }
}
