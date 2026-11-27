namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.ClassificacaoDoDocumentoDeTeste;

public sealed class LeituraDosCriteriosDeDesempateTests
{
    private static readonly ParametrosDoDesempateNoDocumento SemParametros = new(null, null, []);

    private static CriterioDeDesempateNoDocumento MaiorIdade(int posicao) => new(posicao, Referencia(RegraDeDesempate.MaiorIdade), SemParametros);

    private static CriterioDeDesempateNoDocumento NaEtapa(int posicao, string chave) =>
        new(posicao, Referencia(RegraDeDesempate.MaiorNotaNaEtapa), new(chave, null, []));

    private static CriterioDeDesempateNoDocumento Condicao(int posicao, string? dado, string? operador, JsonElement? valor) =>
        new(posicao, Referencia(RegraDeDesempate.CondicaoSobreDado), SemParametros with { CodigoDoDado = dado, Operador = operador, Valor = valor });

    private static JsonElement Json(string texto) => JsonSerializer.Deserialize<JsonElement>(texto);

    private static ItensLidosDaClassificacao<CriterioDesempate> LerComDados(params CriterioDeDesempateNoDocumento[] criterios) =>
        LeituraDosCriteriosDeDesempate.Ler(criterios, Etapas(), Cadastros(dados: VocabularioDeDados()));

    private static ItensLidosDaClassificacao<CriterioDesempate> Ler(params CriterioDeDesempateNoDocumento[] criterios) =>
        LeituraDosCriteriosDeDesempate.Ler(criterios, Etapas(), Cadastros());

    [Fact(DisplayName = "A ordem do critério é a posição dele entre os adotados, a partir de 1, e o recusado não ocupa posição")]
    public void Ordem_EAPosicaoEntreOsAdotados()
    {
        CriterioDeDesempateNoDocumento desconhecido = new(1, new(RegraDigitada, "v1"), SemParametros);

        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(MaiorIdade(0), desconhecido, new(2, Referencia(RegraDeDesempate.Idoso), new(null, 60, [])));

        lidos.Itens.Select(static item => (item.Item.Ordem, item.PosicaoNoDocumento)).Should().Equal((1, 0), (2, 2));
        lidos.Origem(LeituraDosCriteriosDeDesempate.Lista).PosicaoDeOrigem(LeituraDosCriteriosDeDesempate.Lista, 1).Should().Be(2);
    }

    [Fact(DisplayName = "A regra que o catálogo não tem recusa o critério no ponteiro da regra, sem repetir o código digitado")]
    public void RegraForaDoCatalogo_RecusaSemOTextoEnviado()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(MaiorIdade(0), new(1, new(RegraDigitada, "v1"), SemParametros));

