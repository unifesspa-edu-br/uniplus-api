namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;

/// <summary>A banca que a etapa requer entra com o tipo do cadastro; a que o domínio ou o cadastro recusa fica de fora inteira, com as causas acumuladas, e suspende a lista de bancas da etapa.</summary>
public sealed class LeituraDasBancasDaEtapaTests
{
    [Fact(DisplayName = "A banca com tipo do cadastro entra na etapa com a chave declarada e a cópia do tipo")]
    public void Banca_ComTipoDoCadastro_EntraNaEtapa()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(BancaDaEtapa(0, "banca_recursos")));

        BancaDaEtapa banca = lidas.Etapas.Single().Bancas.Single();
        banca.Chave.Valor.Should().Be("banca_recursos");
        banca.Tipo.Papel.Should().Be(PapelDaBanca.JulgaAPrimeiraInstancia, "a banca guarda a cópia do tipo que o cadastro tinha ao ser lida");
        banca.PosicaoNoTipo.Should().Be(1);
        PartesRecusadas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de tipo que o cadastro não tem fica de fora e aponta o campo do tipo na banca, sem o texto digitado")]
    public void Banca_TipoInexistente_ApontaOCampo()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(BancaDaEtapa(0, "banca_a"), BancaDaEtapa(1, "banca_b", "TIPO-DIGITADO")));

        lidas.Etapas.Single().Bancas.Should().ContainSingle();
        PartesRecusadas(lidas).Should().Equal(("/etapas/0/bancas/1/tipoBancaCodigo", EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("DIGITADO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A banca recusada deixa a lista de bancas da etapa com parte recusada, e a recusa guarda a chave da banca")]
    public void Banca_Recusada_SuspendeAListaDeBancas()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(BancaDaEtapa(0, "banca_ruim", "TIPO-DIGITADO")));

        Origem(lidas).PartesRecusadasDe("etapas[0]").Should().BeEquivalentTo(EtapaDeclarada.ListaDasBancas);
        Origem(lidas).PartesRecusadas.Single().Chave.Should().Be("banca_ruim");
    }

    [Fact(DisplayName = "A regra de deliberação que o domínio recusa deixa a banca de fora e aponta o campo da regra")]
    public void Banca_DeliberacaoInvalida_ApontaOCampoDaRegra()
    {
        DeclaracaoDaDeliberacao invalida = new(ModoDeDeliberacao.ColegiadaComVotacao.Codigo(), 0, 0, new(0, CriterioDeVotacao.MaioriaSimples.Codigo(), null, EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null));

        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(BancaDaEtapa(0, "banca_a", regra: invalida)));

        lidas.Etapas.Single().Bancas.Should().BeEmpty();
        PartesRecusadas(lidas).Should().Contain(("/etapas/0/bancas/0/regraDeDeliberacao/integrantes", BancaErrorCodes.IntegrantesInvalidos));
    }

    [Fact(DisplayName = "A consequência da ausência que não é opção conhecida deixa a banca de fora e aponta o campo dela")]
    public void Banca_ConsequenciaDesconhecida_ApontaOCampo()
    {
        BancaNoDocumento banca = BancaDaEtapa(0, "banca_a", TipoDeBancaAvaliadora) with { ConsequenciaDaAusencia = "CONSEQUENCIA-DIGITADA" };

        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(banca));

        PartesRecusadas(lidas).Should().Equal(("/etapas/0/bancas/0/consequenciaDaAusencia", BancaErrorCodes.ConsequenciaDaAusenciaInvalida));
    }

    [Fact(DisplayName = "A consequência da ausência conhecida entra na banca que avalia o mérito")]
    public void Banca_ConsequenciaConhecida_EntraNaBanca()
    {
        string consequencia = ConsequenciaDoDescumprimento.EliminaDoProcesso.Codigo();
        BancaNoDocumento banca = BancaDaEtapa(0, "banca_a", TipoDeBancaAvaliadora) with { ConsequenciaDaAusencia = consequencia };

        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(banca));

        lidas.Etapas.Single().Bancas.Single().ConsequenciaDaAusencia.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact(DisplayName = "Tipo inexistente e regra de deliberação inválida na mesma banca acumulam as causas na ordem tipo e regra")]
    public void Banca_TipoERegraInvalidos_Acumulam()
    {
        DeclaracaoDaDeliberacao invalida = new(ModoDeDeliberacao.ColegiadaComVotacao.Codigo(), 0, 0, new(0, CriterioDeVotacao.MaioriaSimples.Codigo(), null, EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null));

        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(BancaDaEtapa(0, "banca_a", "TIPO-DIGITADO", invalida)));

        PartesRecusadas(lidas).First().Should().Be(("/etapas/0/bancas/0/tipoBancaCodigo", EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo));
        PartesRecusadas(lidas).Should().Contain(("/etapas/0/bancas/0/regraDeDeliberacao/integrantes", BancaErrorCodes.IntegrantesInvalidos));
    }

    [Fact(DisplayName = "As categorias de documento que o documento traga na banca da etapa não são lidas")]
    public void Banca_ComCategorias_NaoLeAsCategorias()
    {
        BancaNoDocumento banca = BancaDaEtapa(0, "banca_a") with { CategoriasDocumentoCodigos = ["CATEGORIA-DIGITADA"] };

        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComBancas(banca));

        lidas.Etapas.Single().Bancas.Should().ContainSingle();
        PartesRecusadas(lidas).Should().BeEmpty();
    }
}
