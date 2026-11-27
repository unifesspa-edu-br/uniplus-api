namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.VagasDoDocumentoDeTeste;

public sealed class LeituraDoAtendimentoTests
{
    private static AtendimentoLido Ler(AtendimentoNoDocumento? atendimento) => LeituraDoAtendimento.Ler(atendimento, Cadastros());

    [Fact(DisplayName = "O bloco ausente não tem atendimento nem recusa")]
    public void BlocoAusente_NaoTemAtendimentoNemRecusa()
    {
        AtendimentoLido lido = Ler(null);

        lido.Atendimento.Should().BeNull();
        lido.Recusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O atendimento conforme vira a configuração com as condições, os recursos e os tipos de deficiência do cadastro")]
    public void AtendimentoConforme_ViraAConfiguracao()
    {
        AtendimentoLido lido = Ler(AtendimentoConforme());

        lido.Recusadas.Should().BeEmpty();
        lido.Atendimento!.Condicoes.Select(static condicao => condicao.CondicaoCodigo).Should().Equal(CondicaoPcd, CondicaoDeLactante);
        lido.Atendimento.Recursos.Should().ContainSingle();
        lido.Atendimento.TiposDeficiencia.Should().ContainSingle();
    }

    [Fact(DisplayName = "As listas vazias formam o atendimento sem nenhum item")]
    public void ListasVazias_FormamOAtendimentoSemItens()
    {
        AtendimentoLido lido = Ler(new([], [], []));

        lido.Recusadas.Should().BeEmpty();
        lido.Atendimento!.Condicoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "A origem guarda a posição enviada de cada item das três listas")]
    public void Origem_GuardaAPosicaoEnviadaDeCadaItem()
    {
        AtendimentoNoDocumento atendimento = new([new(3, CondicaoPcd), new(8, CondicaoDeLactante)], [new(2, RecursoDeSala)], [new(6, TipoVisual)]);

        Domain.ProcessosSeletivos.FasesEEtapas.OrigemNoDocumento origem = Ler(atendimento).Origem();

        origem.PosicaoDeOrigem(LeituraDoAtendimento.ListaDasCondicoes, 1).Should().Be(8);
        origem.PosicaoDeOrigem(LeituraDoAtendimento.ListaDosRecursos, 0).Should().Be(2);
        origem.PosicaoDeOrigem(LeituraDoAtendimento.ListaDosTiposDeDeficiencia, 0).Should().Be(6);
    }

    [Fact(DisplayName = "O código que o cadastro não tem recusa o item na posição enviada, sem repetir o texto, e todo o bloco fica fora")]
    public void CodigoInexistente_RecusaOItemNaPosicaoEnviada()
    {
        AtendimentoNoDocumento atendimento = new([new(0, CondicaoPcd), new(4, TextoDigitado)], [new(3, TextoDigitado)], [new(5, TextoDigitado)]);

        AtendimentoLido lido = Ler(atendimento);

        lido.Atendimento.Should().BeNull();
        Causas(lido.Recusadas).Should().Equal(
            ("/atendimento/condicoes/4/condicaoCodigo", "AtendimentoEspecializado.CondicaoNaoEncontrada"),
            ("/atendimento/recursos/3/recursoCodigo", "AtendimentoEspecializado.RecursoNaoEncontrado"),
            ("/atendimento/tiposDeficiencia/5/tipoDeficienciaCodigo", "AtendimentoEspecializado.TipoDeficienciaNaoEncontrado"));
        lido.Recusadas.Should().OnlyContain(parte => !parte.Causa.Error.Message.Contains(TextoDigitado, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O tipo de deficiência sem a condição de pessoa com deficiência é recusado na lista dos tipos")]
    public void TipoDeDeficienciaSemCondicaoPcd_RecusaNaLista()
    {
        AtendimentoNoDocumento atendimento = new([new(0, CondicaoDeLactante)], [], [new(0, TipoVisual)]);

        Causas(Ler(atendimento).Recusadas).Should().Equal(("/atendimento/tiposDeficiencia", "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd"));
    }

    [Fact(DisplayName = "O item repetido é recusado na lista dele, antes da recusa de cadastro")]
    public void ItemRepetido_RecusaNaLista()
    {
        AtendimentoNoDocumento atendimento = new([new(0, CondicaoPcd), new(1, CondicaoPcd)], [new(0, TextoDigitado)], []);

        Causas(Ler(atendimento).Recusadas).Should().Equal(
            ("/atendimento/condicoes", "AtendimentoEspecializado.CondicaoDuplicada"),
            ("/atendimento/recursos/0/recursoCodigo", "AtendimentoEspecializado.RecursoNaoEncontrado"));
    }

    [Fact(DisplayName = "O tipo de deficiência sem a condição de pessoa com deficiência continua recusado quando um recurso não existe")]
    public void TipoDeDeficienciaSemCondicaoPcd_ComRecursoInexistente_AcumulaAsDuasCausas()
    {
        AtendimentoNoDocumento atendimento = new([new(0, CondicaoDeLactante)], [new(2, TextoDigitado)], [new(0, TipoVisual)]);

        Causas(Ler(atendimento).Recusadas).Should().Equal(
            ("/atendimento/tiposDeficiencia", "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd"),
            ("/atendimento/recursos/2/recursoCodigo", "AtendimentoEspecializado.RecursoNaoEncontrado"));
    }

    [Fact(DisplayName = "A condição que o cadastro não tem não gera a causa de falta da condição de pessoa com deficiência")]
    public void CondicaoInexistente_NaoGeraACausaDeFaltaDaCondicaoPcd()
    {
        AtendimentoNoDocumento atendimento = new([new(1, TextoDigitado)], [], [new(0, TipoVisual)]);

        Causas(Ler(atendimento).Recusadas).Should().Equal(("/atendimento/condicoes/1/condicaoCodigo", "AtendimentoEspecializado.CondicaoNaoEncontrada"));
    }
}
