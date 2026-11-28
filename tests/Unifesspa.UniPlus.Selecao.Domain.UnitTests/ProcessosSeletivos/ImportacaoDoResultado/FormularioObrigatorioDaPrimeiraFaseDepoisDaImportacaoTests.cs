namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado.ProcessoDaImportacaoDeTeste;

/// <summary>
/// No processo de resultado importado, a primeira fase depois da importação tem formulário obrigatório que coleta
/// o que a importação não traz (UNI-REQ-0144). A recusa só vale com a fase declarada e no cronograma.
/// </summary>
public sealed class FormularioObrigatorioDaPrimeiraFaseDepoisDaImportacaoTests
{
    private static readonly string[] SoARecusaDoFormulario = [FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio];

    [Fact(DisplayName = "O formulário obrigatório na primeira fase depois da importação é aceito")]
    public void FormularioObrigatorioNaFaseCerta_Aceita()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        CodigosDaRecusaDoFormulario(processo).Should().BeEmpty();
        CodigosDaImportacao(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "A primeira fase declarada sem nenhum formulário recusa o formulário obrigatório")]
    public void FaseDeclaradaSemFormulario_Recusa()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        CodigosDaRecusaDoFormulario(processo).Should().Equal(SoARecusaDoFormulario);
    }

    [Fact(DisplayName = "O formulário facultativo na primeira fase depois da importação não basta")]
    public void FormularioFacultativoNaFase_Recusa()
    {
        ProcessoSeletivo processo = ImportadoConforme();

        RedefinirFormulario(processo, PrimeiraFase(processo), obrigatorio: false);

        CodigosDaRecusaDoFormulario(processo).Should().Equal(SoARecusaDoFormulario);
    }

    [Fact(DisplayName = "O formulário obrigatório em outra fase não basta")]
    public void FormularioObrigatorioEmOutraFase_Recusa()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        Guid primeiraFase = PrimeiraFase(processo);
        FormularioDeTeste.TrocarCronograma(processo, [.. processo.CronogramaFases, FaseDeTeste.Criar(2, "HABILITACAO")]).IsSuccess.Should().BeTrue();

        RedefinirFormulario(processo, processo.CronogramaFases.Single(fase => fase.Id != primeiraFase).Id, obrigatorio: true);

        CodigosDaRecusaDoFormulario(processo).Should().Equal(SoARecusaDoFormulario);
    }

    [Fact(DisplayName = "A primeira fase ainda por declarar recusa só a declaração, e não o formulário")]
    public void FaseSemDeclarar_NaoRecusaOFormulario()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        processo.DefinirImportacaoDoResultado(UmRelatorio, null).IsSuccess.Should().BeTrue();

        CodigosDaRecusaDoFormulario(processo).Should().BeEmpty();
        CodigosDaImportacao(processo).Should().Equal(ImportacaoDoResultadoErrorCodes.PrimeiraFasePorDeclarar);
    }

    [Fact(DisplayName = "A primeira fase que o cronograma trocado não tem gera uma causa só, sem a recusa do formulário")]
    public void CronogramaTrocadoDepoisDaDeclaracao_UmaCausaSo()
    {
        ProcessoSeletivo processo = ImportadoConforme();
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseDeTeste.Criar(2, "HABILITACAO")], []).IsSuccess.Should().BeTrue();

        CodigosDaImportacao(processo).Should().Equal(ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma);
        CodigosDaRecusaDoFormulario(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema sem declaração não exige formulário na primeira fase depois da importação")]
    public void InscricaoPeloSistemaSemDeclaracao_NaoExige()
    {
        ProcessoSeletivo processo = InscricaoPeloSistema();

        CodigosDaRecusaDoFormulario(processo).Should().BeEmpty();
        CodigosDaImportacao(processo).Should().BeEmpty();
    }
}
