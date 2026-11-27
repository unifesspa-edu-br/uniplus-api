namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EixosDoItemCongeladoTests
{
    private static readonly EixosDoItemCongelado Publicado = new("TEXTO", "CATALOGO", null, "PESSOAL", "OBRIGACAO_LEGAL", "Finalidade publicada.");

    public static TheoryData<EixosDoItemCongelado, string> Diferencas => new()
    {
        { Publicado with { TipoDeCampo = "DATA" }, "tipo de campo" },
        { Publicado with { OrigemValores = "OPCOES_DO_PROCESSO" }, "origem dos valores" },
        { Publicado with { Formato = "FORMATO" }, "formato" },
        { Publicado with { ClassificacaoProtecao = "SENSIVEL" }, "classificação de proteção" },
        { Publicado with { HipoteseLegal = "EXECUCAO_POLITICAS_PUBLICAS" }, "hipótese legal" },
        { Publicado with { FinalidadeTratamento = "Finalidade corrigida." }, "finalidade do tratamento" },
    };

    [Theory(DisplayName = "Cada um dos seis eixos identifica a diferença entre o publicado e o novo")]
    [MemberData(nameof(Diferencas))]
    public void PrimeiraTroca_EixoDiferente_IdentificaOEixo(EixosDoItemCongelado novo, string eixo)
    {
        TrocaDeEixo? troca = Publicado.PrimeiraTroca("FATO", novo);

        troca.Should().NotBeNull();
        troca!.Fato.Should().Be("FATO");
        troca.Eixo.Should().Be(eixo);
        troca.ParaErro().Code.Should().Be("Retificacao.EixoDoFatoJaColetadoAlterado");
    }

    [Fact(DisplayName = "A recusa da troca de eixo não repete o valor novo enviado e mantém o dado do candidato e o valor publicado")]
    public void ParaErro_NaoRepeteOValorNovo()
    {
        EixosDoItemCongelado novo = Publicado with { Formato = "FORMATO_NOVO_FICTICIO_ZETA" };

        DomainError erro = Publicado.PrimeiraTroca("DADO_FICTICIO_ALFA", novo)!.ParaErro();

        erro.Code.Should().Be("Retificacao.EixoDoFatoJaColetadoAlterado");
        erro.Message.Should().NotContainEquivalentOf("ZETA").And.Contain("DADO_FICTICIO_ALFA");
    }

    [Fact(DisplayName = "Eixos iguais, inclusive formato nulo, não geram recusa")]
    public void PrimeiraTroca_EixosIguais_NaoRecusa() =>
        Publicado.PrimeiraTroca("FATO", Publicado with { }).Should().BeNull();

    [Fact(DisplayName = "Retirar o formato publicado é troca de eixo")]
    public void PrimeiraTroca_FormatoRetirado_Recusa()
    {
        TrocaDeEixo? troca = (Publicado with { Formato = "FORMATO" }).PrimeiraTroca("FATO", Publicado);

        troca.Should().Be(new TrocaDeEixo("FATO", "formato", "FORMATO"));
    }

    [Fact(DisplayName = "A primeira diferença segue a ordem dos seis eixos")]
    public void PrimeiraTroca_DoisEixosDiferentes_IdentificaOPrimeiro()
    {
        TrocaDeEixo? troca = Publicado.PrimeiraTroca("FATO", Publicado with { OrigemValores = "OPCOES_DO_PROCESSO", FinalidadeTratamento = "Outra finalidade." });

        troca!.Eixo.Should().Be("origem dos valores");
    }

    [Fact(DisplayName = "A comparação dos eixos usa igualdade ordinal")]
    public void PrimeiraTroca_MaiusculasDiferentes_Recusa() =>
        Publicado.PrimeiraTroca("FATO", Publicado with { TipoDeCampo = "texto" })!.Eixo.Should().Be("tipo de campo");
}
