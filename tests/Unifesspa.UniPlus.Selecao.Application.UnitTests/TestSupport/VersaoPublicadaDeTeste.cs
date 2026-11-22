namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
    /// mas com a configuração vinda de outra instância: a etapa é editada no lugar, e a configuração publicada tem de
    /// sobreviver à edição do agregado vivo.
    /// </summary>
    public static void Publicar(
        ProcessoSeletivo processo,
        ProcessoSeletivo processoDeOrigem,
        IProcessoSeletivoRepository repositorio,
        IRegistroCodecsEnvelope codecs,
        params TipoDeAtoCongelado[] congelados)
    {
        VersaoConfiguracao versao = Abrir(processo, repositorio);
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            ConfiguracaoDoProcesso(processoDeOrigem),
            DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada(TiposDeAtoCongelados: congelados.ToDictionary(static c => c.Codigo, StringComparer.Ordinal)))));
    }

    private static ConfiguracaoPublicada ConfiguracaoDoProcesso(ProcessoSeletivo processoDeOrigem) => new(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processoDeOrigem.CronogramaFases]),
            new AvaliacaoDeclarada([.. processoDeOrigem.Etapas], [.. processoDeOrigem.CriteriosDesempate], ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
            {
                RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)),
                RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)),
            }).Value!),
            new VagasDeclaradas([.. processoDeOrigem.DistribuicaoVagas], 1, processoDeOrigem.BonusRegional is not null,
                processoDeOrigem.BonusRegional, null, AtendimentoEspecializado.Criar([], [], []).Value!),
            new ExigenciasDeclaradas([.. processoDeOrigem.DocumentosExigidos], []),
            new ColetaDeclarada([], [], [], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

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
            processo.Id, new DocumentoCongelado([1], "0.0.57", "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "sub", DateTimeOffset.UnixEpoch);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versao);
        return versao;
    }
}
