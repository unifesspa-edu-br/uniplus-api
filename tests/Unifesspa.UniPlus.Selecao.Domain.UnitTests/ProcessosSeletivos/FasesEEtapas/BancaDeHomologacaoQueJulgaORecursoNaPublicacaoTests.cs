namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A banca de homologação das inscrições é a que julga o recurso da homologação, quando não há banca dedicada no lugar. Por isso
/// a que decide por ato individual precisa declarar a votação do recurso, e a publicação recusa o processo que não a declara.
/// </summary>
public sealed class BancaDeHomologacaoQueJulgaORecursoNaPublicacaoTests
{
    private const string CodigoDaRecusa = "Banca.RegraDeDeliberacaoObrigatoria";
    private const string ItemDaDeliberacao = "cronograma_banca_com_regra_de_deliberacao";

    private static RegraDeDeliberacao AtoIndividualSemVotacao() =>
        RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(ModoDeDeliberacao.AtoIndividual.Codigo(), 1, 0, new(null, null, null, null, null))).Value!;

    private static ProcessoSeletivo ProcessoComABancaDeHomologacao(RegraDeDeliberacao regra)
    {
        BancaRequerida deHomologacao = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.HomologacaoDasInscricoes), [], regra);
        ProcessoSeletivo processo = ApoioConformidadePublicabilidadeEstrutural.ProcessoConforme();

        processo.DefinirCronogramaFases(
            [ApoioConformidadePublicabilidadeEstrutural.FaseBaseDaImportacao(bancas: [deHomologacao, BancasDeTeste.DaFase(TipoDeBanca.ImportacaoDeCandidatos)])], [])
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    private static FieldError[] Causas(ProcessoSeletivo processo) =>
        [.. ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors];

    [Fact(DisplayName = "A banca de homologação das inscrições que decide por ato individual sem votação do recurso é recusada na publicação")]
    public void BancaDeHomologacao_AtoIndividualSemVotacao_PublicacaoRecusada()
    {
        ProcessoSeletivo processo = ProcessoComABancaDeHomologacao(AtoIndividualSemVotacao());

        FieldError causa = Causas(processo).Should().ContainSingle().Subject;

        causa.Field.Should().Be("fases[0].bancasRequeridas[0]");
        causa.Error.Code.Should().Be(CodigoDaRecusa);
        causa.Error.Message.Should().Contain("decide por ato individual e julga recurso por ter avaliado o item recorrido, sem banca dedicada no lugar, mas não declara a votação do recurso.");
        ApoioConformidadePublicabilidadeEstrutural.ExigirPublicacaoRecusada(processo, CodigoDaRecusa);
    }

    [Fact(DisplayName = "O painel deixa vermelho o item da regra de deliberação com a banca de homologação em ato individual sem votação, e só ele")]
    public void BancaDeHomologacao_AtoIndividualSemVotacao_SoOItemDaDeliberacaoVermelho()
    {
        ProcessoSeletivo processo = ProcessoComABancaDeHomologacao(AtoIndividualSemVotacao());

        ApoioConformidadePublicabilidadeEstrutural.SoEstesItensVermelhos(processo, ItemDaDeliberacao);
    }

    [Fact(DisplayName = "A mesma banca de homologação, com a regra de votação, publica sem recusa de deliberação")]
    public void BancaDeHomologacao_ComVotacao_NaoERecusada()
    {
        ProcessoSeletivo processo = ProcessoComABancaDeHomologacao(BancasDeTeste.Regra());

        Causas(processo).Should().BeEmpty();
        ApoioConformidadePublicabilidadeEstrutural.Publicar(processo).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A banca de homologação em ato individual sem votação não é recusada quando há banca dedicada a julgar o recurso no lugar")]
    public void BancaDeHomologacao_ComBancaDedicadaNoLugar_NaoERecusada()
    {
        BancaRequerida deHomologacao = BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.HomologacaoDasInscricoes), [], AtoIndividualSemVotacao());
        ProcessoSeletivo processo = ApoioConformidadePublicabilidadeEstrutural.ProcessoConforme();

        processo.DefinirCronogramaFases(
            [ApoioConformidadePublicabilidadeEstrutural.FaseBaseDaImportacao(bancas: [deHomologacao, BancasDeTeste.DaFase(TipoDeBanca.ImportacaoDeCandidatos), BancasDeTeste.DeRecursosDaFase()])], [])
            .IsSuccess.Should().BeTrue();

        Causas(processo).Should().BeEmpty();
    }
}
