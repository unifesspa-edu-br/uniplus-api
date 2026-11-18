namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// As portas dos serviços de aplicação da inscrição, todas substitutas; o teste configura só a que o
/// cenário exige e pede o serviço montado com elas.
/// </summary>
internal sealed class PortasDaInscricaoDeTeste(Guid processoId)
{
    public IInscricaoRepository Inscricoes { get; } = Substitute.For<IInscricaoRepository>();

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IRegistroCodecsEnvelope Codecs { get; init; } = Substitute.For<IRegistroCodecsEnvelope>();

    public IDocumentoDaInscricaoRepository Documentos { get; init; } = Substitute.For<IDocumentoDaInscricaoRepository>();

    public void ComInscricao(Cpf cpf, Inscricao? inscricao) =>
        Inscricoes.ObterAtivaPorProcessoECpfAsync(processoId, cpf, Arg.Any<CancellationToken>()).Returns(inscricao);

    public InscricaoAtivaDoCandidato InscricaoAtiva() => new(Inscricoes);

}
