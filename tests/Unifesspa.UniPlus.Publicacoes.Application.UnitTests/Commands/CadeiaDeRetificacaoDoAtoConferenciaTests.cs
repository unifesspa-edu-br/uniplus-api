namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>As recusas da cadeia de retificação que dependem do ato retificado, todas juntas e no campo do ato retificado.</summary>
public sealed class CadeiaDeRetificacaoDoAtoConferenciaTests
{
    private static readonly TipoDoAtoNoRegistro TipoQueNaoCongela = new("EDITAL_ABERTURA", null, false, false, false);

    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "Ato retificado inexistente é recusado sem escrever o identificador recebido")]
    public async Task Conferir_AtoInexistente_RecusaSemRepetirOId()
    {
        Guid inexistente = Guid.CreateVersion7();

        IReadOnlyList<FieldError> recusas = await ConferirAsync(inexistente, TipoQueNaoCongela);

        ResumoDasRecusas.De(recusas).Should().Equal(("atoRetificadoId", AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado));
        recusas[0].Error.Message.Should().NotContain(inexistente.ToString());
        CadeiaDeRetificacaoDoAto.RetificadoExiste(recusas).Should().BeFalse();
    }

    [Fact(DisplayName = "Classe de congelamento divergente e ato já retificado são recusados juntos, nessa ordem")]
    public async Task Conferir_ClasseDivergenteEAtoJaRetificado_AcumulaAsDuas()
    {
        AtoNormativo retificado = AtoExistente(congela: true);
        _atos.ComoRetificavel(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(AtoExistente(congela: true));

        IReadOnlyList<FieldError> recusas = await ConferirAsync(retificado.Id, TipoQueNaoCongela);

        ResumoDasRecusas.De(recusas).Should().Equal(
            ("atoRetificadoId", AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente),
            ("atoRetificadoId", AtoNormativoErrorCodes.AtoJaRetificado));
        CadeiaDeRetificacaoDoAto.RetificadoExiste(recusas).Should().BeTrue();
    }

    [Fact(DisplayName = "Sem tipo resolvido a classe de congelamento não é conferida, e o ato já retificado ainda é")]
    public async Task Conferir_TipoAusente_ConfereSoALinearidade()
    {
        AtoNormativo retificado = AtoExistente(congela: true);
        _atos.ComoRetificavel(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(AtoExistente(congela: true));

        IReadOnlyList<FieldError> recusas = await ConferirAsync(retificado.Id, tipo: null);

        ResumoDasRecusas.De(recusas).Should().Equal(("atoRetificadoId", AtoNormativoErrorCodes.AtoJaRetificado));
    }

    [Fact(DisplayName = "Ato retificado existente, da mesma classe e ainda não retificado, não tem recusa")]
    public async Task Conferir_AtoRetificavelDaMesmaClasse_SemRecusa()
    {
        AtoNormativo retificado = AtoExistente(congela: false);
        _atos.ComoRetificavel(retificado);

        IReadOnlyList<FieldError> recusas = await ConferirAsync(retificado.Id, TipoQueNaoCongela);

        recusas.Should().BeEmpty();
    }

    private Task<IReadOnlyList<FieldError>> ConferirAsync(Guid atoRetificadoId, TipoDoAtoNoRegistro? tipo) =>
        new CadeiaDeRetificacaoDoAto(_atos).ConferirAsync(atoRetificadoId, tipo, CancellationToken.None);

    private static AtoNormativo AtoExistente(bool congela) =>
        new AtoNormativoDeTeste { CongelaConfiguracao = congela }.Registrar();
}
