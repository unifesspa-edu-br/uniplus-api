namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O registro de codecs dos testes que não se importam com o que a versão vigente congelou.</summary>
internal static class CodecsDeTeste
{
    /// <summary>
    /// Reidrata qualquer versão como uma configuração sem tipo de ato congelado: a retificação lê da versão
    /// vigente a classificação já congelada, e esses testes só exercitam outras verificações.
    /// </summary>
    public static IRegistroCodecsEnvelope QueReidrataSemTiposDeAto()
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS de teste");
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(_ => Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            ConfiguracaoDoProcesso(processo),
            DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada())));
        return registro;
    }

    private static ConfiguracaoPublicada ConfiguracaoDoProcesso(ProcessoSeletivo processo) => new(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, []),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. processo.DistribuicaoVagas], 1, processo.BonusRegional is not null,
                processo.BonusRegional, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            new ColetaDeclarada([], [], [], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);
}
