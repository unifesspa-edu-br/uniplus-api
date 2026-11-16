namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.CenarioDeConformidadeLegal;

/// <summary>
/// Avaliação de <see cref="DocumentoObrigatorioParaModalidade"/> pela identidade do tipo de documento e da
/// modalidade congelados: renomear o código do cadastro não quebra a regra, e um código reatribuído a outro item
/// reprova mesmo com a string batendo.
/// </summary>
public sealed class AvaliadorDocumentoObrigatorioIdentidadeTests
{
    private const string Laudo = "LAUDO_MEDICO";

    private static ProcessoSeletivo ProcessoComLaudo(Guid identidadeDoLaudo)
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid fase = PrepararProcessoComModalidade(processo, "LB_PPI");
        DefinirDocumentos(processo, ExigenciaGeral(fase, Laudo, identidadeDoLaudo));
        return processo;
    }

    [Fact(DisplayName = "Renomear o tipo e atualizar a regra continua aprovando — a identidade não mudou")]
    public void DocumentoObrigatorio_AposRenomearOTipoEAtualizarARegra_Aprova()
    {
        Guid identidadeDoLaudo = Guid.CreateVersion7();
        ProcessoSeletivo processo = ProcessoComLaudo(identidadeDoLaudo);
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposDocumento = Mapa("LAUDO_MEDICO_PCD", identidadeDoLaudo) };

        RegraAvaliada avaliada = Avaliar(
            processo, NovaRegra("DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO_PCD")), cadastro);

        avaliada.Aprovada.Should().BeTrue("a exigência designa o mesmo documento que a regra exige — só o rótulo mudou");
    }

    [Fact(DisplayName = "Código reatribuído a outro documento reprova, mesmo com a string batendo")]
    public void DocumentoObrigatorio_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = ProcessoComLaudo(Guid.CreateVersion7());
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposDocumento = Mapa(Laudo, Guid.CreateVersion7()) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", Laudo)), cadastro);

        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("designa outro documento", "a mensagem diz o que houve, em vez de mandar procurar exigência ausente");
    }

    [Fact(DisplayName = "Sem exigência alguma, a mensagem continua sendo a de cobertura ausente")]
    public void DocumentoObrigatorio_SemExigencia_MantemMotivoDeCoberturaAusente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        PrepararProcessoComModalidade(processo, "LB_PPI");
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposDocumento = Mapa(Laudo, Guid.CreateVersion7()) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", Laudo)), cadastro);

        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("cobre incondicionalmente").And.NotContain("designa outro documento");
    }

    [Fact(DisplayName = "Exigência condicional continua cobrindo depois de a modalidade ser renomeada")]
    public void DocumentoObrigatorio_ExigenciaCondicionalComModalidadeRenomeada_Aprova()
    {
        // A condição congelada guarda o código que valia quando a exigência foi configurada; o casamento
        // por identidade acomoda a renomeação, em vez de reprovar o processo conforme.
        Guid identidadeDoLaudo = Guid.CreateVersion7();
        ProcessoSeletivo processo = NovoProcesso();
        Guid fase = PrepararProcessoComModalidade(processo, "LB_PPI");
        DefinirDocumentos(processo, ExigenciaCondicionalPorModalidade(fase, Laudo, "LB_PPI", tipoDocumentoOrigemId: identidadeDoLaudo));

        RegraAvaliada avaliada = Avaliar(
            processo,
            NovaRegra("DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI_V2", Laudo)),
            IdentidadesDe(processo) with { Modalidades = Mapa("LB_PPI_V2", IdentidadeDaModalidade(processo)) });

        avaliada.Aprovada.Should().BeTrue("a exigência cobre a mesma modalidade que a regra exige — só o código do cadastro mudou");
    }

    private static Guid IdentidadeDaModalidade(ProcessoSeletivo processo) =>
        processo.DistribuicaoVagas.SelectMany(d => d.Modalidades).First().ModalidadeOrigemId;
}
