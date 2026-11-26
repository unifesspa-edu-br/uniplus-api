namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

public sealed class OrdemDasCausasDosGruposTests
{
    private const string Lista = "documentosExigidos.regrasDeComprovacao";

    [Fact]
    public void AcrescimoDosGrupos_SemReordenacao_SegueAOrdemDoDocumento()
    {
        ProcessoSeletivo processo = ProcessoComDoisGruposQueRetiramOAcrescimo();

        CaminhosDasCausas(processo.Declaracao()).Should().Equal(
            "documentosExigidos.regrasDeComprovacao[0].consequenciaDoDescumprimento",
            "documentosExigidos.regrasDeComprovacao[1].consequenciaDoDescumprimento");
    }

    [Fact]
    public void AcrescimoDosGrupos_ComPosicoesDeOrigemTrocadas_SegueAPosicaoNoDocumento()
    {
        ProcessoSeletivo processo = ProcessoComDoisGruposQueRetiramOAcrescimo();
        OrigemNoDocumento origem = new(new Dictionary<string, IReadOnlyList<int>> { [Lista] = [1, 0] }, []);
        ConfiguracaoDeclarada configuracao = Lida([.. processo.CronogramaFases], [.. processo.Etapas], origem, processo.Declaracao());

        CaminhosDasCausas(configuracao).Should().Equal(
            "documentosExigidos.regrasDeComprovacao[1].consequenciaDoDescumprimento",
            "documentosExigidos.regrasDeComprovacao[0].consequenciaDoDescumprimento");
    }

    [Fact]
    public void AcrescimoDosGrupos_GruposDeclaradosForaDeOrdem_SegueAOrdemDosGrupos()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        processo.DefinirDocumentosExigidosNosFormulariosMinimos([Grupo(processo, 1, "ELIMINA_DO_PROCESSO"), Grupo(processo, 0)]).IsSuccess.Should().BeTrue();

        CaminhosDasCausas(processo.Declaracao()).Should().Equal("documentosExigidos.regrasDeComprovacao[0].consequenciaDoDescumprimento");
    }

    private static IEnumerable<string> CaminhosDasCausas(ConfiguracaoDeclarada configuracao) =>
        CoerenciaDaConsequenciaDoDescumprimento.AcrescimoDosGrupos(configuracao).Select(static causa => causa.Field!);

    private static ProcessoSeletivo ProcessoComDoisGruposQueRetiramOAcrescimo()
    {
        ProcessoSeletivo processo = ProcessoConforme();
        RegraDeComprovacao[] grupos = [.. Enumerable.Range(0, 2).Select(ordem => Grupo(processo, ordem))];
        processo.DefinirDocumentosExigidosNosFormulariosMinimos(grupos).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static RegraDeComprovacao Grupo(ProcessoSeletivo processo, int ordem, string consequencia = "RETIRA_O_ACRESCIMO")
    {
        RegraDeComprovacao item = RegraDeComprovacao.CriarRegraDeDocumento(DocumentoGeral(FaseDoResultado(processo), obrigatorio: false, consequencia: null), 0).Value!;
        return RegraDeComprovacao.CriarGrupo(
            new(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, consequencia), ordem, [BaseLegalDoGrupo(StatusBaseLegal.Resolvido)], [item]).Value!;
    }
}
