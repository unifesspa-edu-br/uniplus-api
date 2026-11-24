namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>A banca que a fase requer entra com o tipo e as categorias do cadastro; a que o domínio ou o cadastro recusa fica de fora inteira, com as causas acumuladas.</summary>
public sealed class LeituraDasBancasDaFaseTests
{
    private static FaseNoDocumento ComBancas(params BancaNoDocumento[] bancas) => Fase(0, "recurso", "RECURSO") with { Bancas = bancas };

    [Fact(DisplayName = "A banca com tipo e categorias do cadastro entra na fase com o tipo e os candidatos atendidos")]
    public void Banca_ComCadastros_EntraNaFase()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComBancas(Banca(0, "banca_recursos", "ANALISE_RECURSOS", ["IDENTIDADE"]))));

        BancaRequerida banca = lidos.Cronograma.CronogramaFases.Single().BancasRequeridas.Single();
        banca.Chave.Valor.Should().Be("banca_recursos");
        banca.Tipo.Codigo.Should().Be(TipoDeBanca.AnaliseDeRecursos);
        banca.Tipo.Papel.Should().Be(PapelDaBanca.JulgaAPrimeiraInstancia, "a banca guarda a cópia do tipo que o cadastro tinha ao ser lida");
        banca.CandidatosAtendidos.Should().ContainSingle().Which.Nome.Should().Be("Identidade");
        lidos.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de tipo que o cadastro não tem fica de fora e aponta o campo do tipo na banca, sem o texto digitado")]
    public void Banca_TipoInexistente_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComBancas(Banca(0, "banca_recursos", "TIPO-DIGITADO", [], null))));

        lidos.Cronograma.CronogramaFases.Single().BancasRequeridas.Should().BeEmpty();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/bancasRequeridas/0/tipoBancaCodigo", FaseCronogramaErrorCodes.TipoBancaNaoEncontrado));
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("DIGITADO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A categoria que o cadastro não tem aponta a posição dela nos candidatos atendidos da banca")]
    public void Banca_CategoriaInexistente_ApontaAPosicaoDaCategoria()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComBancas(Banca(0, "banca_recursos", "ANALISE_RECURSOS", ["IDENTIDADE", "CATEGORIA-DIGITADA"], null))));

        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/bancasRequeridas/0/categoriasDocumentoCodigos/1", FaseCronogramaErrorCodes.CategoriaDocumentoNaoEncontrada));
    }

    [Fact(DisplayName = "Tipo e categoria inexistentes na mesma banca acumulam as duas causas")]
    public void Banca_TipoECategoriaInexistentes_Acumulam()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComBancas(Banca(0, "banca_recursos", "TIPO-DIGITADO", ["CATEGORIA-DIGITADA"], null))));

        PartesRecusadas(lidos).Select(static parte => parte.Codigo).Should().Equal(
            FaseCronogramaErrorCodes.TipoBancaNaoEncontrado, FaseCronogramaErrorCodes.CategoriaDocumentoNaoEncontrada);
    }

    [Fact(DisplayName = "A regra de deliberação que o domínio recusa deixa a banca de fora e aponta o campo da regra")]
    public void Banca_DeliberacaoInvalida_ApontaOCampoDaRegra()
    {
        DeclaracaoDaDeliberacao invalida = new(ModoDeDeliberacao.ColegiadaComVotacao.Codigo(), 0, 0, new(0, CriterioDeVotacao.MaioriaSimples.Codigo(), null, EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null));

        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComBancas(Banca(0, "banca_recursos", "ANALISE_RECURSOS", [], invalida))));

        lidos.Cronograma.CronogramaFases.Single().BancasRequeridas.Should().BeEmpty();
        PartesRecusadas(lidos).Should().NotBeEmpty().And.OnlyContain(parte => parte.Ponteiro!.StartsWith("/cronograma/fases/0/bancasRequeridas/0/regraDeDeliberacao", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A banca recusada suspende a lista de bancas da fase, e a banca vizinha que o domínio aceita continua na fase")]
    public void Banca_Recusada_SuspendeAListaEMantemAVizinha()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(ComBancas(
            Banca(0, "banca_ruim", "TIPO-DIGITADO", [], null),
            Banca(1, "banca_boa", "ANALISE_RECURSOS", [], null))));

        lidos.Cronograma.CronogramaFases.Single().BancasRequeridas.Should().ContainSingle();
        Cronograma(lidos).Fases.Single().ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas).Should().BeTrue();
        lidos.Origem.PartesRecusadas.Single().Chave.Should().Be("banca_ruim");
    }
}
