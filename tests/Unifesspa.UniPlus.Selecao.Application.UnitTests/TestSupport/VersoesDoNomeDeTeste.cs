namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Um processo com várias versões que congelaram o nome com classificações e bases legais diferentes, ou ilegíveis.</summary>
internal static class VersoesDoNomeDeTeste
{
    public const string Hipotese = InscricaoAdministrativaDeTeste.Hipotese;

    public static (VersaoConfiguracao Versao, Result<EnvelopeReidratado> Envelope) VersaoComNome(string classificacao, string hipotese)
    {
        InscricaoAdministrativaDeTeste mundo = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("NOME", classificacao, TipoRenderizacao.Texto, null, hipoteseLegal: hipotese);
        InscricaoAdministrativaDeTeste.Mundo montado = mundo.Montar();
        VersaoConfiguracao versao = montado.Processos.ObterVersoesAsync(mundo.ProcessoId, default).Result[0];

        return (versao, montado.Codecs.Reidratar(versao));
    }

    public static (VersaoConfiguracao Versao, Result<EnvelopeReidratado> Envelope) VersaoIlegivel() =>
        (VersaoComNome("PESSOAL", Hipotese).Versao, Result<EnvelopeReidratado>.Failure(new DomainError("Codec.Ilegivel", "Versão ilegível.")));

    public static (IProcessoSeletivoRepository Processos, IRegistroCodecsEnvelope Codecs) Processo(
        Guid processoId, params (VersaoConfiguracao Versao, Result<EnvelopeReidratado> Envelope)[] versoes)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterUnidadeAdministradoraAsync(processoId, Arg.Any<CancellationToken>()).Returns(DecisoesDeAcessoDeTeste.UnidadeDona);
        IReadOnlyList<VersaoConfiguracao> todas = [.. versoes.Select(static v => v.Versao)];
        processos.ObterVersoesAsync(processoId, Arg.Any<CancellationToken>()).Returns(todas);

        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        IReadOnlyList<CapacidadeCodec> capacidades =
            [.. todas.Select(static v => v.SchemaVersion).Distinct().Select(static s => new CapacidadeCodec(s, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null))];
        codecs.Capacidades.Returns(capacidades);
        foreach ((VersaoConfiguracao versao, Result<EnvelopeReidratado> envelope) in versoes)
        {
            codecs.Reidratar(versao).Returns(envelope);
        }

        return (processos, codecs);
    }
}
