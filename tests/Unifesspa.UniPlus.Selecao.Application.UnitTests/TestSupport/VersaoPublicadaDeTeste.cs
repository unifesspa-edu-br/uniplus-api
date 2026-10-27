namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Dá ao processo uma versão publicada cujo envelope congelou a classificação dos tipos de ato dados,
/// sem montar o codec real.
/// </summary>
internal static class VersaoPublicadaDeTeste
{
    public static void Publicar(
        ProcessoSeletivo processo,
        IProcessoSeletivoRepository repositorio,
        IRegistroCodecsEnvelope codecs,
        params TipoDeAtoCongelado[] congelados) =>
        Publicar(processo, processo, repositorio, codecs, congelados);

    /// <summary>
    /// Como <see cref="Publicar(ProcessoSeletivo, IProcessoSeletivoRepository, IRegistroCodecsEnvelope, TipoDeAtoCongelado[])"/>,
    /// mas com o grafo vindo de outra instância: a etapa é editada no lugar, e o grafo publicado tem de
    /// sobreviver à edição do agregado vivo.
    /// </summary>
    public static void Publicar(
        ProcessoSeletivo processo,
        ProcessoSeletivo grafoDe,
        IProcessoSeletivoRepository repositorio,
        IRegistroCodecsEnvelope codecs,
        params TipoDeAtoCongelado[] congelados)
    {
        VersaoConfiguracao versao = Abrir(processo, repositorio);
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            new GrafoConfiguracao(
                [.. grafoDe.Etapas], OfertaAtendimentoEspecializado.Criar([], [], []).Value!, [.. grafoDe.DistribuicaoVagas], grafoDe.BonusRegional,
                [.. grafoDe.CriteriosDesempate], ConfiguracaoClassificacao.Criar(
                    ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!, null, null,
                    ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!, [],
                    baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []).Value!,
                [.. grafoDe.CronogramaFases], [.. grafoDe.DocumentosExigidos], [], null,
                numeroDeOpcoesDeCurso: 1),
            DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
            tiposDeAtoCongelados: congelados.ToDictionary(static c => c.Codigo, StringComparer.Ordinal))));
    }

    /// <summary>A versão publicada que o codec não consegue ler.</summary>
    public static void PublicarIlegivel(
        ProcessoSeletivo processo,
        IProcessoSeletivoRepository repositorio,
        IRegistroCodecsEnvelope codecs,
        DomainError falhaDoCodec)
    {
        VersaoConfiguracao versao = Abrir(processo, repositorio);
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Failure(falhaDoCodec));
    }

    private static VersaoConfiguracao Abrir(ProcessoSeletivo processo, IProcessoSeletivoRepository repositorio)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id, [1], "0.0.35", "canonical-json/sha256@v1", Guid.CreateVersion7(), new string('a', 64), "sub", DateTimeOffset.UnixEpoch);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versao);
        return versao;
    }
}
