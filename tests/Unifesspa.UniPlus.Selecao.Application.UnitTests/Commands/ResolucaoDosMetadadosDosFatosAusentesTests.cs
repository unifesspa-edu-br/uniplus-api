namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

/// <summary>
/// O metadado dos fatos citados em condição de gatilho se resolve contra o catálogo: cada fato distinto que o catálogo não tem
/// é uma causa própria, na ordem das condições, e o que o catálogo tem não deixa de resolver por causa do ausente.
/// </summary>
public sealed class ResolucaoDosMetadadosDosFatosAusentesTests
{
    private const string CodigoDoFatoAusente = "ProcessoSeletivo.FatoCongeladoNaoEncontrado";

    [Fact(DisplayName = "Duas condições que citam fatos distintos ausentes do catálogo recusam as duas, uma causa por fato, na ordem das condições")]
    public void DoisFatosAusentes_UmaCausaPorFato()
    {
        ProcessoSeletivo processo = ProcessoQueColetaOFato(comItemNovo: false, citaFatoAusenteDoCatalogo: true);

        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> resolucao =
            ResolvedorMetadadosFatosCongelados.Resolver(processo, new Dictionary<string, FatoCandidatoView>());

        resolucao.Errors.Select(static e => e.Error.Code).Should().Equal(CodigoDoFatoAusente, CodigoDoFatoAusente);
        resolucao.Errors[0].Error.Message.Should().Contain(Fato);
        resolucao.Errors[1].Error.Message.Should().Contain(FatoAusenteDoCatalogo);
    }

    [Fact(DisplayName = "O fato ausente citado em duas condições é uma causa só")]
    public void FatoAusenteCitadoDuasVezes_UmaCausa()
    {
        ProcessoSeletivo processo = ProcessoQueColetaOFato(comItemNovo: false, citaFatoAusenteDoCatalogo: true);
        IReadOnlyDictionary<string, FatoCandidatoView> soOFatoCitadoPrimeiro = new Dictionary<string, FatoCandidatoView>
        {
            [Fato] = FatoDoCatalogo("BOOLEANO", "PESSOAL"),
        };

        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> resolucao = ResolvedorMetadadosFatosCongelados.Resolver(processo, soOFatoCitadoPrimeiro);

        resolucao.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain(FatoAusenteDoCatalogo);
    }
}
