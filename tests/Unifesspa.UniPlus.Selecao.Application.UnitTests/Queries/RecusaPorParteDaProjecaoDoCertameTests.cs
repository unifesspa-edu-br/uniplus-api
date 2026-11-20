namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// O bloco do envelope que não se deixa ler recusa a projeção do certame citando a parte pelo nome que o
/// contrato público usa, nunca pela chave interna do envelope congelado.
/// </summary>
public sealed class RecusaPorParteDaProjecaoDoCertameTests
{
    public static TheoryData<string, string> Partes()
    {
        TheoryData<string, string> partes = [];
        foreach ((string bloco, string parte) in PartesDoContratoDoCertame.NaOrdemDaLeitura)
        {
            partes.Add(bloco, parte);
        }

        return partes;
    }

    public static TheoryData<string> BlocosComChaveInternaDiferente() =>
        [.. PartesDoContratoDoCertame.BlocosComChaveInternaDiferente];

    [Theory(DisplayName = "O bloco ilegível recusa a leitura do certame citando a parte pelo nome do contrato")]
    [MemberData(nameof(Partes))]
    public void Projetar_BlocoIlegivel_RecusaCitandoAParte(string bloco, string parte)
    {
        DomainError recusa = ProjecaoDoCertameDeTeste.RecusaPorBlocosIlegiveis(bloco);

        recusa.Code.Should().Be(ProjecaoDoCertamePublicado.CodigoEnvelopeInesperado);
        recusa.Message.Should().Contain($"'{parte}'");
    }

    // A parte "quadro de vagas" contém a palavra da chave "vagas"; a mensagem cita a parte entre aspas, e é nessa forma que a chave vazaria.
    [Theory(DisplayName = "A recusa não revela a chave interna do bloco quando ela difere do nome da parte")]
    [MemberData(nameof(BlocosComChaveInternaDiferente))]
    public void Projetar_BlocoIlegivel_NaoRevelaAChaveInterna(string bloco)
    {
        DomainError recusa = ProjecaoDoCertameDeTeste.RecusaPorBlocosIlegiveis(bloco);

        recusa.Message.Should().NotContain($"'{bloco}'");
    }

    [Fact(DisplayName = "Toda parte pública do certame tem a sua linha nesta suíte, e só as públicas")]
    public void Partes_CobremTodosOsBlocosPublicos() =>
        PartesDoContratoDoCertame.NaOrdemDaLeitura.Select(static parte => parte.Bloco)
            .Should().BeEquivalentTo(ClassificacaoDosBlocosDoCertame.Publicados);
}
