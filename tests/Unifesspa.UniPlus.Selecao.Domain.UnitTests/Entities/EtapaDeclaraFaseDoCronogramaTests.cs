namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioEtapaVinculadaAFase;

/// <summary>
/// A fase declarada pela etapa é a do cronograma: o vínculo é encontrado pelo código, a fase
/// de outro código ou ausente do cronograma é recusada, e qualquer fase pode abrigar várias etapas.
/// </summary>
public sealed class EtapaDeclaraFaseDoCronogramaTests
{
    [Fact(DisplayName = "O checklist projeta a janela da etapa na fase — a conferência e a exibição dizem a mesma coisa")]
    public void ChecklistProjetaAJanelaDaEtapaNaFase()
    {
        // As duas gravações recusam a incoerência na escrita, mas o EF hidrata etapas e fases
        // direto das linhas: a conferência de publicação é a metade que alcança o certame gravado
        // antes de a invariante existir. Sem o item correspondente, a publicação recusaria por
        // uma causa que o painel de conformidade aprovava, e quem lê o painel não teria o que
        // corrigir.
        IReadOnlyList<ItemConformidade> checklist =
            ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(ProcessoConformeFactory.Criar()), CadastrosDeTeste.Padrao);

        checklist.Should().ContainSingle(i => i.Codigo == "cronograma_etapa_fora_da_janela_da_fase")
            .Which.Ok.Should().BeTrue("o processo conforme não tem etapa pendurada fora da fase");
    }

    [Fact(DisplayName = "Etapa que declara fase presente no cronograma passa a ser encontrada por aquela fase")]
    public void EtapaComFaseDeclarada_EhEncontradaPelaFase()
    {
        ProcessoSeletivo processo = Processo();
        FaseCronograma habilitacao = Fase(1, "HABILITACAO");
        processo.DefinirCronogramaFases([habilitacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirEtapas([Etapa("Envio dos documentos pessoais", "HABILITACAO")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // A relação tem uma representação só, o código: a etapa aparece sob a fase que declarou, e não sob nenhuma outra.
        processo.EtapasDaFase("HABILITACAO").Should().ContainSingle()
            .Which.Nome.Should().Be("Envio dos documentos pessoais");
        processo.Etapas.Single().FaseCodigo.Should().Be(habilitacao.Codigo);
    }

    [Fact(DisplayName = "Etapa que declara fase fora do cronograma é recusada, nomeando a fase")]
    public void EtapaComFaseAusente_Recusada()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Envio dos documentos pessoais", "HABILITACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseNoCronograma");
        resultado.Error!.Message.Should().Contain("HABILITACAO");
    }

    [Fact(DisplayName = "Qualquer fase subdivide-se: habilitação com várias etapas é aceita")]
    public void FaseNaoAgrupadora_AceitaVariasEtapas()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Preenchimento do cadastro acadêmico", "HABILITACAO"), Etapa("Envio dos documentos pessoais", "HABILITACAO"), Etapa("Envio dos comprovantes da cota de renda", "HABILITACAO")],
            PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.EtapasDaFase("HABILITACAO").Should().HaveCount(3);
        processo.EtapasDaFase("INSCRICAO").Should().BeEmpty();
    }

    [Fact(DisplayName = "Etapas de fases distintas ficam cada uma na sua")]
    public void EtapasDeFasesDistintas_NaoSeMisturam()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO"), Fase(2, "HABILITACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO"), Etapa("Envio dos documentos pessoais", "HABILITACAO")],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.EtapasDaFase("AVALIACAO").Single().Nome.Should().Be("Prova objetiva");
        processo.EtapasDaFase("HABILITACAO").Single().Nome.Should().Be("Envio dos documentos pessoais");
    }

    [Fact(DisplayName = "Fase agrupadora com etapa declarada nela não é recusada por falta de etapa")]
    public void FaseAgrupadora_ComEtapaDeclarada_Aceita()
    {
        ProcessoSeletivo processo = Processo();
        FaseCronograma avaliacao = FaseDeAvaliacao();

        // A ordem que o vínculo impõe: a fase entra primeiro, e só então a etapa pode
        // declará-la. É por isso que a guarda eager de "agrupadora sem etapa" saiu da
        // gravação do cronograma — ali ela fecharia um ciclo sem ordem possível.
        processo.DefinirCronogramaFases([avaliacao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();
        processo.EtapasDaFase("AVALIACAO").Should().HaveCount(1);
    }

    [Fact(DisplayName = "Etapa sem fase declarada continua aceita — é o formato anterior ao vínculo")]
    public void EtapaSemFaseDeclarada_ContinuaAceita()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "AVALIACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", faseCodigo: null)], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue();

        // Sem fase declarada não há relação, e é assim que se diz isso agora: o código fica
        // nulo e a etapa não aparece sob fase nenhuma. Antes havia um id zerado convivendo com
        // o código nulo, e "sem fase" e "fase que não existe" tinham a mesma aparência.
        processo.Etapas.Single().FaseCodigo.Should().BeNull();
        processo.EtapasDaFase("AVALIACAO").Should().BeEmpty();
    }
}
