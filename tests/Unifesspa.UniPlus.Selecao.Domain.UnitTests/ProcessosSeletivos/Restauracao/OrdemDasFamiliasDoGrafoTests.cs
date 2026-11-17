namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoRestaurarConfiguracao;

/// <summary>
/// A restauração confere o grafo por família inteira, na ordem em que a gravação as aplica: etapas, vagas, formulários,
/// desempate, classificação, cronograma e árvore de exigências. Cada teste fixa uma escolha de quem passa a vir primeiro
/// ou de qual texto é o único.
/// </summary>
public sealed class OrdemDasFamiliasDoGrafoTests
{
    private static DomainError? Recusa(GrafoConfiguracao grafo)
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafo);

        return resultado.Error;
    }

    private static CriterioDesempate[] CriteriosAcimaDoTeto() =>
        [.. Enumerable.Range(1, RegrasDoDesempate.CriteriosDesempateMaximo + 1).Select(static ordem =>
            CriterioDesempate.Criar(ordem, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!)];

    private static FatoColetado[] FatosAcimaDoTeto() =>
        [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(static i => FatoColetado.Criar(
            $"FATO_{i}", i, "Campo", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, finalidade: FinalidadeFormulario.Inscricao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!)];

    [Fact(DisplayName = "Critérios de desempate e itens de formulário acima do teto no mesmo grafo: a família dos formulários vem antes da do desempate")]
    public void Restaurar_DesempateEFormulariosAcimaDoTeto_RecusaOsFormularios() =>
        Recusa(Grafo(criterios: CriteriosAcimaDoTeto(), fatosColetados: FatosAcimaDoTeto()))!.Code
            .Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);

    [Fact(DisplayName = "Etapa de nota do ENEM sob classificação que não é do ENEM junto de outra causa do conjunto: a causa do conjunto vem primeiro")]
    public void Restaurar_EnemSemClassificacaoEnemComEnemDuplicada_RecusaADuplicada() =>
        Recusa(Grafo(etapas: [EtapaNotaEnemCongelada(Guid.CreateVersion7(), 1), EtapaNotaEnemCongelada(Guid.CreateVersion7(), 2)]))!.Code
            .Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");

    [Fact(DisplayName = "Etapa de nota do ENEM sob classificação que não é do ENEM sozinha: continua recusada pela classificação")]
    public void Restaurar_EnemSemClassificacaoEnemSozinha_RecusaPelaClassificacao() =>
        Recusa(Grafo(etapas: [EtapaNotaEnemCongelada(Guid.CreateVersion7(), 1)]))!.Code
            .Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");

    [Fact(DisplayName = "Duas fases com o mesmo código e origens diferentes: a mesma fase canônica duas vezes no cronograma")]
    public void Restaurar_FasesComOMesmoCodigoEOrigensDiferentes_RecusaFaseCanonicaDuplicada()
    {
        FaseCronograma[] fases = [new ModeloDeFase("INSCRICAO", 1).Criar(), new ModeloDeFase("INSCRICAO", 2).Criar()];

        Recusa(Grafo(cronogramaFases: fases))!.Code.Should().Be("ProcessoSeletivo.FaseCanonicaDuplicada");
    }

    [Fact(DisplayName = "O identificador de etapa repetido tem o texto único da gravação")]
    public void Restaurar_IdDeEtapaRepetido_UsaOTextoUnico()
    {
        DomainError? recusa = Recusa(OrdemDasRecusasDoGrafoTests.GrafoComProblemas([2]));

        recusa.Should().Be(EtapasDoProcesso.IdRepetido());
    }

    [Fact(DisplayName = "A etapa citada pelo desempate que não existe tem o texto único, no processo")]
    public void Restaurar_DesempateCitaEtapaInexistente_UsaOTextoUnico() =>
        Recusa(OrdemDasRecusasDoGrafoTests.GrafoComProblemas([9]))!.Message
            .Should().StartWith("O critério de desempate na ordem 2 referencia a etapa ").And.EndWith(", que não existe neste processo.");

    [Fact(DisplayName = "A etapa citada pela eliminação que não existe tem o texto único, no processo")]
    public void Restaurar_EliminacaoCitaEtapaInexistente_UsaOTextoUnico() =>
        Recusa(OrdemDasRecusasDoGrafoTests.GrafoComProblemas([10]))!.Message
            .Should().StartWith("A regra de eliminação referencia a etapa ").And.EndWith(", que não existe neste processo.");
}
