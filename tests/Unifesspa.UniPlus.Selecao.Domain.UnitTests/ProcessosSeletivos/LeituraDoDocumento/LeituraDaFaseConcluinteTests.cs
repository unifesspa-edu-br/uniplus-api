namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>
/// A fase concluinte é citada pela chave de uma fase do documento. A chave que nenhuma fase tem é causa de configuração nova; a que cita
/// uma fase que a leitura recusou não é chave inexistente, e a conclusão do ciclo fica suspensa em vez de recusada ou aprovada.
/// </summary>
public sealed class LeituraDaFaseConcluinteTests
{
    private static FaseNoDocumento QueAbreOCiclo(string? concluinte) =>
        Fase(0, "recurso", "RECURSO").Publicando(Preliminar(0, AtoDoResultado)).ComConcluinte(concluinte);

    private static string[] CodigosDaConclusao(IdentificacaoECronogramaLidos lidos) =>
        [.. Cronograma(lidos).ConferirConclusaoDoCiclo().Select(static causa => causa.Error.Code)];

    [Fact(DisplayName = "A chave de uma fase do documento resolve o código canônico da concluinte")]
    public void Chave_DeFaseDoDocumento_ResolveOCodigo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(QueAbreOCiclo("resultado"), Fase(1, "resultado", "RESULTADO").Publicando(Definitivo(0, AtoDoDefinitivo))));

        lidos.Cronograma.CronogramaFases[0].FaseConcluinteCodigo.Should().Be("RESULTADO");
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A chave que nenhuma fase do documento tem usa o código novo, aponta a chave citada e não repete o texto digitado")]
    public void Chave_Inexistente_UsaOCodigoNovo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(QueAbreOCiclo("chave-digitada")));

        lidos.Cronograma.CronogramaFases.Single().FaseConcluinteCodigo.Should().BeNull();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/faseConcluinteChave", DocumentoDaConfiguracaoErrorCodes.ChaveCitadaInexistente));
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("digitada", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A fase cuja concluinte a leitura recusou não é recusada por conclusão não declarada, e a fase sem concluinte continua recusada")]
    public void ConcluinteRecusada_SuspendeAConclusaoNaoDeclarada()
    {
        CodigosDaConclusao(Ler(Documento(QueAbreOCiclo(null)))).Should().Equal("ProcessoSeletivo.ConclusaoNaoDeclarada");
        CodigosDaConclusao(Ler(Documento(QueAbreOCiclo("chave-digitada")))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A concluinte que cita fase recusada pela leitura não gera causa nova nem fica fora do cronograma, e a conclusão fica indeterminada")]
    public void ConcluinteQueCitaFaseRecusada_SuspendeAConclusao()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(QueAbreOCiclo("resultado"), Fase(1, "resultado", "FASE-DIGITADA")));

        PartesRecusadas(lidos).Select(static parte => parte.Codigo).Should().Equal(FaseCronogramaErrorCodes.FaseCanonicaNaoEncontrada);
        CodigosDaConclusao(lidos).Should().BeEmpty();
        Cronograma(lidos).ConclusoesIndeterminadas().Should().ContainSingle();
    }
}
