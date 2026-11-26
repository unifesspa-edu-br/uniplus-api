namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo divulgado com o período de inscrição de 1 a 31 de janeiro de 2026, para os testes que conferem
/// se as inscrições estão abertas. Cada propriedade desfaz um passo da leitura do período: a divulgação, a
/// versão que ela aponta, a versão que o codec lê e o formulário de inscrição.
/// </summary>
internal sealed class ProcessoDivulgadoParaOPeriodoDeTeste
{
    private const string VersaoDeEsquemaQueNaoSeLe = "9.9.9";

    public Guid ProcessoId { get; } = Guid.CreateVersion7();

    public DateTimeOffset InicioDoPeriodo { get; } = DivulgacaoDeTeste.InicioDoPeriodo;

    public DateTimeOffset FimDoPeriodo { get; } = DivulgacaoDeTeste.FimDoPeriodo;

    public bool Divulgado { get; init; } = true;

    public bool ComVersaoDaDivulgacao { get; init; } = true;

    public bool VersaoLegivel { get; init; } = true;

    public bool ComFormularioDeInscricao { get; init; } = true;

    private Guid AtoCriadorId { get; } = Guid.CreateVersion7();

    public VersaoConfiguracao Versao { get; private set; } = null!;

    /// <summary>Registra o processo nos repositórios e no codec de teste, sem desfazer o que outros processos registraram.</summary>
    public void Registrar(ICertameDivulgadoRepository certames, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs)
    {
        Versao = ApoioDaListaDeInscricoes.NovaVersao(ProcessoId, VersaoLegivel ? ApoioDaListaDeInscricoes.SchemaVersion : VersaoDeEsquemaQueNaoSeLe);
        if (Divulgado)
        {
            certames.ObterParaLeituraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(Certame());
        }

        IReadOnlyList<VersaoConfiguracao> versoes = ComVersaoDaDivulgacao ? [Versao] : [];
        processos.ObterVersoesPorAtoCriadorAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(AtoCriadorId)), Arg.Any<CancellationToken>()).Returns(versoes);
        codecs.LeAVersao(ApoioDaListaDeInscricoes.SchemaVersion).Returns(true);
        codecs.Reidratar(Arg.Is<VersaoConfiguracao>(v => v.Id == Versao.Id)).Returns(Result<EnvelopeReidratado>.Success(Envelope()));
    }

    /// <summary>Os repositórios e o codec que respondem por todos os processos informados.</summary>
    public static (ICertameDivulgadoRepository Certames, IProcessoSeletivoRepository Processos, IRegistroCodecsEnvelope Codecs) Repositorios(
        params ProcessoDivulgadoParaOPeriodoDeTeste[] processos)
    {
        ICertameDivulgadoRepository certames = Substitute.For<ICertameDivulgadoRepository>();
        IProcessoSeletivoRepository repositorioDeProcessos = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        foreach (ProcessoDivulgadoParaOPeriodoDeTeste processo in processos)
        {
            processo.Registrar(certames, repositorioDeProcessos, codecs);
        }

        return (certames, repositorioDeProcessos, codecs);
    }

    private CertameDivulgado Certame() => CertameDivulgado.Criar(
        ProcessoId, numeroVersao: 1, AtoCriadorId, new string('a', 64), versaoProjecao: "1",
        new FacetasDoCertameDivulgado("certame-do-periodo", "Certame do período", "001/2026", ["AC"], InicioDoPeriodo, FimDoPeriodo),
        """{"nome":"documento"}""", InicioDoPeriodo);

    private EnvelopeReidratado Envelope()
    {
        ConfiguracaoPublicada configuracao = DivulgacaoDeTeste.ConfiguracaoDe(ProcessoSeletivoConformeBuilder.Criar("PS do período"));
        return new EnvelopeReidratado(
            ComFormularioDeInscricao ? configuracao : ConfiguracaoPublicadaDeTeste.ComColeta(configuracao, new ColetaDeclarada([], [], [], [], [], [], null)),
            DadosEdital.Criar("001/2026", InicioDoPeriodo, FimDoPeriodo, Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Belem", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada());
    }
}