        Causas(lidos.Recusadas).Should().Equal(("/classificacao/criteriosDeDesempate/1/regra/codigo", "CriterioDesempate.RegraNaoEncontrada"));
        lidos.Recusadas.Single().Causa.Error.Message.Should().NotContain(RegraDigitada);
        lidos.Recusadas.Single().Caminho.Should().Be("classificacao.criteriosDeDesempate");
    }

    [Fact(DisplayName = "A regra do catálogo que não é de desempate é recusada como tipo inválido")]
    public void RegraDeOutroTipo_RecusaComoTipoInvalido()
    {
        Causas(Ler(new CriterioDeDesempateNoDocumento(0, Referencia(RegraDeCalculoDaNota.MediaPonderada), SemParametros)).Recusadas).Should().Equal(
            ("/classificacao/criteriosDeDesempate/0/regra/codigo", "CriterioDesempate.RegraTipoInvalido"));
    }

    [Fact(DisplayName = "A etapa citada que a leitura adotou vira o id em memória dela nos parâmetros do critério")]
    public void EtapaAdotada_ViraOIdEmMemoria()
    {
        EtapasLidas etapas = Etapas();

        ItensLidosDaClassificacao<CriterioDesempate> lidos = LeituraDosCriteriosDeDesempate.Ler([NaEtapa(0, EtapaAdotada)], etapas, Cadastros());

        lidos.Recusadas.Should().BeEmpty();
        lidos.Itens.Single().Item.Parametros.Should().BeOfType<ParametrosDesempateMaiorNotaEtapa>()
            .Which.EtapaId.Should().Be(etapas.IdPelaChave[EtapaAdotada]);
    }

    [Fact(DisplayName = "A etapa que o documento não tem recusa o critério sem repetir a chave digitada")]
    public void EtapaInexistente_RecusaSemOTextoEnviado()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(NaEtapa(0, ChaveDigitada));

        Causas(lidos.Recusadas).Should().Equal(("/classificacao/criteriosDeDesempate/0/parametros/etapaChave", "ProcessoSeletivo.EtapaRefDesempateInexistente"));
        lidos.Recusadas.Single().Causa.Error.Message.Should().NotContain(ChaveDigitada).And.Contain("posição 1");
    }

    [Fact(DisplayName = "O critério que cita etapa que a leitura recusou fica fora sem causa nova e sem ocupar posição")]
    public void EtapaRecusadaNaLeitura_FicaForaSemCausa()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(NaEtapa(0, EtapaRecusada), MaiorIdade(1));

        lidos.Recusadas.Should().BeEmpty();
        lidos.Itens.Select(static item => item.Item.Ordem).Should().Equal(1);
    }

    [Fact(DisplayName = "O critério do idoso sem idade mínima e o das áreas sem área são recusados nos parâmetros")]
    public void ParametrosFaltando_Recusa()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(
            new(0, Referencia(RegraDeDesempate.Idoso), SemParametros),
            new(1, Referencia(RegraDeDesempate.MaiorNotaPorAreaDoEnem), SemParametros));

        Causas(lidos.Recusadas).Should().Equal(
            ("/classificacao/criteriosDeDesempate/0/parametros/idadeMinima", "CriterioDesempate.IdadeMinimaObrigatoria"),
            ("/classificacao/criteriosDeDesempate/1/parametros/areas", "CriterioDesempate.AreasObrigatorias"));
    }

    [Fact(DisplayName = "O critério por áreas do ENEM mal escritas é recusado na área, sem ocupar posição")]
    public void AreaMalEscrita_RecusaNaArea()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(
            new(0, Referencia(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new(null, null, ["redacao em minusculas"])),
            new(1, Referencia(RegraDeDesempate.MaiorNotaPorAreaDoEnem), new(null, null, ["REDACAO"])));

        lidos.Recusadas.Single().Causa.Field.Should().Be("/classificacao/criteriosDeDesempate/0/parametros/areas/0");
        lidos.Itens.Select(static item => (item.Item.Ordem, item.PosicaoNoDocumento)).Should().Equal((1, 1));
    }

    [Fact(DisplayName = "A condição sobre dado completa e válida é lida: o critério é adotado na ordem certa, com o dado, o operador e o valor")]
    public void CondicaoSobreDado_CompletaEValida_ELida()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = LerComDados(MaiorIdade(0), Condicao(1, "PROFESSOR_RURAL", "IGUAL", Json("true")));

        lidos.Recusadas.Should().BeEmpty();
        CriterioDesempate adotado = lidos.Itens.Single(static item => item.PosicaoNoDocumento == 1).Item;
        adotado.Ordem.Should().Be(2);
        CondicaoDnf condicao = adotado.Parametros.Should().BeOfType<ParametrosDesempatePredicadoFato>().Subject.Condicao;
        (condicao.Fato, condicao.Operador, condicao.Valor.GetBoolean()).Should().Be(("PROFESSOR_RURAL", Operador.Igual, true));
    }

    [Theory(DisplayName = "A condição sem o dado, sem o operador ou sem o valor é recusada como incompleta, no campo que falta")]
    [InlineData(null, "IGUAL", true, "codigoDoDado")]
    [InlineData("PROFESSOR_RURAL", null, true, "operador")]
    [InlineData("PROFESSOR_RURAL", "IGUAL", false, "valor")]
    public void CondicaoSobreDado_Incompleta_RecusaNoCampoQueFalta(string? dado, string? operador, bool comValor, string campo)
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = LerComDados(Condicao(0, dado, operador, comValor ? Json("true") : null));

        lidos.Itens.Should().BeEmpty();
        Causas(lidos.Recusadas).Should().Equal(($"/classificacao/criteriosDeDesempate/0/parametros/{campo}", "CriterioDesempate.PredicadoFatoIncompleto"));
    }

    [Fact(DisplayName = "O dado que o vocabulário do desempate não tem é recusado no dado")]
    public void CondicaoSobreDado_ForaDoVocabulario_RecusaNoDado()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = LerComDados(Condicao(0, "DADO_INEXISTENTE", "IGUAL", Json("true")));

        lidos.Itens.Should().BeEmpty();
        Causas(lidos.Recusadas).Should().Equal(("/classificacao/criteriosDeDesempate/0/parametros/codigoDoDado", "PredicadoDnf.FatoDesconhecido"));
    }

    [Fact(DisplayName = "O operador que o tipo do dado não admite é recusado no operador")]
    public void CondicaoSobreDado_OperadorIncompativel_RecusaNoOperador()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = LerComDados(Condicao(0, "PROFESSOR_RURAL", "MAIOR_IGUAL", Json("true")));

        Causas(lidos.Recusadas).Should().Equal(("/classificacao/criteriosDeDesempate/0/parametros/operador", "PredicadoDnf.OperadorIncompativelComDominio"));
    }

    [Fact(DisplayName = "O valor fora do domínio do dado é recusado no valor")]
    public void CondicaoSobreDado_ValorForaDoDominio_RecusaNoValor()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = LerComDados(Condicao(0, "MODALIDADE", "IGUAL", Json("\"OUTRA\"")));

        Causas(lidos.Recusadas).Should().Equal(("/classificacao/criteriosDeDesempate/0/parametros/valor", "PredicadoDnf.ValorForaDoDominio"));
    }

    [Fact(DisplayName = "O vocabulário vazio recusa o dado em vez de aceitá-lo sem conferência")]
    public void CondicaoSobreDado_ComVocabularioVazio_RecusaODado()
    {
        ItensLidosDaClassificacao<CriterioDesempate> lidos = Ler(Condicao(0, "PROFESSOR_RURAL", "IGUAL", Json("true")));

        Causas(lidos.Recusadas).Should().Equal(("/classificacao/criteriosDeDesempate/0/parametros/codigoDoDado", "PredicadoDnf.FatoDesconhecido"));
    }
}
