namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Manifestacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A manifestação de interesse no processo seletivo (UNI-REQ-0503): o processo novo não a prevê, a definição troca o valor
/// anterior, a recusa não altera o estado e o processo publicado só muda por retificação.
/// </summary>
public sealed class ManifestacaoDeInteresseDoProcessoTests
{
    [Fact(DisplayName = "O processo novo não prevê a manifestação de interesse")]
    public void ProcessoNovo_NaoPrevista() =>
        NovoProcesso().ManifestacaoDeInteresse.Should().BeNull();

    [Fact(DisplayName = "A definição guarda a manifestação declarada")]
    public void Definir_GuardaAManifestacao()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.DefinirManifestacaoDeInteresse("CONFIRMA_A_CADA_CHAMADA").IsSuccess.Should().BeTrue();

        processo.ManifestacaoDeInteresse.Should().Be(ManifestacaoDeInteresse.ConfirmaACadaChamada);
    }

    [Fact(DisplayName = "A nova definição troca o valor anterior")]
    public void Definir_TrocaOValorAnterior()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirManifestacaoDeInteresse("UMA_VEZ");

        processo.DefinirManifestacaoDeInteresse("CONFIRMA_A_CADA_CHAMADA").IsSuccess.Should().BeTrue();

        processo.ManifestacaoDeInteresse.Should().Be(ManifestacaoDeInteresse.ConfirmaACadaChamada);
    }

    [Fact(DisplayName = "Sem código, a definição volta o processo à manifestação não prevista")]
    public void SemCodigo_VoltaANaoPrevista()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirManifestacaoDeInteresse("UMA_VEZ");

        processo.DefinirManifestacaoDeInteresse(null).IsSuccess.Should().BeTrue();

        processo.ManifestacaoDeInteresse.Should().BeNull();
    }

    [Fact(DisplayName = "O código desconhecido é recusado no campo e mantém a declaração anterior")]
    public void CodigoDesconhecido_RecusaSemAlterarOEstado()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirManifestacaoDeInteresse("UMA_VEZ");

        Result resultado = processo.DefinirManifestacaoDeInteresse("OUTRA");

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("manifestacaoDeInteresse", ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido));
        processo.ManifestacaoDeInteresse.Should().Be(ManifestacaoDeInteresse.UmaVez);
    }

    [Fact(DisplayName = "O processo publicado recusa a edição da manifestação, que só muda por retificação")]
    public void ProcessoPublicado_RecusaEdicao()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        processo.DefinirManifestacaoDeInteresse("UMA_VEZ").IsSuccess.Should().BeTrue();
        CenarioDeCongelamento cenario = ApoioDaPublicacaoDoProcessoSeletivo.CenarioDoEdital() with
        {
            Relogio = new RelogioFixo(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        };
        processo.Publicar(cenario).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirManifestacaoDeInteresse("CONFIRMA_A_CADA_CHAMADA");

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.ManifestacaoDeInteresse.Should().Be(ManifestacaoDeInteresse.UmaVez);
    }

    [Fact(DisplayName = "O processo sem manifestação declarada é publicado")]
    public void ProcessoSemManifestacao_Publica()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        CenarioDeCongelamento cenario = ApoioDaPublicacaoDoProcessoSeletivo.CenarioDoEdital() with
        {
            Relogio = new RelogioFixo(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        };

        processo.Publicar(cenario).IsSuccess.Should().BeTrue();

        processo.ManifestacaoDeInteresse.Should().BeNull();
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo de seleção", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
